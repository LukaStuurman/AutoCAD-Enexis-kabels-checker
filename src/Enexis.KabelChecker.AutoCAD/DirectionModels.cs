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
    private int _nextDirectionNumber = 1;

    public IReadOnlyList<DirectionState> Directions => _directions
        .OrderBy(x => x.Number)
        .ToArray();

    public DirectionState? Get(int number) =>
        _directions.FirstOrDefault(x => x.Number == number);

    public DirectionState Save(
        int? existingNumber,
        LoadProfile profile,
        IEnumerable<CableSegment> segments,
        IEnumerable<CurrentLoadInput> currentLoads,
        IEnumerable<ExcelMappedLoad> excelLoads)
    {
        var number = existingNumber ?? _nextDirectionNumber++;
        if (existingNumber is int existing)
            _nextDirectionNumber = Math.Max(_nextDirectionNumber, existing + 1);

        var state = new DirectionState(
            number,
            profile,
            segments.Select(x => new CableSegment(x.CableName, x.LengthMeters)).ToArray(),
            currentLoads.Select(x => new CurrentLoadInput(x.Amps, x.Count)).ToArray(),
            excelLoads.Select(x => new ExcelMappedLoad(x.ExcelLoadKey, x.Amps, x.Count)).ToArray());

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
