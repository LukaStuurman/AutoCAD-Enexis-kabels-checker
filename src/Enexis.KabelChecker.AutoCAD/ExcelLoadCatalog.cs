namespace Enexis.KabelChecker.AutoCAD;

internal sealed record ExcelLoadOption(
    string Key,
    string DisplayName,
    int Row,
    double CableDesignCurrentAmps);

internal static class ExcelLoadCatalog
{
    public static IReadOnlyList<ExcelLoadOption> All { get; } = new[]
    {
        new ExcelLoadOption("T1_VRIJSTAAND", "Type 1 — Vrijstaand", 5, 10.3),
        new ExcelLoadOption("T1_TWEE_ONDER_EEN_KAP", "Type 1 — Twee onder een kap", 6, 8.8),
        new ExcelLoadOption("T1_RIJTJESWONING", "Type 1 — Rijtjeswoning", 7, 7.8),
        new ExcelLoadOption("T1_ONBEKEND", "Type 1 — Onbekend", 8, 8.4),
        new ExcelLoadOption("T2_VRIJSTAAND", "Type 2 — Vrijstaand", 11, 4.1),
        new ExcelLoadOption("T2_TWEE_ONDER_EEN_KAP", "Type 2 — Twee onder een kap", 12, 4.1),
        new ExcelLoadOption("T2_RIJTJESWONING", "Type 2 — Rijtjeswoning", 13, 4.1),
        new ExcelLoadOption("T2_ONBEKEND", "Type 2 — Onbekend", 14, 4.1),
        new ExcelLoadOption("T3_APPARTEMENT", "Type 3 — Appartement incl. publiek laden", 17, 3.5),
        new ExcelLoadOption("T4_APPARTEMENT", "Type 4 — Appartement incl. publiek laden", 21, 6.0),
        new ExcelLoadOption("T6_3X25A", "Type 6 — Toekomstige aansluitwaarde 3x25A", 27, 8.7),
        new ExcelLoadOption("T6_3X35_40A", "Type 6 — Toekomstige aansluitwaarde 3x35A t/m 3x40A", 28, 32.0),
        new ExcelLoadOption("T6_3X50A", "Type 6 — Toekomstige aansluitwaarde 3x50A", 29, 44.0),
        new ExcelLoadOption("T6_3X63_80A", "Type 6 — Toekomstige aansluitwaarde 3x63A t/m 3x80A", 30, 65.0),
        new ExcelLoadOption("T7_3X160A", "Type 7 — Toekomstige aansluitwaarde 3x160A", 33, 138.0),
        new ExcelLoadOption("T7_3X250A", "Type 7 — Toekomstige aansluitwaarde 3x250A", 34, 210.0),
        new ExcelLoadOption("T8_3X25A", "Type 8 — Netbewust publiek laden 3x25A", 38, 6.7),
        new ExcelLoadOption("T9_3X25A", "Type 9 — Straatmeubilair 3x25A", 41, 5.8),
        new ExcelLoadOption("T9_3X40_50A", "Type 9 — Straatmeubilair 3x40A t/m 3x50A", 42, 22.0),
        new ExcelLoadOption("T9_3X63_80A", "Type 9 — Straatmeubilair 3x63A t/m 3x80A", 43, 36.0)
    };

    public static ExcelLoadOption? FindByKey(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : All.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<ExcelLoadOption> FindByAmps(double amps) =>
        All.Where(x => Math.Abs(x.CableDesignCurrentAmps - amps) <= 1e-9).ToArray();

    public static string? AutoKeyForAmps(double amps)
    {
        var matches = FindByAmps(amps);
        return matches.Count == 1 ? matches[0].Key : null;
    }
}
