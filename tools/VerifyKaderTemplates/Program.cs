using System.Text.Json;
using ClosedXML.Excel;
using Enexis.KabelChecker.AutoCAD;
using Enexis.KabelChecker.ExcelWorker;

if (args.Length != 1)
    throw new ArgumentException("Gebruik: VerifyKaderTemplates <Resources-map>");

// Bestaande stations bewaren de enum als getal. Nieuwe kaders mogen die nummers niet veranderen.
Require((int)KaderVersion.K2024_1_0 == 0 && (int)KaderVersion.K2025_2_0 == 1
    && (int)KaderVersion.K2026_3_2 == 2 && (int)KaderVersion.K2026_3_0 == 3,
    "Kaderversies van bestaande stations zijn veranderd.");
Require(KaderVersionSelection.Current == KaderVersion.K2026_3_2, "Standaardkader veranderd.");

foreach (var version in new[] { KaderVersion.K2026_3_0, KaderVersion.K2026_3_2 })
{
    var isV30 = version == KaderVersion.K2026_3_0;
    var layout = isV30 ? "V30" : "V32";
    var options = ExcelLoadCatalog.For(version);
    var definition = KaderVersions.Get(version);
    var bytes = File.ReadAllBytes(Path.Combine(args[0], definition.ResourceFileName));
    using var input = new XLWorkbook(new MemoryStream(bytes));
    Require(options.Select(x => x.Key).Distinct().Count() == options.Count, "Dubbele belastingcode.");
    Require(options.Select(x => x.Row).Distinct().Count() == options.Count, "Dubbele belastingrij.");
    Require(options.Count == (isV30 ? 49 : 51), "Belastingopties ontbreken.");

    for (var number = 1; number <= 12; number++)
    {
        var sheet = input.Worksheet($"({number})");
        foreach (var option in options)
        {
            var afname = sheet.Cell(option.Row, 6).GetDouble();
            var opwek = sheet.Cell(option.Row, 7).IsEmpty() ? 0 : sheet.Cell(option.Row, 7).GetDouble();
            Require(Math.Abs(option.CableDesignCurrentAmps - Math.Max(afname, opwek)) < 1e-9,
                $"{layout} {sheet.Name} {option.Key}: ontwerpstroom wijkt af van F/G.");
        }
    }

    var lastHalfFirst = isV30 ? 62 : 64;
    var lastHalfLast = isV30 ? 80 : 82;
    var cableNames = input.Worksheet("(1)").Range("O18:O36").Cells()
        .Select(x => x.GetString().Trim()).Where(x => x.Length > 0).ToArray();
    var segments = cableNames.Select(name => new { CableName = name, LengthMeters = 12.345 }).ToArray();
    var request = JsonSerializer.Serialize(new
    {
        Layout = layout, CountFirstRow = 5, CountLastRow = isV30 ? 75 : 79,
        Directions = new[]
        {
            new { Number = 1, Evenredig = true,
                Loads = options.Select((x, i) => new { x.Row, Count = i + 1 }).ToArray(), Segments = segments },
            new { Number = 12, Evenredig = false,
                Loads = options.Select((x, i) => new { x.Row, Count = i + 2 }).ToArray(), Segments = segments }
        }
    });
    var outputPath = Path.Combine(Path.GetTempPath(), $"Enexis-{layout}-{Guid.NewGuid():N}.xlsx");
    try
    {
        ExcelWorker.Export(outputPath, bytes, request);
        using var output = new XLWorkbook(outputPath);
        Require(output.Worksheets.Count == input.Worksheets.Count, "Template-tabbladen veranderd.");
        for (var number = 1; number <= 12; number++)
        {
            var sheet = output.Worksheet($"({number})");
            for (var i = 0; i < options.Count; i++)
            {
                var cell = sheet.Cell(options[i].Row, 2);
                if (number is 1 or 12)
                    Require(cell.GetDouble() == i + (number == 1 ? 1 : 2), $"{layout}: verkeerde aantallen in {cell.Address}.");
                else
                    Require(cell.IsEmpty(), $"{layout}: voorbeeldbelasting achtergebleven in {sheet.Name}.");
            }
            CheckLengths(sheet, 18, 36, number == 1);
            CheckLengths(sheet, lastHalfFirst, lastHalfLast, number == 12);
        }
        foreach (var sheet in input.Worksheets)
            foreach (var cell in sheet.CellsUsed().Where(x => x.HasFormula))
                Require(output.Worksheet(sheet.Name).Cell(cell.Address).FormulaA1 == cell.FormulaA1,
                    $"{layout}: templateformule veranderd in {sheet.Name}!{cell.Address}.");
        Console.WriteLine($"OK - {definition.DisplayName}: {options.Count} opties in alle 12 richtingen, beide kabelprofielen, lege ongebruikte richtingen en behoud van templateformules.");
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }

    void CheckLengths(IXLWorksheet sheet, int firstRow, int lastRow, bool populated)
    {
        for (var row = firstRow; row <= lastRow; row++)
        {
            var cell = sheet.Cell(row, 30);
            var hasCable = cableNames.Contains(sheet.Cell(row, 15).GetString().Trim());
            if (populated && hasCable)
                Require(Math.Abs(cell.GetDouble() - 12.35) < 1e-9, $"{layout}: verkeerde kabellengte in {sheet.Name}!{cell.Address}.");
            else
                Require(cell.IsEmpty(), $"{layout}: voorbeeldlengte achtergebleven in {sheet.Name}!{cell.Address}.");
        }
    }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
