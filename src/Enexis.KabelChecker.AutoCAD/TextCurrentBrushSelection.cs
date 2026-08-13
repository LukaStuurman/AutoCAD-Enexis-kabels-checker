using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Enexis.KabelChecker.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed record TextCurrentBrushSelectionResult(
    bool Cancelled,
    IReadOnlyList<TextCurrentObjectValue> AddedValues,
    IReadOnlyList<TextCurrentObjectValue> RemovedValues,
    string Message);

internal static class TextCurrentBrushSelection
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");
    private static readonly Regex MTextFormatCode = new(@"\\[A-Za-z][^;]*;", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TextCurrentBrushSelectionResult Read(
        double radiusMeters,
        IReadOnlyCollection<ObjectId>? selectedObjectIds = null)
    {
        if (radiusMeters <= 0 || double.IsNaN(radiusMeters) || double.IsInfinity(radiusMeters))
        {
            return new TextCurrentBrushSelectionResult(
                true,
                Array.Empty<TextCurrentObjectValue>(),
                Array.Empty<TextCurrentObjectValue>(),
                "De selectiestraal moet groter dan 0 meter zijn.");
        }

        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return new TextCurrentBrushSelectionResult(
                true,
                Array.Empty<TextCurrentObjectValue>(),
                Array.Empty<TextCurrentObjectValue>(),
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
            return new TextCurrentBrushSelectionResult(
                false,
                Array.Empty<TextCurrentObjectValue>(),
                Array.Empty<TextCurrentObjectValue>(),
                "Geen TEXT/MTEXT met precies één geldige stroomwaarde gevonden in de actieve ruimte.");
        }

        var candidateById = candidates.ToDictionary(x => x.Id);
        var initialSelectedIds = new HashSet<ObjectId>(
            (selectedObjectIds ?? Array.Empty<ObjectId>())
                .Where(candidateById.ContainsKey));
        var workingSelectedIds = new HashSet<ObjectId>(initialSelectedIds);

        editor.WriteMessage(
            $"\nRonde stroomselectie actief. Straal: {radiusMeters.ToString("0.0", DutchCulture)} m. " +
            "Klik = alle geldige teksten binnen de cirkel toevoegen; Shift+klik = eerder geselecteerde teksten binnen de cirkel verwijderen; Enter = klaar.");

        foreach (var id in initialSelectedIds)
            SetEntityHighlight(database, id, true);

        try
        {
            while (true)
            {
                var jig = new TextCurrentBrushJig(radiusDrawingUnits);
                var dragResult = editor.Drag(jig);
                if (jig.FinishedByEnter)
                    break;

                if (dragResult.Status != PromptStatus.OK)
                {
                    return new TextCurrentBrushSelectionResult(
                        true,
                        Array.Empty<TextCurrentObjectValue>(),
                        Array.Empty<TextCurrentObjectValue>(),
                        "Cirkelselectie geannuleerd.");
                }

                if (jig.ShiftPressed)
                {
                    var found = FindCandidatesWithinCircle(
                        jig.Center,
                        radiusDrawingUnits,
                        candidates,
                        candidate => workingSelectedIds.Contains(candidate.Id));

                    if (found.Count == 0)
                    {
                        editor.WriteMessage("\nGeen eerder geselecteerde stroomteksten binnen de cirkel om te verwijderen.");
                        continue;
                    }

                    foreach (var candidate in found)
                    {
                        workingSelectedIds.Remove(candidate.Id);
                        SetEntityHighlight(database, candidate.Id, false);
                    }

                    editor.WriteMessage(
                        $"\n{found.Count} tekst(en) gedeselecteerd ({workingSelectedIds.Count} object(en) blijven geselecteerd)." );
                }
                else
                {
                    var found = FindCandidatesWithinCircle(
                        jig.Center,
                        radiusDrawingUnits,
                        candidates,
                        candidate => !workingSelectedIds.Contains(candidate.Id));

                    if (found.Count == 0)
                    {
                        editor.WriteMessage("\nGeen nieuwe geldige stroomteksten binnen de cirkel.");
                        continue;
                    }

                    foreach (var candidate in found)
                    {
                        workingSelectedIds.Add(candidate.Id);
                        SetEntityHighlight(database, candidate.Id, true);
                    }

                    editor.WriteMessage(
                        $"\n{found.Count} tekst(en) toegevoegd ({workingSelectedIds.Count} object(en) geselecteerd)." );
                }
            }

            var addedValues = workingSelectedIds
                .Except(initialSelectedIds)
                .Where(candidateById.ContainsKey)
                .Select(id => candidateById[id])
                .Select(ToObjectValue)
                .ToList();

            var removedValues = initialSelectedIds
                .Except(workingSelectedIds)
                .Where(candidateById.ContainsKey)
                .Select(id => candidateById[id])
                .Select(ToObjectValue)
                .ToList();

            var messages = new List<string>
            {
                $"Cirkelwijziging: {addedValues.Count} toegevoegd, {removedValues.Count} gedeselecteerd.",
                $"Selectiestraal: {radiusMeters.ToString("0.0", DutchCulture)} m."
            };
            if (!string.IsNullOrWhiteSpace(unitWarning))
                messages.Add(unitWarning);

            return new TextCurrentBrushSelectionResult(
                false,
                addedValues,
                removedValues,
                string.Join(Environment.NewLine, messages));
        }
        finally
        {
            foreach (var id in initialSelectedIds.Union(workingSelectedIds))
                SetEntityHighlight(database, id, false);
        }
    }

    private static TextCurrentObjectValue ToObjectValue(TextCandidate candidate) =>
        new(candidate.Id, candidate.SourceText, candidate.Amps);

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
            }

            candidates.Add(new TextCandidate(id, sourceText, amps, min, max));
        }

        transaction.Commit();
        return candidates;
    }

    private static IReadOnlyList<TextCandidate> FindCandidatesWithinCircle(
        Point3d center,
        double radius,
        IReadOnlyList<TextCandidate> candidates,
        Func<TextCandidate, bool> predicate) =>
        candidates
            .Where(predicate)
            .Select(x => (Candidate: x, Distance: DistanceToExtents2d(center, x.MinPoint, x.MaxPoint)))
            .Where(x => x.Distance <= radius + 1e-9)
            .OrderBy(x => x.Distance)
            .Select(x => x.Candidate)
            .ToList();

    private static void SetEntityHighlight(Database database, ObjectId id, bool highlight)
    {
        try
        {
            using var transaction = database.TransactionManager.StartOpenCloseTransaction();
            if (transaction.GetObject(id, OpenMode.ForRead) is Entity entity)
            {
                if (highlight)
                    entity.Highlight();
                else
                    entity.Unhighlight();
            }
            transaction.Commit();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
        }
    }

    private static double DistanceToExtents2d(Point3d point, Point3d min, Point3d max)
    {
        var closestX = Math.Max(Math.Min(min.X, max.X), Math.Min(point.X, Math.Max(min.X, max.X)));
        var closestY = Math.Max(Math.Min(min.Y, max.Y), Math.Min(point.Y, Math.Max(min.Y, max.Y)));
        var dx = point.X - closestX;
        var dy = point.Y - closestY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string CleanMTextContents(string contents)
    {
        if (string.IsNullOrEmpty(contents))
            return string.Empty;

        var cleaned = MTextFormatCode.Replace(contents, string.Empty);
        return cleaned
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Replace("\\P", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("\\~", " ", StringComparison.Ordinal);
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
