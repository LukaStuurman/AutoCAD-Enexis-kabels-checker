using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;

internal static class ExcelWorkbookIntegrityCheck
{
    private const uint LocalFileHeaderSignature = 0x04034b50;

    public static void Run()
    {
        var templatePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "Enexis.KabelChecker.AutoCAD",
            "Resources",
            "Eea-0205.K_2.0.xlsx");

        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Excel-template voor integriteitstest ontbreekt.", templatePath);

        using var source = File.OpenRead(templatePath);
        using var recovered = RecoverTruncatedOpenXmlPackage(source);
        using var workbook = new XLWorkbook(recovered);

        var cableTemplate = workbook.Worksheet("Ontwerpstroom_kabel");
        var evenredigTemplate = workbook.Worksheet("Controle_kabel_evenredig");
        var lastHalfTemplate = workbook.Worksheet("Controle_kabel_laatste_helft");

        cableTemplate.CopyTo("Ontwerpstroom_kabel R2");
        evenredigTemplate.CopyTo("Controle_evenredig R2");
        cableTemplate.CopyTo("Ontwerpstroom_kabel R12");
        lastHalfTemplate.CopyTo("Controle_laatste_helft R12");

        cableTemplate.Delete();
        evenredigTemplate.Delete();
        lastHalfTemplate.Delete();

        var outputPath = Path.Combine(Path.GetTempPath(), $"enexis-integrity-{Guid.NewGuid():N}.xlsx");
        try
        {
            workbook.SaveAs(outputPath);
            using var reopened = new XLWorkbook(outputPath);
            var required = new[]
            {
                "Ontwerpstroom_kabel R2",
                "Controle_evenredig R2",
                "Ontwerpstroom_kabel R12",
                "Controle_laatste_helft R12"
            };

            foreach (var sheetName in required)
                if (!reopened.Worksheets.Contains(sheetName))
                    throw new InvalidOperationException($"Integriteitstest mist tabblad '{sheetName}'.");

            Console.WriteLine("OK - Excel exportintegriteit: beschadigde ZIP-index hersteld, R2/R12 gekopieerd, opgeslagen en opnieuw geopend.");
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    private static MemoryStream RecoverTruncatedOpenXmlPackage(Stream source)
    {
        using var input = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
        var output = new MemoryStream();
        var recoveredEntries = 0;

        using (var targetArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            while (source.Position + 30 <= source.Length)
            {
                var signature = input.ReadUInt32();
                if (signature != LocalFileHeaderSignature)
                    break;

                _ = input.ReadUInt16(); // version needed
                var flags = input.ReadUInt16();
                var compressionMethod = input.ReadUInt16();
                _ = input.ReadUInt16(); // time
                _ = input.ReadUInt16(); // date
                _ = input.ReadUInt32(); // CRC32
                var compressedSize = input.ReadUInt32();
                _ = input.ReadUInt32(); // uncompressed size
                var fileNameLength = input.ReadUInt16();
                var extraFieldLength = input.ReadUInt16();

                if ((flags & 0x0008) != 0)
                    throw new InvalidDataException("Excel-template gebruikt een ZIP data descriptor; automatisch herstel is daarvoor niet ondersteund.");
                if (source.Position + fileNameLength + extraFieldLength + compressedSize > source.Length)
                    throw new InvalidDataException("Excel-template is midden in een ZIP-entry afgebroken.");

                var fileNameBytes = input.ReadBytes(fileNameLength);
                var fileName = Encoding.UTF8.GetString(fileNameBytes);
                if (extraFieldLength > 0)
                    _ = input.ReadBytes(extraFieldLength);

                var compressedData = input.ReadBytes(checked((int)compressedSize));
                var targetEntry = targetArchive.CreateEntry(fileName, CompressionLevel.Optimal);
                if (fileName.EndsWith("/", StringComparison.Ordinal))
                {
                    recoveredEntries++;
                    continue;
                }

                using var targetStream = targetEntry.Open();
                if (compressionMethod == 0)
                {
                    targetStream.Write(compressedData);
                }
                else if (compressionMethod == 8)
                {
                    using var compressedStream = new MemoryStream(compressedData, writable: false);
                    using var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress);
                    deflate.CopyTo(targetStream);
                }
                else
                {
                    throw new InvalidDataException($"Niet-ondersteunde ZIP-compressiemethode {compressionMethod} in '{fileName}'.");
                }

                recoveredEntries++;
            }
        }

        if (recoveredEntries == 0)
            throw new InvalidDataException("Geen herstelbare bestanden in het ingebouwde Excel-template gevonden.");

        output.Position = 0;
        Console.WriteLine($"Excel-template: {recoveredEntries} ZIP-entry/entries hersteld uit lokale headers.");
        return output;
    }
}
