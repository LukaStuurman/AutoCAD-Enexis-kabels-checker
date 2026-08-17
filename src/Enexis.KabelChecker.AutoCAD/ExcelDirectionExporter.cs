using System.IO;
using ClosedXML.Excel;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal static class ExcelDirectionExporter
{
    private const string CableSheetName = "Ontwerpstroom_kabel";
    private const string TransformerSheetName = "Ontwerpstroom_trafo";
    private const string EvenredigControlSheetName = "Controle_kabel_evenredig";
    private const string LastHalfControlSheetName = "Controle_kabel_laatste_helft";
    private const string EmbeddedTemplateFileName = "Eea-0205.K_2.0.xlsx";

    private const int ControlFirstCableRow = 18;
    private const int ControlLastCableRow = 37;
    private const int ControlCableNameColumn = 2;   // B
    private const int ControlLengthColumn = 17;     // Q

    public static void Export(string outputPath, IReadOnlyList<DirectionState> directions)
    {
        if (directions.Count == 0)
            throw new InvalidOperationException("Sla eerst minimaal één richting op.");

        using var templateStream = OpenEmbeddedTemplate();
        using var workbook = new XLWorkbook(templateStream);
        ValidateTemplates(workbook);

        var cableTemplate = workbook.Worksheet(CableSheetName);
        var evenredigTemplate = workbook.Worksheet(EvenredigControlSheetName);
        var lastHalfTemplate = workbook.Worksheet(LastHalfControlSheetName);

        foreach (var direction in directions.OrderBy(x => x.Number))
        {
            var cableSheet = cableTemplate.CopyTo(BuildDirectionCableSheetName(direction.Number));
            ClearCounts(cableSheet);
            WriteDirectionCounts(cableSheet, direction.ExcelLoads);

            var controlTemplate = direction.Profile == LoadProfile.Evenredig
                ? evenredigTemplate
                : lastHalfTemplate;
            var controlSheet = controlTemplate.CopyTo(BuildDirectionControlSheetName(direction));
            ClearControlCableLengths(controlSheet);
            WriteControlCableLengths(controlSheet, direction.Segments);
        }

        cableTemplate.Delete();
        evenredigTemplate.Delete();
        lastHalfTemplate.Delete();

        var transformer = workbook.Worksheet(TransformerSheetName);
        ClearCounts(transformer);
        var totals = directions
            .SelectMany(x => x.ExcelLoads)
            .GroupBy(x => x.ExcelLoadKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count), StringComparer.OrdinalIgnoreCase);
        WriteCounts(transformer, totals);

        workbook.SaveAs(outputPath);
    }

    private static Stream OpenEmbeddedTemplate()
    {
        var assembly = typeof(ExcelDirectionExporter).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(EmbeddedTemplateFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("Het ingebouwde Enexis Excel-template kon niet worden gevonden in de plugin.");

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Het ingebouwde Enexis Excel-template kon niet worden geopend.");
    }

    private static void ValidateTemplates(XLWorkbook workbook)
    {
        var required = new[]
        {
            CableSheetName,
            TransformerSheetName,
            EvenredigControlSheetName,
            LastHalfControlSheetName
        };

        var missing = required.Where(x => !workbook.Worksheets.Contains(x)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Het ingebouwde Excel-template mist: " + string.Join(", ", missing.Select(x => $"'{x}'")) + ".");
        }
    }

    private static string BuildDirectionCableSheetName(int directionNumber)
    {
        var name = $"Ontwerpstroom_kabel R{directionNumber}";
        return name.Length <= 31 ? name : $"Kabel R{directionNumber}";
    }

    private static string BuildDirectionControlSheetName(DirectionState direction) =>
        direction.Profile == LoadProfile.Evenredig
            ? BuildSafeSheetName($"Controle_evenredig R{direction.Number}", $"Ctrl 50% R{direction.Number}")
            : BuildSafeSheetName($"Controle_laatste_helft R{direction.Number}", $"Ctrl 75% R{direction.Number}");

    private static string BuildSafeSheetName(string preferred, string fallback) =>
        preferred.Length <= 31 ? preferred : fallback;

    private static void ClearCounts(IXLWorksheet sheet)
    {
        foreach (var option in ExcelLoadCatalog.All)
            sheet.Cell(option.Row, 1).Clear(XLClearOptions.Contents);
    }

    private static void WriteDirectionCounts(IXLWorksheet sheet, IReadOnlyList<ExcelMappedLoad> loads)
    {
        var counts = loads
            .GroupBy(x => x.ExcelLoadKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count), StringComparer.OrdinalIgnoreCase);
        WriteCounts(sheet, counts);
    }

    private static void WriteCounts(IXLWorksheet sheet, IReadOnlyDictionary<string, int> counts)
    {
        foreach (var pair in counts)
        {
            var option = ExcelLoadCatalog.FindByKey(pair.Key);
            if (option is null)
                throw new InvalidOperationException($"Onbekende Excel-belastingcode: {pair.Key}.");

            sheet.Cell(option.Row, 1).Value = pair.Value;
        }
    }

    private static void ClearControlCableLengths(IXLWorksheet sheet)
    {
        for (var row = ControlFirstCableRow; row <= ControlLastCableRow; row++)
            sheet.Cell(row, ControlLengthColumn).Clear(XLClearOptions.Contents);
    }

    private static void WriteControlCableLengths(IXLWorksheet sheet, IReadOnlyList<CableSegment> segments)
    {
        var lengths = segments
            .GroupBy(x => x.CableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => Math.Round(x.Sum(y => y.LengthMeters), 2, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in lengths)
        {
            var row = FindControlCableRow(sheet, pair.Key);
            if (row is null)
            {
                throw new InvalidOperationException(
                    $"Kabeltype '{pair.Key}' uit richtinggegevens is niet gevonden in tabblad '{sheet.Name}'.");
            }

            sheet.Cell(row.Value, ControlLengthColumn).Value = pair.Value;
            sheet.Cell(row.Value, ControlLengthColumn).Style.NumberFormat.Format = "0.00";
        }
    }

    private static int? FindControlCableRow(IXLWorksheet sheet, string cableName)
    {
        for (var row = ControlFirstCableRow; row <= ControlLastCableRow; row++)
        {
            var name = sheet.Cell(row, ControlCableNameColumn).GetString().Trim();
            if (name.Equals(cableName, StringComparison.OrdinalIgnoreCase))
                return row;
        }

        return null;
    }
}
