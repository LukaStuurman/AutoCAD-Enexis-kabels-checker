using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Enexis.KabelChecker.AutoCAD;

internal static class TextCurrentBrushSelection
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");
    private static readonly Regex MTextFormatCode = new(
        @"\\[A-Za-z][^;]*;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TextCurrentSelectionResult Read(double radiusMeters)
    {
        if (radiusMeters <= 0 || double.IsNaN(radiusMeters) || double.IsInfinity(radiusMeters))
        {
            return new TextCurrentSelectionResult(
                true,
                Array.Empty<TextCurrentValue>(),
                0,
                "De selectiestraal moet groter dan 0 meter zijn.");
        }

        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return new TextCurrentSelectionResult(
                true,
                Array.Empty<TextCurrentValue>(),
                0,
                "Geen actieve tekening.");
        }

        var editor = doc.Editor;
        var database = doc.Database;

        using var documentLock = doc.LockDocument();

        var metersPerDrawingUnit = GetMetersPerDrawingUnit(database.Insunits, out var unitWarning);
        var radiusDrawingUnits = radiusMeters / metersPerDrawingUnit;
        var candidates = LoadValidTextCandidates(database);

        if (candidates.Count == 0)
        {
            return new TextCurrentSelectionResult(
                false,
                Array.Empty<TextCurrentValue>(),
                0,
                "Geen TEXT/MTEXT met precies één geldige stroomwaarde gevonden in de actieve ruimte.");
        }

        var selectedIds = new HashSet<ObjectId>();
        var values = new List<TextCurrentValue>();

        editor.WriteMessage(
            $"\nRonde stroomselectie actief. Straal: {radiusMeters.ToString("0.0", DutchCulture)} m. " +
            "Klik nabij een stroomtekst; druk Enter om af te ronden.");

        while (true)
        {
            var jig = new TextCurrentBrushJig(radiusDrawingUnits);
            var dragResult = editor.Drag(jig);

            if (jig.FinishedByEnter)
                break;

            if (dragResult.Status != PromptStatus.OK)
            {
                return new TextCurrentSelectionResult(
                    true,
                    Array.Empty<TextCurrentValue>(),
                    0,
                    "Cirkelselectie geannuleerd.");
            }

            var candidate = FindNearestCandidate(jig.Center, radiusDrawingUnits, candidates, selectedIds);
            if (candidate is null)
            {
                editor.WriteMessage("\nGeen nieuwe geldige stroomtekst binnen de cirkel. Klik opnieuw of druk Enter.");
                continue;
            }

            selectedIds.Add(candidate.Id);
            values.Add(new TextCurrentValue(candidate.SourceText, candidate.Amps));
            editor.WriteMessage(
                $"\nToegevoegd: {candidate.Amps.ToString("0.##", DutchCulture)} A " +
                $"({values.Count} geselecteerd). Klik de volgende tekst of druk Enter.");
        }

        var messages = new List<string>();
        if (values.Count == 0)
            messages.Add("Geen stroomteksten toegevoegd met de cirkelselectie.");
        else
            messages.Add($"{values.Count} stroomwaarde(n) met de cirkelselectie toegevoegd.");

        messages.Add($"Selectiestraal: {radiusMeters.ToString("0.0", DutchCulture)} m.");
        if (!string.IsNullOrWhiteSpace(unitWarning))
            messages.Add(unitWarning);

        return new TextCurrentSelectionResult(
            false,
            values,
            0,
            string.Join(Environment.NewLine, messages));
    }

    private static IReadOnlyList<TextCandidate> LoadValidTextCandidates(Database database)
    {
        var candidates = new List<TextCandidate>();

        using var transaction = database.TransactionManager.StartTransaction();
        var currentSpace = transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
        if (currentSpace is null)
            return candidates;

        foreach (ObjectId id in currentSpace)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not Entity entity)
                continue;

            string? sourceText = entity switch
            {
                MText mText => CleanMTextContents(mText.Contents),
                DBText dbText => dbText.TextString,
                _ => null
            };

            if (sourceText is null)
                continue;

            sourceText = sourceText.Trim();
            if (!CurrentTextParser.TryParseSingleCurrent(sourceText, out var amps))
                continue;

            var anchor = entity switch
            {
                MText mText => mText.Location,
                DBText dbText => dbText.Position,
                _ => Point3d.Origin
            };

            var min = anchor;
            var max = anchor;
            try
            {
                var extents = entity.GeometricExtents;
                min = extents.MinPoint;
                max = extents.MaxPoint;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Bij een object zonder bruikbare extents blijft het invoegpunt de trefzone.
            }

            candidates.Add(new TextCandidate(id, sourceText, amps, min, max));
        }

        transaction.Commit();
        return candidates;
    }

    private static TextCandidate? FindNearestCandidate(
        Point3d center,
        double radiusDrawingUnits,
        IReadOnlyList<TextCandidate> candidates,
        ISet<ObjectId> selectedIds)
    {
        TextCandidate? best = null;
        var bestDistance = double.MaxValue;

        foreach (var candidate in candidates)
        {
            if (selectedIds.Contains(candidate.Id))
                continue;

            var distance = DistanceToExtents2d(center, candidate.MinPoint, candidate.MaxPoint);
            if (distance > radiusDrawingUnits + 1e-9 || distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private static double DistanceToExtents2d(Point3d point, Point3d min, Point3d max)
    {
        var lowX = Math.Min(min.X, max.X);
        var highX = Math.Max(min.X, max.X);
        var lowY = Math.Min(min.Y, max.Y);
        var highY = Math.Max(min.Y, max.Y);

        var closestX = Math.Max(lowX, Math.Min(point.X, highX));
        var closestY = Math.Max(lowY, Math.Min(point.Y, highY));
        var dx = point.X - closestX;
        var dy = point.Y - closestY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string CleanMTextContents(string contents)
    {
        if (string.IsNullOrEmpty(contents))
            return string.Empty;

        var cleaned = MTextFormatCode.Replace(contents, string.Empty);
        cleaned = cleaned
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Replace("\\P", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("\\~", " ", StringComparison.Ordinal);
        return cleaned;
    }

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
        warning = "INSUNITS is niet als een ondersteunde lengteeenheid herkend; cirkelstraal wordt behandeld alsof de teken-eenheden meters zijn.";
        return 1.0;
    }

    private sealed record TextCandidate(
        ObjectId Id,
        string SourceText,
        double Amps,
        Point3d MinPoint,
        Point3d MaxPoint);
}
