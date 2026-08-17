using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Enexis.KabelChecker.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed record DirectionPolylinePickResult(
    bool Cancelled,
    double LengthMeters,
    string LayerName,
    int? SuggestedDirectionNumber,
    string Message);

internal static class DirectionPolylineSelection
{
    private static readonly Regex DirectionNumberRegex = new(
        @"\bK0*(?<number>[1-9]|1[0-2])\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static DirectionPolylinePickResult PickSinglePolyline(string cableName)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return Cancel("Geen actieve tekening.");

        var editor = doc.Editor;
        var database = doc.Database;
        using var documentLock = doc.LockDocument();

        var picked = PromptForPolyline(editor, cableName);
        if (picked.Status != PromptStatus.OK)
            return Cancel("Polyline-selectie geannuleerd.");

        var unitFactor = GetMetersPerDrawingUnit(database.Insunits, out var unitWarning);
        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(picked.ObjectId, OpenMode.ForRead) is not Curve curve)
            return Cancel("Het geselecteerde object kon niet als polyline worden gelezen.");

        try
        {
            var rawLength = GetCurveLength(curve);
            var lengthMeters = rawLength * unitFactor;
            if (lengthMeters <= 0 || double.IsNaN(lengthMeters) || double.IsInfinity(lengthMeters))
                return Cancel("De geselecteerde polyline heeft geen geldige lengte.");

            return Success(lengthMeters, curve.Layer, unitWarning);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            return Cancel($"Lengte kon niet worden bepaald: {ex.Message}");
        }
    }

    public static DirectionPolylinePickResult PickPolylinePartToVirtualCut(string cableName)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return Cancel("Geen actieve tekening.");

        var editor = doc.Editor;
        var database = doc.Database;
        using var documentLock = doc.LockDocument();

        var picked = PromptForPolyline(editor, cableName);
        if (picked.Status != PromptStatus.OK)
            return Cancel("Polyline-selectie geannuleerd.");

        var unitFactor = GetMetersPerDrawingUnit(database.Insunits, out var unitWarning);
        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(picked.ObjectId, OpenMode.ForRead) is not Curve curve)
            return Cancel("Het geselecteerde object kon niet als polyline worden gelezen.");

        try
        {
            var totalRawLength = GetCurveLength(curve);
            if (totalRawLength <= 0)
                return Cancel("De geselecteerde polyline heeft geen geldige lengte.");

            if (curve.StartPoint.DistanceTo(curve.EndPoint) <= Math.Max(totalRawLength * 1e-9, 1e-8))
            {
                return Cancel(
                    "Virtueel knippen is alleen beschikbaar voor een open polyline; een gesloten polyline heeft geen eenduidige begin- en eindzijde.");
            }

            var cutPrompt = editor.GetPoint(new PromptPointOptions(
                "\nKlik het virtuele knippunt op of nabij de polyline: "));
            if (cutPrompt.Status != PromptStatus.OK)
                return Cancel("Virtueel knippunt geannuleerd.");

            var cutOnCurve = curve.GetClosestPointTo(cutPrompt.Value, false);
            var cutDistance = curve.GetDistAtPoint(cutOnCurve);

            var sideOptions = new PromptPointOptions(
                "\nKlik op het deel van de polyline waarvan je de lengte wilt gebruiken: ")
            {
                UseBasePoint = true,
                BasePoint = cutOnCurve
            };

            var sidePrompt = editor.GetPoint(sideOptions);
            if (sidePrompt.Status != PromptStatus.OK)
                return Cancel("Keuze van het kabeldeel geannuleerd.");

            var sideOnCurve = curve.GetClosestPointTo(sidePrompt.Value, false);
            var sideDistance = curve.GetDistAtPoint(sideOnCurve);
            var selected = VirtualCutLengthCalculator.SelectLength(totalRawLength, cutDistance, sideDistance);

            var lengthMeters = selected.Length * unitFactor;
            var sideName = selected.Side == VirtualCutSide.Start ? "beginzijde" : "eindzijde";
            var cutMeters = cutDistance * unitFactor;
            var message =
                $"Virtueel knippunt op {cutMeters:0.00} m vanaf het polylinebegin; {sideName} gekozen. " +
                "De originele polyline is niet aangepast." +
                (string.IsNullOrWhiteSpace(unitWarning) ? string.Empty : Environment.NewLine + unitWarning);

            return Success(lengthMeters, curve.Layer, message);
        }
        catch (InvalidOperationException ex)
        {
            return Cancel(ex.Message);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            return Cancel($"Virtuele kniplengte kon niet worden bepaald: {ex.Message}");
        }
    }

    public static bool TryGetDirectionNumber(string? layerName, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        var match = DirectionNumberRegex.Match(layerName);
        return match.Success &&
               int.TryParse(match.Groups["number"].Value, out number) &&
               number is >= 1 and <= 12;
    }

    private static DirectionPolylinePickResult Success(double lengthMeters, string layerName, string message)
    {
        int? suggested = TryGetDirectionNumber(layerName, out var number) ? number : null;
        return new DirectionPolylinePickResult(false, lengthMeters, layerName, suggested, message);
    }

    private static DirectionPolylinePickResult Cancel(string message) =>
        new(true, 0, string.Empty, null, message);

    private static PromptEntityResult PromptForPolyline(Editor editor, string cableName)
    {
        var options = new PromptEntityOptions($"\nSelecteer de polyline voor {cableName}: ");
        options.SetRejectMessage("\nSelecteer een 2D- of 3D-polyline.");
        options.AddAllowedClass(typeof(Polyline), false);
        options.AddAllowedClass(typeof(Polyline2d), false);
        options.AddAllowedClass(typeof(Polyline3d), false);
        return editor.GetEntity(options);
    }

    private static double GetCurveLength(Curve curve) =>
        Math.Abs(
            curve.GetDistanceAtParameter(curve.EndParam) -
            curve.GetDistanceAtParameter(curve.StartParam));

    private static double GetMetersPerDrawingUnit(UnitsValue units, out string warning)
    {
        warning = string.Empty;
        return units switch
        {
            UnitsValue.Millimeters => 0.001,
            UnitsValue.Centimeters => 0.01,
            UnitsValue.Meters => 1.0,
            UnitsValue.Kilometers => 1000.0,
            UnitsValue.Inches => 0.0254,
            UnitsValue.Feet => 0.3048,
            _ => AssumeMeters(out warning)
        };
    }

    private static double AssumeMeters(out string warning)
    {
        warning = "INSUNITS is niet als een ondersteunde lengteeenheid herkend; teken-eenheden worden als meters behandeld.";
        return 1.0;
    }
}
