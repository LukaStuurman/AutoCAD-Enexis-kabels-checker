using ClosedXML.Excel;

var file = Path.Combine(Directory.GetCurrentDirectory(), "src", "Enexis.KabelChecker.AutoCAD", "Resources", "Eea-0205.K 3.2.xlsx");
using var wb = new XLWorkbook(file);
var ws = wb.Worksheet("(1)");

Console.WriteLine("LOADS");
for (var row = 1; row <= 83; row++)
{
    var b = ws.Cell(row, 2).GetFormattedString().Trim();
    var c = ws.Cell(row, 3).GetFormattedString().Trim();
    var f = ws.Cell(row, 6).GetFormattedString().Trim();
    var g = ws.Cell(row, 7).GetFormattedString().Trim();
    if (!string.IsNullOrWhiteSpace(b) || !string.IsNullOrWhiteSpace(c) || !string.IsNullOrWhiteSpace(f) || !string.IsNullOrWhiteSpace(g))
        Console.WriteLine($"R{row}: B=[{b}] C=[{c}] F=[{f}] G=[{g}]");
}

Console.WriteLine("EVENREDIG");
for (var row = 12; row <= 45; row++)
{
    var o = ws.Cell(row, 15).GetFormattedString().Trim();
    var p = ws.Cell(row, 16).GetFormattedString().Trim();
    var ad = ws.Cell(row, 30).GetFormattedString().Trim();
    if (!string.IsNullOrWhiteSpace(o) || !string.IsNullOrWhiteSpace(p) || !string.IsNullOrWhiteSpace(ad))
        Console.WriteLine($"R{row}: O=[{o}] P=[{p}] AD=[{ad}]");
}

Console.WriteLine("LAATSTE");
for (var row = 58; row <= 90; row++)
{
    var o = ws.Cell(row, 15).GetFormattedString().Trim();
    var p = ws.Cell(row, 16).GetFormattedString().Trim();
    var ad = ws.Cell(row, 30).GetFormattedString().Trim();
    if (!string.IsNullOrWhiteSpace(o) || !string.IsNullOrWhiteSpace(p) || !string.IsNullOrWhiteSpace(ad))
        Console.WriteLine($"R{row}: O=[{o}] P=[{p}] AD=[{ad}]");
}

var trafo = wb.Worksheet("Transformator");
Console.WriteLine("TRAFO_A");
for (var row = 1; row <= 83; row++)
{
    var a = trafo.Cell(row, 1);
    if (a.HasFormula) Console.WriteLine($"A{row}: {a.FormulaA1}");
}
