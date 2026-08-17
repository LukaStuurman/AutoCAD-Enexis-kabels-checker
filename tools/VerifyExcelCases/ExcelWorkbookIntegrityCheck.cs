using ClosedXML.Excel;

internal static class ExcelWorkbookIntegrityCheck
{
    public static void Run()
    {
        var templatePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "Enexis.KabelChecker.AutoCAD",
            "Resources",
            "Eea-0205.K_2.0.xlsx");

        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Excel-template voor integriteitstest ontbreekt.", templatePath);

        using var workbook = new XLWorkbook(templatePath);

        var cableTemplate = workbook.Worksheet("Ontwerpstroom_kabel");
        var evenredigTemplate = workbook.Worksheet("Controle_kabel_evenredig");
        var lastHalfTemplate = workbook.Worksheet("Controle_kabel_laatste_helft");

        cableTemplate.CopyTo("Ontwerpstroom_kabel R2");
        evenredigTemplate.CopyTo("Controle_evenredig R2");
        cableTemplate.CopyTo("Ontwerpstroom_kabel R12");
        lastHalfTemplate.CopyTo("Controle_laatste_helft R12");

        cableTemplate.Delete();
        evenredigTemplate.Delete();
        lastHalfTemplate.Delete();

        var outputPath = Path.Combine(Path.GetTempPath(), $"enexis-integrity-{Guid.NewGuid():N}.xlsx");
        try
        {
            workbook.SaveAs(outputPath);

            using var reopened = new XLWorkbook(outputPath);
            var required = new[]
            {
                "Ontwerpstroom_kabel R2",
                "Controle_evenredig R2",
                "Ontwerpstroom_kabel R12",
                "Controle_laatste_helft R12"
            };

            foreach (var sheetName in required)
            {
                if (!reopened.Worksheets.Contains(sheetName))
                    throw new InvalidOperationException($"Integriteitstest mist tabblad '{sheetName}'.");
            }

            Console.WriteLine("OK - Excel exportintegriteit: template geopend, R2/R12 gekopieerd, opgeslagen en opnieuw geopend.");
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}
