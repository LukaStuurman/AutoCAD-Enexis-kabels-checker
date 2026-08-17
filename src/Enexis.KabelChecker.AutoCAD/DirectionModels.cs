using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed record CurrentLoadInput(double Amps, int Count);

internal sealed record ExcelMappedLoad(
    string ExcelLoadKey,
    double Amps,
    int Count);

internal sealed record DirectionState(
    int Number,
    LoadProfile Profile,
    IReadOnlyList<CableSegment> Segments,
    IReadOnlyList<CurrentLoadInput> CurrentLoads,
    IReadOnlyList<ExcelMappedLoad> ExcelLoads);

internal sealed class DirectionStore
{
    public static DirectionStore Instance { get; } = new();

    private readonly List<DirectionState> _directions = new();

    public IReadOnlyList<DirectionState> Directions => _directions
        .OrderBy(x => x.Number)
        .ToArray();

    public DirectionState? Get(int number) =>
        _directions.FirstOrDefault(x => x.Number == number);

    public int FirstAvailableNumber() =>
        Enumerable.Range(1, 12).FirstOrDefault(number => Get(number) is null) is var number && number > 0
            ? number
            : 1;

    public DirectionState Save(
        int number,
        int? existingNumber,
        LoadProfile profile,
        IEnumerable<CableSegment> segments,
        IEnumerable<CurrentLoadInput> currentLoads,
        IEnumerable<ExcelMappedLoad> excelLoads)
    {
        if (number is < 1 or > 12)
            throw new InvalidOperationException("Richtingnummer moet tussen 1 en 12 liggen.");

        var collision = _directions.FirstOrDefault(x => x.Number == number && x.Number != existingNumber);
        if (collision is not null)
            throw new InvalidOperationException($"Richting {number} bestaat al. Kies een ander nummer of open die richting om hem te wijzigen.");

        var state = new DirectionState(
            number,
            profile,
            segments.Select(x => new CableSegment(x.CableName, x.LengthMeters)).ToArray(),
            currentLoads.Select(x => new CurrentLoadInput(x.Amps, x.Count)).ToArray(),
            excelLoads.Select(x => new ExcelMappedLoad(x.ExcelLoadKey, x.Amps, x.Count)).ToArray());

        if (existingNumber is int oldNumber && oldNumber != number)
        {
            var oldIndex = _directions.FindIndex(x => x.Number == oldNumber);
            if (oldIndex >= 0)
                _directions.RemoveAt(oldIndex);
        }

        var index = _directions.FindIndex(x => x.Number == number);
        if (index >= 0)
            _directions[index] = state;
        else
            _directions.Add(state);

        return state;
    }

    public bool Delete(int number)
    {
        var index = _directions.FindIndex(x => x.Number == number);
        if (index < 0)
            return false;

        _directions.RemoveAt(index);
        return true;
    }
}
