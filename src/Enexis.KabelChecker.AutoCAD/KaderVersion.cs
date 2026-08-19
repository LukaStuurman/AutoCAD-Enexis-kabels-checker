namespace Enexis.KabelChecker.AutoCAD;

internal enum KaderVersion
{
    K2024_1_0,
    K2025_2_0,
    K2026_3_2
}

internal sealed record KaderVersionDefinition(
    KaderVersion Version,
    string DisplayName,
    string ResourceFileName)
{
    public override string ToString() => DisplayName;
}

internal static class KaderVersions
{
    public static IReadOnlyList<KaderVersionDefinition> All { get; } = new[]
    {
        new KaderVersionDefinition(KaderVersion.K2024_1_0, "2024 — Eea-0205.K 1.0", "Eea-0205.K 1.0 - Copy.xlsx"),
        new KaderVersionDefinition(KaderVersion.K2025_2_0, "2025 — Eea-0205.K 2.0", "Eea-0205.K 2.0.xlsx"),
        new KaderVersionDefinition(KaderVersion.K2026_3_2, "2026 — Eea-0205.K 3.2", "Eea-0205.K 3.2.xlsx")
    };

    public static KaderVersionDefinition Get(KaderVersion version) =>
        All.Single(x => x.Version == version);
}

internal static class KaderVersionSelection
{
    public static KaderVersion Current { get; private set; } = KaderVersion.K2026_3_2;

    public static void SetCurrent(KaderVersion version) => Current = version;

    public static KaderVersion EnsureSelected(IWin32Window? owner = null) => Current;

    public static KaderVersion SelectForExport(IWin32Window? owner = null) => Current;
}
