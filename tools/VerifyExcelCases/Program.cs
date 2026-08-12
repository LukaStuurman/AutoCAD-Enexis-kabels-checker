using Enexis.KabelChecker.Core;

var engine = new KabelCheckerEngine();

Check(
    "Controle_kabel_evenredig voorbeeld",
    new[]
    {
        new CableSegment("4*150mm2 Al", 209.09),
        new CableSegment("4*95mm2 Al", 68.59)
    },
    LoadProfile.Evenredig,
    expectedFuse: 200,
    expectedDesignCurrent: 180,
    expectedZ: 0.06868816869589478);

Check(
    "Controle_kabel_laatste_helft voorbeeld",
    new[]
    {
        new CableSegment("4*150mm2 Al", 204.0),
        new CableSegment("4*120mm2 Al", 127.0)
    },
    LoadProfile.LaatsteHelft,
    expectedFuse: 160,
    expectedDesignCurrent: 144,
    expectedZ: 0.08207385655615314);

Console.WriteLine("Alle Excel-referentiecontroles zijn geslaagd.");
return;

void Check(
    string name,
    IReadOnlyList<CableSegment> segments,
    LoadProfile profile,
    int expectedFuse,
    int expectedDesignCurrent,
    double expectedZ)
{
    var result = engine.Calculate(segments, profile);

    if (result.FuseAmps != expectedFuse)
        throw new InvalidOperationException($"{name}: gG verwacht {expectedFuse}, berekend {result.FuseAmps?.ToString() ?? "geen"}.");

    if (result.MaxDesignCurrentAmps != expectedDesignCurrent)
        throw new InvalidOperationException($"{name}: ontwerpstroom verwacht {expectedDesignCurrent}, berekend {result.MaxDesignCurrentAmps?.ToString() ?? "geen"}.");

    if (Math.Abs(result.TotalImpedanceOhm - expectedZ) > 1e-12)
        throw new InvalidOperationException(
            $"{name}: Z verwacht {expectedZ:R}, berekend {result.TotalImpedanceOhm:R}.");

    Console.WriteLine(
        $"OK - {name}: {result.FuseAmps} A gG / {result.MaxDesignCurrentAmps} A / Z={result.TotalImpedanceOhm:0.000000} Ω");
}
