using ClosedXML.Excel;

var root = Path.Combine(Directory.GetCurrentDirectory(), "src", "Enexis.KabelChecker.AutoCAD", "Resources");
CheckLegacy(Path.Combine(root, "Eea-0205.K 1.0 - Copy.xlsx"), 17);
CheckLegacy(Path.Combine(root, "Eea-0205.K_2.0.xlsx"), 18);
Check2026(Path.Combine(root, "Eea-0205.K 3.2.xlsx"));
Console.WriteLine("Alle drie kaderversies kunnen worden ingevuld, opgeslagen en opnieuw geopend.");

static void CheckLegacy(string source, int controlRow)
{
    var output = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
    using (var wb = new XLWorkbook(source))
    {
        wb.Worksheet("Ontwerpstroom_kabel").Cell(5, 1).Value = 2;
        wb.Worksheet("Controle_kabel_evenredig").Cell(controlRow, 17).Value = 12.34;
        wb.SaveAs(output);
    }
    using (var wb = new XLWorkbook(output))
    {
        if (wb.Worksheet("Ontwerpstroom_kabel").Cell(5, 1).GetDouble() != 2)
            throw new InvalidOperationException($"{Path.GetFileName(source)}: aantal niet bewaard.");
        if (Math.Abs(wb.Worksheet("Controle_kabel_evenredig").Cell(controlRow, 17).GetDouble() - 12.34) > 1e-9)
            throw new InvalidOperationException($"{Path.GetFileName(source)}: kabellengte niet bewaard.");
    }
    File.Delete(output);
    Console.WriteLine($"OK - {Path.GetFileName(source)}");
}

static void Check2026(string source)
{
    var output = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
    using (var wb = new XLWorkbook(source))
    {
        var direction = wb.Worksheet("(1)");
        direction.Cell(5, 2).Value = 3;
        direction.Cell(18, 30).Value = 45.67;
        direction.Cell(64, 30).Value = 45.67;
        if (!wb.Worksheet("Transformator").Cell(5, 2).HasFormula)
            throw new InvalidOperationException("3.2: Transformator B5 moet een formule blijven.");
        wb.SaveAs(output);
    }
    using (var wb = new XLWorkbook(output))
    {
        var direction = wb.Worksheet("(1)");
        if (direction.Cell(5, 2).GetDouble() != 3)
            throw new InvalidOperationException("3.2: richting-aantal niet bewaard.");
        if (Math.Abs(direction.Cell(18, 30).GetDouble() - 45.67) > 1e-9)
            throw new InvalidOperationException("3.2: evenredige kabellengte niet bewaard.");
        if (Math.Abs(direction.Cell(64, 30).GetDouble() - 45.67) > 1e-9)
            throw new InvalidOperationException("3.2: laatste-helft kabellengte niet bewaard.");
        if (!wb.Worksheet("Transformator").Cell(5, 2).HasFormula)
            throw new InvalidOperationException("3.2: Transformator-formule is verloren gegaan.");
    }
    File.Delete(output);
    Console.WriteLine("OK - Eea-0205.K 3.2.xlsx inclusief Transformator-formule");
}
