using System.Globalization;
using System.Text.RegularExpressions;

namespace Enexis.KabelChecker.Core;

public enum LoadProfile
{
    Evenredig,
    LaatsteHelft
}

public sealed record CableSpec(
    string Name,
    int CrossSectionMm2,
    string Material,
    double ResistanceOhmPerKm,
    double ReactanceOhmPerKm,
    double SummerAmpacityA);

public sealed record CableSegment(string CableName, double LengthMeters);

public sealed record FuseOption(
    int FuseAmps,
    int MaxDesignCurrentAmps,
    double MaxImpedanceOhm);

public sealed record FuseAssessment(
    FuseOption Option,
    bool ImpedanceOk,
    bool AmpacityOk)
{
    public bool Allowed => ImpedanceOk && AmpacityOk;

    public string FailureReason => Allowed
        ? "toegestaan"
        : string.Join(", ", new[]
        {
            ImpedanceOk ? null : "impedantie te hoog",
            AmpacityOk ? null : "stroombelastbaarheid kabel te laag"
        }.Where(x => x is not null));
}

public sealed record CalculationResult(
    LoadProfile Profile,
    double TotalResistanceOhm,
    double TotalReactanceOhm,
    double TotalImpedanceOhm,
    double LimitingCableAmpacityA,
    IReadOnlyList<CableSegment> Segments,
    IReadOnlyList<FuseAssessment> Assessments,
    FuseAssessment? MaximumAllowed)
{
    public int? FuseAmps => MaximumAllowed?.Option.FuseAmps;
    public int? MaxDesignCurrentAmps => MaximumAllowed?.Option.MaxDesignCurrentAmps;
}

public static class CableCatalog
{
    public static IReadOnlyList<CableSpec> All { get; } = new[]
    {
        new CableSpec("4*240mm2 Al", 240, "Al", 0.129, 0.073, 343.6020),
        new CableSpec("4*150mm2 Al", 150, "Al", 0.206, 0.079, 260.2125),
        new CableSpec("4*120mm2 Al", 120, "Al", 0.281, 0.081, 232.8426),
        new CableSpec("4*95mm2 Al",   95, "Al", 0.320, 0.082, 203.6016),
        new CableSpec("4*70mm2 Al",   70, "Al", 0.443, 0.0835, 169.1442),
        new CableSpec("4*50mm2 Al",   50, "Al", 0.641, 0.085, 137.3436),
        new CableSpec("4*35mm2 Al",   35, "Al", 0.868, 0.101, 115.0929),
        new CableSpec("4*25mm2 Al",   25, "Al", 1.200, 0.094, 95.9931),
        new CableSpec("4*16mm2 Al",   16, "Al", 1.910, 0.096, 74.3337),

        new CableSpec("4*185mm2 Cu", 185, "Cu", 0.107, 0.068, 382.9842),
        new CableSpec("4*150mm2 Cu", 150, "Cu", 0.125, 0.068, 339.0741),
        new CableSpec("4*95mm2 Cu",   95, "Cu", 0.194, 0.069, 266.5143),
        new CableSpec("4*70mm2 Cu",   70, "Cu", 0.268, 0.072, 222.7014),
        new CableSpec("4*50mm2 Cu",   50, "Cu", 0.387, 0.085, 179.9739),
        new CableSpec("4*35mm2 Cu",   35, "Cu", 0.524, 0.100, 152.0127),
        new CableSpec("4*25mm2 Cu",   25, "Cu", 0.727, 0.094, 126.6111),
        new CableSpec("4*16mm2 Cu",   16, "Cu", 1.150, 0.097, 97.8642)
    };

