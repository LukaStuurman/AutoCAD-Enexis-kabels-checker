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

CheckCurrentText("25", 25.0);
CheckCurrentText("25,5", 25.5);
CheckCurrentText("25.5 A", 25.5);
CheckCurrentTextRejected("groep 1: 25,5 A");
CheckCurrentTextRejected("geen stroomwaarde");

CheckVirtualCut(100.0, 72.5, 20.0, 72.5, VirtualCutSide.Start);
CheckVirtualCut(100.0, 72.5, 90.0, 27.5, VirtualCutSide.End);

ExcelWorkbookIntegrityCheck.Run();

Console.WriteLine("Alle Excel-referentiecontroles, tekststroom-parsercontroles, virtuele-knipcontroles en Excel-integriteitscontroles zijn geslaagd.");
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

void CheckCurrentText(string text, double expected)
{
    if (!CurrentTextParser.TryParseSingleCurrent(text, out var actual))
        throw new InvalidOperationException($"Tekststroom '{text}' had gelezen moeten worden.");

    if (Math.Abs(actual - expected) > 1e-12)
        throw new InvalidOperationException($"Tekststroom '{text}': verwacht {expected}, gelezen {actual}.");

    Console.WriteLine($"OK - tekststroom '{text}' => {actual:0.##} A");
}

void CheckCurrentTextRejected(string text)
{
    if (CurrentTextParser.TryParseSingleCurrent(text, out var actual))
        throw new InvalidOperationException($"Tekst '{text}' had als ambigu/ongeldig geweigerd moeten worden, maar gaf {actual} A.");

    Console.WriteLine($"OK - ambigue/ongeldige tekst geweigerd: '{text}'");
}

void CheckVirtualCut(
    double totalLength,
    double cutDistance,
    double sidePickDistance,
    double expectedLength,
    VirtualCutSide expectedSide)
{
    var result = VirtualCutLengthCalculator.SelectLength(totalLength, cutDistance, sidePickDistance);
    if (Math.Abs(result.Length - expectedLength) > 1e-12 || result.Side != expectedSide)
    {
        throw new InvalidOperationException(
            $"Virtuele knip: verwacht {expectedLength} / {expectedSide}, berekend {result.Length} / {result.Side}.");
    }

    Console.WriteLine($"OK - virtuele knip => {result.Length:0.##} ({result.Side})");
}
