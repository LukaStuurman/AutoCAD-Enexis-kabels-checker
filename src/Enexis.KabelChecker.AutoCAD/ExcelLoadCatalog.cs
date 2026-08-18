namespace Enexis.KabelChecker.AutoCAD;

internal sealed record ExcelLoadOption(
    string Key,
    string DisplayName,
    int Row,
    double CableDesignCurrentAmps);

internal static class ExcelLoadCatalog
{
    private static readonly IReadOnlyList<ExcelLoadOption> K2024 = new[]
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
        new ExcelLoadOption("T6_3X35_40A", "Type 6 — Toekomstige aansluitwaarde 3x35A t/m 3x40A", 27, 32.0),
        new ExcelLoadOption("T6_3X50A", "Type 6 — Toekomstige aansluitwaarde 3x50A", 28, 44.0),
        new ExcelLoadOption("T6_3X63_80A", "Type 6 — Toekomstige aansluitwaarde 3x63A t/m 3x80A", 29, 65.0),
        new ExcelLoadOption("T7_3X160A", "Type 7 — Toekomstige aansluitwaarde 3x160A", 32, 138.0),
        new ExcelLoadOption("T7_3X250A", "Type 7 — Toekomstige aansluitwaarde 3x250A", 33, 210.0),
        new ExcelLoadOption("T8_3X25A", "Type 8 — Netbewust publiek laden 3x25A", 36, 4.6)
    };

    private static readonly IReadOnlyList<ExcelLoadOption> K2025 = new[]
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

    private static readonly IReadOnlyList<ExcelLoadOption> K2026 = new[]
    {
        new ExcelLoadOption("V32_T1_VRIJSTAAND", "Type 1 — Vrijstaand (t/m 2021, individuele warmtepomp)", 5, 8.1),
        new ExcelLoadOption("V32_T1_TWEE_ONDER_EEN_KAP", "Type 1 — Twee onder een kap (t/m 2021, individuele warmtepomp)", 6, 7.1),
        new ExcelLoadOption("V32_T1_RIJTJESWONING", "Type 1 — Rijtjeswoning (t/m 2021, individuele warmtepomp)", 7, 6.4),
        new ExcelLoadOption("V32_T1_ONBEKEND", "Type 1 — Onbekend (t/m 2021, individuele warmtepomp)", 8, 6.8),
        new ExcelLoadOption("V32_T2_VRIJSTAAND", "Type 2 — Vrijstaand (stads-/gasverwarming)", 11, 3.9),
        new ExcelLoadOption("V32_T2_TWEE_ONDER_EEN_KAP", "Type 2 — Twee onder een kap (stads-/gasverwarming)", 12, 3.9),
        new ExcelLoadOption("V32_T2_RIJTJESWONING", "Type 2 — Rijtjeswoning (stads-/gasverwarming)", 13, 3.9),
        new ExcelLoadOption("V32_T2_ONBEKEND", "Type 2 — Onbekend (stads-/gasverwarming)", 14, 3.9),
        new ExcelLoadOption("V32_T3_VRIJSTAAND", "Type 3 — Vrijstaand (vanaf 2022, individuele warmtepomp)", 17, 7.5),
        new ExcelLoadOption("V32_T3_TWEE_ONDER_EEN_KAP", "Type 3 — Twee onder een kap (vanaf 2022, individuele warmtepomp)", 18, 6.8),
        new ExcelLoadOption("V32_T3_RIJTJESWONING", "Type 3 — Rijtjeswoning (vanaf 2022, individuele warmtepomp)", 19, 6.3),
        new ExcelLoadOption("V32_T3_ONBEKEND", "Type 3 — Onbekend (vanaf 2022, individuele warmtepomp)", 20, 6.4),
        new ExcelLoadOption("V32_T4_APPARTEMENT", "Type 4 — Appartement incl. publiek laden (collectieve WP/stads-/gasverwarming)", 23, 3.4),
        new ExcelLoadOption("V32_T5_APPARTEMENT", "Type 5 — Appartement incl. publiek laden (t/m 2021, individuele warmtepomp)", 27, 5.4),
        new ExcelLoadOption("V32_T6_APPARTEMENT", "Type 6 — Appartement incl. publiek laden (vanaf 2022, individuele warmtepomp)", 30, 5.1),
        new ExcelLoadOption("V32_T8_LT35_G4", "Type 8 — huidig <3x35A — G4/G6/geen of ander gas", 36, 8.7),
        new ExcelLoadOption("V32_T8_LT35_G10_25", "Type 8 — huidig <3x35A — G10 t/m G25", 37, 44.0),
        new ExcelLoadOption("V32_T8_LT35_GT25", "Type 8 — huidig <3x35A — >G25", 38, 65.0),
        new ExcelLoadOption("V32_T8_35_40_G4", "Type 8 — huidig 3x35A t/m 3x40A — G4/G6/geen of ander gas", 39, 32.0),
        new ExcelLoadOption("V32_T8_35_40_G10_25", "Type 8 — huidig 3x35A t/m 3x40A — G10 t/m G25", 40, 65.0),
        new ExcelLoadOption("V32_T8_35_40_GT25", "Type 8 — huidig 3x35A t/m 3x40A — >G25", 41, 65.0),
        new ExcelLoadOption("V32_T8_50_G4", "Type 8 — huidig 3x50A — G4/G6/geen of ander gas", 42, 44.0),
        new ExcelLoadOption("V32_T8_50_G10_25", "Type 8 — huidig 3x50A — G10 t/m G25", 45, 65.0),
        new ExcelLoadOption("V32_T8_50_GT25", "Type 8 — huidig 3x50A — >G25", 46, 65.0),
        new ExcelLoadOption("V32_T8_63_G4", "Type 8 — huidig 3x63A — G4/G6/geen of ander gas", 47, 51.0),
        new ExcelLoadOption("V32_T8_63_G10_25", "Type 8 — huidig 3x63A — G10 t/m G25", 48, 65.0),
        new ExcelLoadOption("V32_T8_63_GT25", "Type 8 — huidig 3x63A — >G25", 49, 65.0),
        new ExcelLoadOption("V32_T8_80_G4", "Type 8 — huidig 3x80A — G4/G6/geen of ander gas", 50, 65.0),
        new ExcelLoadOption("V32_T8_80_G10_25", "Type 8 — huidig 3x80A — G10 t/m G25", 51, 65.0),
        new ExcelLoadOption("V32_T8_80_GT25", "Type 8 — huidig 3x80A — >G25", 52, 65.0),
        new ExcelLoadOption("V32_T9_100_G4", "Type 9 — huidig 3x100A — G4/G6/geen of ander gas", 55, 85.0),
        new ExcelLoadOption("V32_T9_100_G10_25", "Type 9 — huidig 3x100A — G10 t/m G25", 56, 210.0),
        new ExcelLoadOption("V32_T9_100_GT25", "Type 9 — huidig 3x100A — >G25", 57, 210.0),
        new ExcelLoadOption("V32_T9_125_G4", "Type 9 — huidig 3x125A — G4/G6/geen of ander gas", 58, 106.0),
        new ExcelLoadOption("V32_T9_125_G10_25", "Type 9 — huidig 3x125A — G10 t/m G25", 59, 210.0),
        new ExcelLoadOption("V32_T9_125_GT25", "Type 9 — huidig 3x125A — >G25", 60, 210.0),
        new ExcelLoadOption("V32_T9_160_G4", "Type 9 — huidig 3x160A — G4/G6/geen of ander gas", 61, 138.0),
        new ExcelLoadOption("V32_T9_160_G10_25", "Type 9 — huidig 3x160A — G10 t/m G25", 62, 210.0),
        new ExcelLoadOption("V32_T9_160_GT25", "Type 9 — huidig 3x160A — >G25", 63, 210.0),
        new ExcelLoadOption("V32_T9_200_G4", "Type 9 — huidig 3x200A — G4/G6/geen of ander gas", 64, 210.0),
        new ExcelLoadOption("V32_T9_200_G10_25", "Type 9 — huidig 3x200A — G10 t/m G25", 65, 210.0),
        new ExcelLoadOption("V32_T9_200_GT25", "Type 9 — huidig 3x200A — >G25", 66, 210.0),
        new ExcelLoadOption("V32_T9_250_G4", "Type 9 — huidig 3x250A — G4/G6/geen of ander gas", 67, 210.0),
        new ExcelLoadOption("V32_T9_250_G10_25", "Type 9 — huidig 3x250A — G10 t/m G25", 68, 210.0),
        new ExcelLoadOption("V32_T9_250_GT25", "Type 9 — huidig 3x250A — >G25", 69, 210.0),
        new ExcelLoadOption("V32_T10_3X25A", "Type 10 — Netbewust publiek laden 3x25A", 72, 4.8),
        new ExcelLoadOption("V32_T11_1X6A", "Type 11 — Straatmeubilair 1x6A", 75, 4.3),
        new ExcelLoadOption("V32_T11_1X10A", "Type 11 — Straatmeubilair 1x10A", 76, 8.7),
        new ExcelLoadOption("V32_T11_3X25A", "Type 11 — Straatmeubilair 3x25A", 77, 5.8),
        new ExcelLoadOption("V32_T11_3X40_50A", "Type 11 — Straatmeubilair 3x40A t/m 3x50A", 78, 22.0),
        new ExcelLoadOption("V32_T11_3X63_80A", "Type 11 — Straatmeubilair 3x63A t/m 3x80A", 79, 36.0)
    };

    public static IReadOnlyList<ExcelLoadOption> For(KaderVersion version) => version switch
    {
        KaderVersion.K2024_1_0 => K2024,
        KaderVersion.K2025_2_0 => K2025,
        KaderVersion.K2026_3_2 => K2026,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public static ExcelLoadOption? FindByKey(KaderVersion version, string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : For(version).FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<ExcelLoadOption> FindByAmps(KaderVersion version, double amps) =>
        For(version).Where(x => Math.Abs(x.CableDesignCurrentAmps - amps) <= 1e-9).ToArray();
}
