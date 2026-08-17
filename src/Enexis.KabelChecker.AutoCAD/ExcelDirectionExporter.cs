using System.Reflection;
using ClosedXML.Excel;

namespace Enexis.KabelChecker.AutoCAD;

internal static class ExcelDirectionExporter
{
    private const string CableSheetName = "Ontwerpstroom_kabel";
    private const string TransformerSheetName = "Ontwerpstroom_trafo";
    private const string TemplateResourceSuffix = "Eea-0205.K_2.0.xlsx";

    public static void Export(string outputPath, IReadOnlyList<DirectionState> directions)
    {
        if (directions.Count == 0)
            throw new InvalidOperationException("Sla eerst minimaal één richting op.");

        using var template = OpenTemplate();
        using var workbook = new XLWorkbook(template);

        if (!workbook.Worksheets.Contains(CableSheetName) || !workbook.Worksheets.Contains(TransformerSheetName))
        {
            throw new InvalidOperationException(
                $"De ingebouwde Excel-template mist '{CableSheetName}' of '{TransformerSheetName}'.");
        }

        var cableTemplate = workbook.Worksheet(CableSheetName);
        foreach (var direction in directions.OrderBy(x => x.Number))
        {
            var sheetName = BuildDirectionSheetName(direction.Number);
            var sheet = cableTemplate.CopyTo(sheetName);
            ClearCounts(sheet);
            WriteDirectionCounts(sheet, direction.ExcelLoads);
        }

        cableTemplate.Delete();

        var transformer = workbook.Worksheet(TransformerSheetName);
        ClearCounts(transformer);
        var totals = directions
            .SelectMany(x => x.ExcelLoads)
            .GroupBy(x => x.ExcelLoadKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count), StringComparer.OrdinalIgnoreCase);
        WriteCounts(transformer, totals);

        workbook.SaveAs(outputPath);
    }

    private static Stream OpenTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith(TemplateResourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("De ingebouwde Enexis Excel-template kon niet worden gevonden.");

        return assembly.GetManifestResourceStream(resourceName)
               ?? throw new InvalidOperationException("De ingebouwde Enexis Excel-template kon niet worden geopend.");
    }

    private static string BuildDirectionSheetName(int directionNumber)
    {
        var name = $"Ontwerpstroom_kabel R{directionNumber}";
        return name.Length <= 31 ? name : $"Kabel R{directionNumber}";
    }

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
}
