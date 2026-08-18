using ClosedXML.Excel;

var resourceDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "Enexis.KabelChecker.AutoCAD", "Resources");
var files = new[]
{
    Path.Combine(resourceDir, "Eea-0205.K_2.0.xlsx"),
    Path.Combine(resourceDir, "Eea-0205.K 3.2.xlsx")
};

foreach (var file in files)
{
    Console.WriteLine($"=== {Path.GetFileName(file)} ===");
    using var wb = new XLWorkbook(file);
    foreach (var ws in wb.Worksheets)
    {
        var used = ws.RangeUsed();
        Console.WriteLine($"SHEET {ws.Position}: {ws.Name} | used={(used is null ? "<empty>" : used.RangeAddress.ToString())} | formulas={ws.CellsUsed().Count(c => c.HasFormula)}");
    }

    if (Path.GetFileName(file).Contains("2.0", StringComparison.OrdinalIgnoreCase))
    {
        var ws = wb.Worksheet("Ontwerpstroom_kabel");
        Console.WriteLine("--- 2.0 LOAD ROWS ---");
        for (var r = 1; r <= 60; r++)
        {
            var b = ws.Cell(r, 2).GetFormattedString().Trim();
            var c = ws.Cell(r, 3).GetFormattedString().Trim();
            if (!string.IsNullOrWhiteSpace(b))
                Console.WriteLine($"R{r}: B=[{b}] C=[{c}]");
        }
        continue;
    }

    Console.WriteLine("--- 3.2 KEYWORD CELLS ---");
    foreach (var ws in wb.Worksheets)
    {
        foreach (var cell in ws.CellsUsed())
        {
            var text = cell.GetFormattedString().Replace("\r", " ").Replace("\n", " ").Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var lower = text.ToLowerInvariant();
            if (lower.Contains("richting") || lower.Contains("ontwerpstroom") || lower.Contains("evenredig") || lower.Contains("laatste helft") || lower.Contains("kabelleng") || lower.Contains("aantal van") || lower.Contains("type 1:") || lower.Contains("type 6:") || lower.Contains("type 8:") || lower.Contains("type 9:"))
                Console.WriteLine($"{ws.Name}!{cell.Address}: [{text}] FORMULA=[{(cell.HasFormula ? cell.FormulaA1 : "")}]");
        }
    }

    var directionSheets = wb.Worksheets.Where(w => w.Name.Contains("richting", StringComparison.OrdinalIgnoreCase)).ToArray();
    foreach (var ws in directionSheets.Take(2))
    {
        Console.WriteLine($"--- 3.2 INPUT-LIKE CELLS {ws.Name} ---");
        for (var r = 1; r <= 180; r++)
        {
            for (var c = 1; c <= 40; c++)
            {
                var cell = ws.Cell(r,c);
                if (cell.HasFormula) continue;
                var text = cell.GetFormattedString().Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (c <= 4 || c >= 15)
                    Console.WriteLine($"{cell.Address}: [{text}]");
            }
        }
    }

    Console.WriteLine("--- 3.2 FORMULAS REFERENCING DIRECTIONS ---");
    foreach (var ws in wb.Worksheets)
    foreach (var cell in ws.CellsUsed(c => c.HasFormula))
    {
        var f = cell.FormulaA1;
        if (f.Contains("Richting", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"{ws.Name}!{cell.Address}: {f}");
    }
}
