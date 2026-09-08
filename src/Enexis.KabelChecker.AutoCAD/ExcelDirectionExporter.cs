using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Text.Json;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal static class ExcelDirectionExporter
{
    public static void Export(string outputPath, IReadOnlyList<DirectionState> directions)
    {
        if (directions.Count == 0)
            throw new InvalidOperationException("Sla eerst minimaal één richting op.");

        var owner = Form.ActiveForm;
        var version = KaderVersionSelection.SelectForExport(owner);
        var resolvedDirections = ResolveForVersion(owner, directions, version);
        var definition = KaderVersions.Get(version);

        var templateBytes = ReadEmbeddedTemplate(definition.ResourceFileName);
        var requestJson = BuildWorkerRequest(resolvedDirections, version);
        IsolatedExcelWorker.Export(outputPath, templateBytes, requestJson);
    }

    private static IReadOnlyList<DirectionState> ResolveForVersion(
        IWin32Window? owner,
        IReadOnlyList<DirectionState> directions,
        KaderVersion version)
    {
        var result = new List<DirectionState>();
        foreach (var direction in directions.OrderBy(x => x.Number))
        {
            var mapped = ExcelLoadResolver.Resolve(owner, direction.CurrentLoads, version, direction.ExcelLoads);
            if (mapped is null)
                throw new OperationCanceledException("Excel-export geannuleerd tijdens het koppelen van de ontwerpstromen.");

            result.Add(direction with { ExcelLoads = mapped });
        }

        return result;
    }

    private static string BuildWorkerRequest(
        IReadOnlyList<DirectionState> directions,
        KaderVersion version)
    {
        var options = ExcelLoadCatalog.For(version);
        var firstRow = options.Count == 0 ? 0 : options.Min(x => x.Row);
        var lastRow = options.Count == 0 ? 0 : options.Max(x => x.Row);

        var workerDirections = directions
            .OrderBy(x => x.Number)
            .Select(direction => new WorkerDirectionRequest(
                direction.Number,
                direction.Profile == LoadProfile.Evenredig,
                direction.ExcelLoads
                    .GroupBy(x => x.ExcelLoadKey, StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var option = ExcelLoadCatalog.FindByKey(version, group.Key)
                            ?? throw new InvalidOperationException(
                                $"Onbekende Excel-belastingcode voor {KaderVersions.Get(version).DisplayName}: {group.Key}.");
                        return new WorkerRowCount(option.Row, group.Sum(x => x.Count));
                    })
                    .OrderBy(x => x.Row)
                    .ToArray(),
                direction.Segments
                    .Select(x => new WorkerSegmentRequest(x.CableName, x.LengthMeters))
                    .ToArray()))
            .ToArray();

        var layout = version switch
        {
            KaderVersion.K2024_1_0 => "Legacy2024",
            KaderVersion.K2025_2_0 => "Legacy2025",
            KaderVersion.K2026_3_2 => "V32",
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };

        return JsonSerializer.Serialize(new WorkerExportRequest(layout, firstRow, lastRow, workerDirections));
    }

    private static byte[] ReadEmbeddedTemplate(string embeddedTemplateFileName)
    {
        var assembly = typeof(ExcelDirectionExporter).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(embeddedTemplateFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException($"Het ingebouwde Enexis Excel-template '{embeddedTemplateFileName}' kon niet worden gevonden in de plugin.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Het ingebouwde Enexis Excel-template '{embeddedTemplateFileName}' kon niet worden geopend.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed record WorkerExportRequest(
        string Layout,
        int CountFirstRow,
        int CountLastRow,
        IReadOnlyList<WorkerDirectionRequest> Directions);

    private sealed record WorkerDirectionRequest(
        int Number,
        bool Evenredig,
        IReadOnlyList<WorkerRowCount> Loads,
        IReadOnlyList<WorkerSegmentRequest> Segments);

    private sealed record WorkerRowCount(int Row, int Count);

    private sealed record WorkerSegmentRequest(string CableName, double LengthMeters);
}

internal static class IsolatedExcelWorker
{
    private const string WorkerDirectoryName = "excel";
    private const string WorkerAssemblyName = "Enexis.KabelChecker.ExcelWorker.dll";
    private const string WorkerTypeName = "Enexis.KabelChecker.ExcelWorker.ExcelWorker";

    public static void Export(string outputPath, byte[] templateBytes, string requestJson)
    {
        var pluginAssemblyPath = typeof(IsolatedExcelWorker).Assembly.Location;
        var pluginDirectory = Path.GetDirectoryName(pluginAssemblyPath)
            ?? throw new InvalidOperationException("De installatiemap van de Enexis Kabel Checker kon niet worden bepaald.");
        var workerPath = Path.Combine(pluginDirectory, WorkerDirectoryName, WorkerAssemblyName);

        if (!File.Exists(workerPath))
        {
            throw new FileNotFoundException(
                "De geïsoleerde Excel-module ontbreekt. Installeer de volledige Enexis Kabel Checker release opnieuw.",
                workerPath);
        }

        var loadContext = new ExcelWorkerLoadContext(workerPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(workerPath);
            var type = assembly.GetType(WorkerTypeName, throwOnError: true)
                ?? throw new InvalidOperationException("De geïsoleerde Excel-module kon niet worden geopend.");
            var method = type.GetMethod(
                "Export",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(byte[]), typeof(string) },
                modifiers: null)
                ?? throw new MissingMethodException(WorkerTypeName, "Export");

            try
            {
                method.Invoke(null, new object[] { outputPath, templateBytes, requestJson });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private sealed class ExcelWorkerLoadContext : AssemblyLoadContext
    {
        private readonly string _workerDirectory;

        public ExcelWorkerLoadContext(string workerAssemblyPath)
            : base($"EnexisKabelChecker.Excel.{Guid.NewGuid():N}", isCollectible: true)
        {
            _workerDirectory = Path.GetDirectoryName(workerAssemblyPath)
                ?? throw new ArgumentException("Excel-workerpad heeft geen map.", nameof(workerAssemblyPath));
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName.Name))
                return null;

            var candidate = Path.Combine(_workerDirectory, assemblyName.Name + ".dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var candidate = Path.Combine(_workerDirectory, unmanagedDllName);
            if (!Path.HasExtension(candidate))
                candidate += ".dll";

            return File.Exists(candidate) ? LoadUnmanagedDllFromPath(candidate) : IntPtr.Zero;
        }
    }
}
