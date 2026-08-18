using ClosedXML.Excel;

var resourceDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "Enexis.KabelChecker.AutoCAD", "Resources");
var files = new[]
{
    Path.Combine(resourceDir, "Eea-0205.K 1.0 - Copy.xlsx"),
    Path.Combine(resourceDir, "Eea-0205.K_2.0.xlsx"),
    Path.Combine(resourceDir, "Eea-0205.K 3.2.xlsx")
};

foreach (var file in files)
{
    Console.WriteLine($"=== {Path.GetFileName(file)} ===");
    using var wb = new XLWorkbook(file);
    for (var i = 1; i <= wb.Worksheets.Count; i++)
    {
        var ws = wb.Worksheet(i);
        var used = ws.RangeUsed();
        var range = used is null ? "<empty>" : used.RangeAddress.ToString();
        var formulas = ws.CellsUsed().Count(c => c.HasFormula);
        Console.WriteLine($"SHEET {i}: {ws.Name} | used={range} | formulas={formulas}");
    }

    if (Path.GetFileName(file).Contains("3.2", StringComparison.OrdinalIgnoreCase))
    {
        var directionSheets = wb.Worksheets
            .Where(w => w.Name.Contains("richting", StringComparison.OrdinalIgnoreCase))
            .OrderBy(w => w.Position)
            .ToArray();

        foreach (var ws in directionSheets.Take(2))
            Dump(ws, 1, 140, 1, 32, 500);

        foreach (var ws in wb.Worksheets.Where(w =>
                     w.Name.Contains("trafo", StringComparison.OrdinalIgnoreCase) ||
                     w.Name.Contains("transform", StringComparison.OrdinalIgnoreCase)))
            Dump(ws, 1, 140, 1, 32, 500);
    }
    else
    {
        foreach (var sheetName in new[]
                 {
                     "Ontwerpstroom_kabel",
                     "Ontwerpstroom_trafo",
                     "Controle_kabel_evenredig",
                     "Controle_kabel_laatste_helft"
                 })
        {
            if (wb.Worksheets.Contains(sheetName))
                Dump(wb.Worksheet(sheetName), 1, 70, 1, 20, 500);
        }
    }
}

static void Dump(IXLWorksheet ws, int firstRow, int lastRow, int firstCol, int lastCol, int limit)
{
    Console.WriteLine($"--- DUMP {ws.Name} ---");
    var printed = 0;
    for (var row = firstRow; row <= lastRow; row++)
    {
        for (var col = firstCol; col <= lastCol; col++)
        {
            var cell = ws.Cell(row, col);
            if (cell.IsEmpty() && !cell.HasFormula)
                continue;

            var value = cell.GetFormattedString().Replace("\r", " ").Replace("\n", " ");
            var formula = cell.HasFormula ? cell.FormulaA1 : string.Empty;
            Console.WriteLine($"{cell.Address}: VALUE=[{value}] FORMULA=[{formula}]");
            printed++;
            if (printed >= limit)
            {
                Console.WriteLine($"... dump limit {limit} bereikt ...");
                return;
            }
        }
    }
}
