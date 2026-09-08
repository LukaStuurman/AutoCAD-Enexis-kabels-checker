using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Text.Json;
using ClosedXML.Excel;

if (args.Length != 1)
    throw new ArgumentException("Gebruik: VerifyExcelIsolation <pad-naar-Enexis.KabelChecker.ExcelWorker.dll>");

var workerPath = Path.GetFullPath(args[0]);
if (!File.Exists(workerPath))
    throw new FileNotFoundException("Excel-worker ontbreekt.", workerPath);

// Forceer bewust Autodesk-achtig gedrag: laad eerst een oudere ClosedXML-versie
// in de Default AssemblyLoadContext. De worker is tegen 0.105.1 gebouwd en moet
// ondanks deze reeds geladen 0.102.2-versie succesvol blijven werken.
var defaultClosedXmlVersion = typeof(XLWorkbook).Assembly.GetName().Version
    ?? throw new InvalidOperationException("ClosedXML assemblyversie ontbreekt.");
if (defaultClosedXmlVersion.Major != 0 || defaultClosedXmlVersion.Minor != 102)
    throw new InvalidOperationException($"Regressietest verwacht ClosedXML 0.102.x in Default ALC, gevonden {defaultClosedXmlVersion}.");

byte[] templateBytes;
using (var template = new XLWorkbook())
{
    template.AddWorksheet("Ontwerpstroom_kabel");
    template.AddWorksheet("Ontwerpstroom_trafo");
    template.AddWorksheet("Controle_kabel_evenredig");
    template.AddWorksheet("Controle_kabel_laatste_helft");

    using var stream = new MemoryStream();
    template.SaveAs(stream);
    templateBytes = stream.ToArray();
}

var requestJson = JsonSerializer.Serialize(new
{
    Layout = "Legacy2025",
    CountFirstRow = 5,
    CountLastRow = 40,
    Directions = new[]
    {
        new
        {
            Number = 1,
            Evenredig = true,
            Loads = new[] { new { Row = 20, Count = 2 } },
            Segments = Array.Empty<object>()
        }
    }
});

var outputPath = Path.Combine(Path.GetTempPath(), $"Enexis-ExcelIsolation-{Guid.NewGuid():N}.xlsx");
var context = new WorkerLoadContext(workerPath);
try
{
    var assembly = context.LoadFromAssemblyPath(workerPath);
    var workerClosedXml = context.Assemblies.FirstOrDefault(x => x.GetName().Name == "ClosedXML");

    var type = assembly.GetType("Enexis.KabelChecker.ExcelWorker.ExcelWorker", throwOnError: true)
        ?? throw new InvalidOperationException("ExcelWorker type ontbreekt.");
    var method = type.GetMethod(
        "Export",
        BindingFlags.Public | BindingFlags.Static,
        binder: null,
        types: new[] { typeof(string), typeof(byte[]), typeof(string) },
        modifiers: null)
        ?? throw new MissingMethodException(type.FullName, "Export");

    try
    {
        method.Invoke(null, new object[] { outputPath, templateBytes, requestJson });
    }
    catch (TargetInvocationException ex) when (ex.InnerException is not null)
    {
        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        throw;
    }

    workerClosedXml ??= context.Assemblies.FirstOrDefault(x => x.GetName().Name == "ClosedXML");
    var isolatedVersion = workerClosedXml?.GetName().Version
        ?? throw new InvalidOperationException("De worker heeft geen eigen ClosedXML geladen.");
    if (isolatedVersion.Major != 0 || isolatedVersion.Minor != 105)
        throw new InvalidOperationException($"Worker verwacht geïsoleerde ClosedXML 0.105.x, gevonden {isolatedVersion}.");

    if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        throw new InvalidOperationException("Geïsoleerde Excel-export heeft geen bestand gemaakt.");

    using var archive = ZipFile.OpenRead(outputPath);
    if (archive.GetEntry("xl/workbook.xml") is null)
        throw new InvalidOperationException("Uitvoer is geen geldige XLSX-package.");

    Console.WriteLine($"OK: Default ClosedXML {defaultClosedXmlVersion}; isolated worker ClosedXML {isolatedVersion}.");
}
finally
{
    context.Unload();
    if (File.Exists(outputPath))
        File.Delete(outputPath);
}

sealed class WorkerLoadContext : AssemblyLoadContext
{
    private readonly string _directory;

    public WorkerLoadContext(string workerAssemblyPath)
        : base($"VerifyExcelIsolation-{Guid.NewGuid():N}", isCollectible: true)
    {
        _directory = Path.GetDirectoryName(workerAssemblyPath)
            ?? throw new ArgumentException("Workerpad heeft geen map.", nameof(workerAssemblyPath));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name))
            return null;

        var candidate = Path.Combine(_directory, assemblyName.Name + ".dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