    private static readonly IReadOnlyDictionary<string, CableSpec> ByName =
        All.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string name, out CableSpec spec) =>
        ByName.TryGetValue(name, out spec!);

    public static CableSpec Get(string name) =>
        TryGet(name, out var spec)
            ? spec
            : throw new ArgumentException($"Onbekend kabeltype: {name}", nameof(name));

    public static bool TryRecognize(string? text, out CableSpec spec)
    {
        spec = null!;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text
            .ToUpperInvariant()
            .Replace("²", "2", StringComparison.Ordinal)
            .Replace("MM^2", "MM2", StringComparison.Ordinal)
            .Replace("MM²", "MM2", StringComparison.Ordinal);

        var material = normalized.Contains("CU", StringComparison.Ordinal) ? "Cu" :
                       normalized.Contains("AL", StringComparison.Ordinal) ? "Al" : null;

        if (material is null)
            return false;

        var matches = Regex.Matches(normalized, @"(?<!\d)(240|185|150|120|95|70|50|35|25|16)(?!\d)");
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                continue;

            var found = All.FirstOrDefault(x =>
                x.CrossSectionMm2 == size &&
                x.Material.Equals(material, StringComparison.OrdinalIgnoreCase));

            if (found is not null)
            {
                spec = found;
                return true;
            }
        }

        return false;
    }
}

public static class FuseProfiles
{
    private static readonly int[] FuseAmps = { 63, 80, 100, 125, 160, 200, 250 };
    private static readonly int[] DesignCurrent = { 57, 72, 90, 113, 144, 180, 225 };

    private static readonly double[] EvenredigImpedance =
        { 0.250, 0.250, 0.204, 0.163, 0.128, 0.092, 0.062 };

    private static readonly double[] LaatsteHelftImpedance =
        { 0.215, 0.170, 0.136, 0.109, 0.085, 0.068, 0.055 };

    public static IReadOnlyList<FuseOption> Get(LoadProfile profile)
    {
        var limits = profile == LoadProfile.Evenredig
            ? EvenredigImpedance
            : LaatsteHelftImpedance;

        return Enumerable.Range(0, FuseAmps.Length)
            .Select(i => new FuseOption(FuseAmps[i], DesignCurrent[i], limits[i]))
            .ToArray();
    }
}

public sealed class KabelCheckerEngine
{
    public CalculationResult Calculate(IEnumerable<CableSegment> input, LoadProfile profile)
    {
        ArgumentNullException.ThrowIfNull(input);

        var grouped = input
            .Where(x => x.LengthMeters > 0)
            .GroupBy(x => x.CableName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CableSegment(g.Key, g.Sum(x => x.LengthMeters)))
            .ToArray();

        if (grouped.Length == 0)
            throw new InvalidOperationException("Voer minimaal één kabellengte groter dan 0 meter in.");

        foreach (var segment in grouped)
        {
            if (segment.LengthMeters < 0 || double.IsNaN(segment.LengthMeters) || double.IsInfinity(segment.LengthMeters))
                throw new InvalidOperationException($"Ongeldige lengte voor {segment.CableName}.");

            _ = CableCatalog.Get(segment.CableName);
        }

        var totalR = grouped.Sum(segment =>
        {
            var cable = CableCatalog.Get(segment.CableName);
            return cable.ResistanceOhmPerKm * segment.LengthMeters / 1000.0;
        });

        var totalX = grouped.Sum(segment =>
        {
            var cable = CableCatalog.Get(segment.CableName);
            return cable.ReactanceOhmPerKm * segment.LengthMeters / 1000.0;
        });

        var totalZ = Math.Sqrt(totalR * totalR + totalX * totalX);
        var limitingAmpacity = grouped.Min(segment => CableCatalog.Get(segment.CableName).SummerAmpacityA);

        var assessments = FuseProfiles.Get(profile)
            .Select(option => new FuseAssessment(
                option,
                totalZ <= option.MaxImpedanceOhm + 1e-12,
                limitingAmpacity + 1e-12 >= option.MaxDesignCurrentAmps))
            .ToArray();

        var maximum = assessments.LastOrDefault(x => x.Allowed);

        return new CalculationResult(
            profile,
            totalR,
            totalX,
            totalZ,
            limitingAmpacity,
            grouped,
            assessments,
            maximum);
    }
}
