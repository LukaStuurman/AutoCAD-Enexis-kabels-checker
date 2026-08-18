using ClosedXML.Excel;
var file = Path.Combine(Directory.GetCurrentDirectory(), "src", "Enexis.KabelChecker.AutoCAD", "Resources", "Eea-0205.K 3.2.xlsx");
using var wb = new XLWorkbook(file);
var ws = wb.Worksheet("(1)");
Console.WriteLine("DETAIL_ROWS");
for (var row = 1; row <= 81; row++)
{
    var vals = new List<string>();
    for (var col = 1; col <= 8; col++)
    {
        var value = ws.Cell(row, col).GetFormattedString().Replace("\r", " ").Replace("\n", " ").Trim();
        if (!string.IsNullOrWhiteSpace(value)) vals.Add($"{ws.Cell(row,col).Address}=[{value}]");
    }
    if (vals.Count > 0) Console.WriteLine(string.Join(" | ", vals));
}
Console.WriteLine("TRAFO_FORMULAS");
var trafo = wb.Worksheet("Transformator");
for (var row = 1; row <= 83; row++)
for (var col = 1; col <= 14; col++)
{
    var cell = trafo.Cell(row,col);
    if (cell.HasFormula && cell.FormulaA1.Contains("(1)")) Console.WriteLine($"{cell.Address}={cell.FormulaA1}");
}
