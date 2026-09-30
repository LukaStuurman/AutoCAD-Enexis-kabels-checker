namespace Enexis.KabelChecker.AutoCAD;

internal enum DesignCurrentMode { Automatisch, Verbruik, Opwek }

internal sealed record DesignCurrentTotals(
    double CableConsumptionAmps,
    double CableGenerationAmps,
    double StationConsumptionAmps,
    double StationGenerationAmps,
    int Count)
{
    public static DesignCurrentTotals Empty { get; } = new(0, 0, 0, 0, 0);
    public double CableCurrent(DesignCurrentMode mode) => mode switch
    {
        DesignCurrentMode.Verbruik => CableConsumptionAmps,
        DesignCurrentMode.Opwek => CableGenerationAmps,
        _ => Math.Max(CableConsumptionAmps, CableGenerationAmps)
    };
    public double StationCurrent(DesignCurrentMode mode) => mode switch
    {
        DesignCurrentMode.Verbruik => StationConsumptionAmps,
        DesignCurrentMode.Opwek => StationGenerationAmps,
        _ => Math.Max(StationConsumptionAmps, StationGenerationAmps)
    };
    public string CableBasis(DesignCurrentMode mode) => mode == DesignCurrentMode.Automatisch
        ? CableGenerationAmps > CableConsumptionAmps ? "opwek" : "verbruik"
        : mode.ToString().ToLowerInvariant();
    public string StationBasis(DesignCurrentMode mode) => mode == DesignCurrentMode.Automatisch
        ? StationGenerationAmps > StationConsumptionAmps ? "opwek" : "verbruik"
        : mode.ToString().ToLowerInvariant();
}

internal static class DesignCurrentCalculator
{
    public static DesignCurrentTotals Calculate(KaderVersion version, IEnumerable<ExcelMappedLoad> loads)
    {
        double cc = 0, cg = 0, sc = 0, sg = 0;
        var count = 0;
        foreach (var load in loads)
        {
            if (load.Count <= 0)
                throw new InvalidOperationException("Een aansluiting moet een positief aantal hebben.");
            var option = ExcelLoadCatalog.FindByKey(version, load.ExcelLoadKey)
                ?? throw new InvalidOperationException($"Aansluittype '{load.ExcelLoadKey}' ontbreekt in {KaderVersions.Get(version).DisplayName}.");
            cc += option.CableConsumptionAmps * option.CablePhaseFactor * load.Count;
            cg += option.CableGenerationAmps * option.CablePhaseFactor * load.Count;
            sc += option.StationConsumptionAmps * load.Count;
            sg += option.StationGenerationAmps * load.Count;
            count = checked(count + load.Count);
        }
        return new(cc, cg, sc, sg, count);
    }

    public static bool MatchesInputs(IReadOnlyList<CurrentLoadInput> inputs, IReadOnlyList<ExcelMappedLoad> mapped)
    {
        if (inputs.Any(x => !double.IsFinite(x.Amps) || x.Amps < 0 || x.Count <= 0)
            || mapped.Any(x => !double.IsFinite(x.Amps) || x.Amps < 0 || x.Count <= 0))
            return false;
        var inputGroups = inputs.GroupBy(x => x.Amps).ToDictionary(x => x.Key, x => x.Sum(y => y.Count));
        var mappedGroups = mapped.GroupBy(x => x.Amps).ToDictionary(x => x.Key, x => x.Sum(y => y.Count));
        return inputGroups.Count == mappedGroups.Count && inputGroups.All(x =>
            mappedGroups.Any(y => Math.Abs(x.Key - y.Key) <= 1e-9 && x.Value == y.Value));
    }
}
