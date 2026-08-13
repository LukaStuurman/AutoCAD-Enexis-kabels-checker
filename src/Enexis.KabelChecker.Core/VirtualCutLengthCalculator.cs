namespace Enexis.KabelChecker.Core;

public enum VirtualCutSide
{
    Start,
    End
}

public sealed record VirtualCutLengthResult(
    double Length,
    VirtualCutSide Side);

public static class VirtualCutLengthCalculator
{
    public static VirtualCutLengthResult SelectLength(
        double totalLength,
        double cutDistanceFromStart,
        double sidePickDistanceFromStart)
    {
        if (totalLength <= 0 || double.IsNaN(totalLength) || double.IsInfinity(totalLength))
            throw new ArgumentOutOfRangeException(nameof(totalLength), "Totale polylinelengte moet groter dan 0 zijn.");

        if (cutDistanceFromStart < 0 || cutDistanceFromStart > totalLength ||
            double.IsNaN(cutDistanceFromStart) || double.IsInfinity(cutDistanceFromStart))
        {
            throw new ArgumentOutOfRangeException(nameof(cutDistanceFromStart), "Knippunt ligt buiten de polyline.");
        }

        if (sidePickDistanceFromStart < 0 || sidePickDistanceFromStart > totalLength ||
            double.IsNaN(sidePickDistanceFromStart) || double.IsInfinity(sidePickDistanceFromStart))
        {
            throw new ArgumentOutOfRangeException(nameof(sidePickDistanceFromStart), "Zijdekeuze ligt buiten de polyline.");
        }

        var epsilon = Math.Max(totalLength * 1e-9, 1e-9);
        if (cutDistanceFromStart <= epsilon || totalLength - cutDistanceFromStart <= epsilon)
            throw new InvalidOperationException("Kies een knippunt dat niet op het begin- of eindpunt van de polyline ligt.");

        if (Math.Abs(sidePickDistanceFromStart - cutDistanceFromStart) <= epsilon)
            throw new InvalidOperationException("Klik na het knippunt duidelijk op de zijde van de polyline die je wilt gebruiken.");

        return sidePickDistanceFromStart < cutDistanceFromStart
            ? new VirtualCutLengthResult(cutDistanceFromStart, VirtualCutSide.Start)
            : new VirtualCutLengthResult(totalLength - cutDistanceFromStart, VirtualCutSide.End);
    }
}
