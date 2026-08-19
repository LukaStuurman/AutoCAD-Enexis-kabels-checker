using System.IO;
using System.Text.Json;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed record SavedStationSnapshot(
    string Name,
    KaderVersion KaderVersion,
    IReadOnlyList<DirectionState> Directions);

internal sealed class StationPersistence
{
    public static StationPersistence Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    private StationPersistence()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EnexisKabelChecker");
        _filePath = Path.Combine(folder, "stations.json");
    }

    public IReadOnlyList<string> Names =>
        ReadFile().Stations
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public bool Save(
        string name,
        KaderVersion kaderVersion,
        IEnumerable<DirectionState> directions)
    {
        var trimmedName = ValidateName(name);
        var directionArray = directions.OrderBy(x => x.Number).ToArray();
        if (directionArray.Length == 0)
            throw new InvalidOperationException("Sla eerst minimaal één richting op voordat je een station opslaat.");

        var file = ReadFile();
        var existingIndex = file.Stations.FindIndex(x =>
            string.Equals(x.Name?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase));

        var station = new StationDto
        {
            Name = trimmedName,
            KaderVersion = kaderVersion,
            Directions = directionArray.Select(ToDto).ToList()
        };

        var replaced = existingIndex >= 0;
        if (replaced)
            file.Stations[existingIndex] = station;
        else
            file.Stations.Add(station);

        WriteFile(file);
        return replaced;
    }

    public SavedStationSnapshot? Load(string name)
    {
        var trimmedName = ValidateName(name);
        var station = ReadFile().Stations.FirstOrDefault(x =>
            string.Equals(x.Name?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase));
        if (station is null)
            return null;

        var directions = station.Directions
            .OrderBy(x => x.Number)
            .Select(FromDto)
            .ToArray();

        return new SavedStationSnapshot(
            station.Name,
            station.KaderVersion,
            directions);
    }

    public bool Delete(string name)
    {
        var trimmedName = ValidateName(name);
        var file = ReadFile();
        var removed = file.Stations.RemoveAll(x =>
            string.Equals(x.Name?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
            WriteFile(file);
        return removed;
    }

    public void ReplaceCurrentDirections(DirectionStore store, IEnumerable<DirectionState> directions)
    {
        foreach (var existing in store.Directions.Select(x => x.Number).ToArray())
            store.Delete(existing);

        foreach (var direction in directions.OrderBy(x => x.Number))
        {
            store.Save(
                direction.Number,
                null,
                direction.Profile,
                direction.Segments,
                direction.CurrentLoads,
                direction.ExcelLoads);
        }
    }

    private StationFileDto ReadFile()
    {
        if (!File.Exists(_filePath))
            return new StationFileDto();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<StationFileDto>(json, JsonOptions) ?? new StationFileDto();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Het opgeslagen stationsbestand is beschadigd: {_filePath}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Het opgeslagen stationsbestand kon niet worden gelezen: {_filePath}", ex);
        }
    }

    private void WriteFile(StationFileDto file)
    {
        try
        {
            var folder = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(folder);
            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Het station kon niet worden opgeslagen in: {_filePath}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Geen schrijfrechten voor het stationsbestand: {_filePath}", ex);
        }
    }

    private static string ValidateName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Vul eerst een stationsnaam in.");
        if (trimmed.Length > 120)
            throw new InvalidOperationException("Stationsnaam mag maximaal 120 tekens lang zijn.");
        return trimmed;
    }

    private static DirectionDto ToDto(DirectionState direction) => new()
    {
        Number = direction.Number,
        Profile = direction.Profile,
        Segments = direction.Segments
            .Select(x => new SegmentDto { CableName = x.CableName, LengthMeters = x.LengthMeters })
            .ToList(),
        CurrentLoads = direction.CurrentLoads
            .Select(x => new CurrentLoadDto { Amps = x.Amps, Count = x.Count })
            .ToList(),
        ExcelLoads = direction.ExcelLoads
            .Select(x => new ExcelLoadDto { ExcelLoadKey = x.ExcelLoadKey, Amps = x.Amps, Count = x.Count })
            .ToList()
    };

    private static DirectionState FromDto(DirectionDto direction) => new(
        direction.Number,
        direction.Profile,
        direction.Segments
            .Select(x => new CableSegment(x.CableName, x.LengthMeters))
            .ToArray(),
        direction.CurrentLoads
            .Select(x => new CurrentLoadInput(x.Amps, x.Count))
            .ToArray(),
        direction.ExcelLoads
            .Select(x => new ExcelMappedLoad(x.ExcelLoadKey, x.Amps, x.Count))
            .ToArray());

    private sealed class StationFileDto
    {
        public List<StationDto> Stations { get; set; } = new();
    }

    private sealed class StationDto
    {
        public string Name { get; set; } = string.Empty;
        public KaderVersion KaderVersion { get; set; } = KaderVersion.K2026_3_2;
        public List<DirectionDto> Directions { get; set; } = new();
    }

    private sealed class DirectionDto
    {
        public int Number { get; set; }
        public LoadProfile Profile { get; set; }
        public List<SegmentDto> Segments { get; set; } = new();
        public List<CurrentLoadDto> CurrentLoads { get; set; } = new();
        public List<ExcelLoadDto> ExcelLoads { get; set; } = new();
    }

    private sealed class SegmentDto
    {
        public string CableName { get; set; } = string.Empty;
        public double LengthMeters { get; set; }
    }

    private sealed class CurrentLoadDto
    {
        public double Amps { get; set; }
        public int Count { get; set; }
    }

    private sealed class ExcelLoadDto
    {
        public string ExcelLoadKey { get; set; } = string.Empty;
        public double Amps { get; set; }
        public int Count { get; set; }
    }
}
