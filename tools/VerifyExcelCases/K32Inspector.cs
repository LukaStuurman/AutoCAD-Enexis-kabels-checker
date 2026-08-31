using System.Globalization;
using ClosedXML.Excel;

internal static class K32Inspector
{
    public static void Print()
    {
        var path = Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "src",
            "Enexis.KabelChecker.AutoCAD",
            "Resources",
            "Eea-0205.K 3.2.xlsx"));

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("(1)");
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber()
            ?? throw new InvalidOperationException("Geen gebruikte kolommen gevonden in K3.2.");

        var headers = sheet.Range(1, 1, 6, lastColumn).CellsUsed().ToArray();
        var afnameHeader = headers.FirstOrDefault(cell =>
        {
            var text = Normalize(cell.GetString());
            return text.Contains("ontwerpstroom") && text.Contains("eenheid") && !text.Contains("opwek");
        });
        var opwekHeader = headers.FirstOrDefault(cell =>
        {
            var text = Normalize(cell.GetString());
            return text.Contains("ontwerpstroom") && text.Contains("eenheid") && text.Contains("opwek");
        });

        if (afnameHeader is null || opwekHeader is null)
            throw new InvalidOperationException("K3.2 ontwerpstroomkolommen niet gevonden.");

        Console.WriteLine($"K32_HEADERS|afname={afnameHeader.Address}|opwek={opwekHeader.Address}");
        var afnameColumn = afnameHeader.Address.ColumnNumber;
        var opwekColumn = opwekHeader.Address.ColumnNumber;

        for (var row = 5; row <= 79; row++)
        {
            var afname = ReadNumber(sheet.Cell(row, afnameColumn));
            var opwek = ReadNumber(sheet.Cell(row, opwekColumn));
            if (afname is null && opwek is null)
                continue;

            var max = Math.Max(afname ?? 0.0, opwek ?? 0.0);
            Console.WriteLine(
                $"K32|{row}|{Format(afname)}|{Format(opwek)}|{max.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
    }

    private static double? ReadNumber(IXLCell cell)
    {
        if (cell.TryGetValue<double>(out var value))
            return value;

        var text = cell.GetString().Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("nl-NL"), out value))
            return value;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return value;
        return null;
    }

    private static string Format(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";

    private static string Normalize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim().ToLowerInvariant();
}
