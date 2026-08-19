using System.IO;
using ClosedXML.Excel;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal static class ExcelDirectionExporter
{
    private const string CableSheetName = "Ontwerpstroom_kabel";
    private const string TransformerSheetName = "Ontwerpstroom_trafo";
    private const string EvenredigControlSheetName = "Controle_kabel_evenredig";
    private const string LastHalfControlSheetName = "Controle_kabel_laatste_helft";

    private const int LegacyCountColumn = 1;              // A
    private const int LegacyControlCableNameColumn = 2;   // B
    private const int LegacyControlLengthColumn = 17;     // Q
    private const int V32CountColumn = 2;                 // B
    private const int V32CountFirstRow = 5;
    private const int V32CountLastRow = 79;
    private const int V32ControlCableNameColumn = 15;     // O
    private const int V32ControlLengthColumn = 30;        // AD

    public static void Export(string outputPath, IReadOnlyList<DirectionState> directions)
    {
        if (directions.Count == 0)
            throw new InvalidOperationException("Sla eerst minimaal één richting op.");

        var owner = Form.ActiveForm;
        var version = KaderVersionSelection.SelectForExport(owner);
        var resolvedDirections = ResolveForVersion(owner, directions, version);
        var definition = KaderVersions.Get(version);

        using var templateStream = OpenEmbeddedTemplate(definition.ResourceFileName);
        using var workbook = new XLWorkbook(templateStream);

        if (version == KaderVersion.K2026_3_2)
            Export2026(workbook, resolvedDirections, version);
        else
            ExportLegacy(workbook, resolvedDirections, version);

        workbook.SaveAs(outputPath);
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

    private static void ExportLegacy(
        XLWorkbook workbook,
        IReadOnlyList<DirectionState> directions,
        KaderVersion version)
    {
        ValidateLegacyTemplates(workbook);

        var cableTemplate = workbook.Worksheet(CableSheetName);
        var transformer = workbook.Worksheet(TransformerSheetName);
        var evenredigTemplate = workbook.Worksheet(EvenredigControlSheetName);
        var lastHalfTemplate = workbook.Worksheet(LastHalfControlSheetName);
        var (controlFirstRow, controlLastRow) = version == KaderVersion.K2024_1_0
            ? (17, 34)
            : (18, 37);

        // Maak eerst alle invoervelden van het bronkader schoon. Daardoor kunnen
        // voorbeeldwaarden uit de aangeleverde Excel nooit in een export blijven staan.
        ClearLegacyCounts(cableTemplate, version);
        ClearLegacyCounts(transformer, version);
        if (version == KaderVersion.K2024_1_0)
        {
            // In het 2024-bronbestand staat nog een voorbeeldwaarde 50 in G18 van
            // Ontwerpstroom_trafo. Dit is een invoerveld en moet in iedere export leeg zijn.
            transformer.Cell("G18").Clear(XLClearOptions.Contents);
        }
        ClearControlCableLengths(evenredigTemplate, controlFirstRow, controlLastRow, LegacyControlLengthColumn);
        ClearControlCableLengths(lastHalfTemplate, controlFirstRow, controlLastRow, LegacyControlLengthColumn);

        foreach (var direction in directions.OrderBy(x => x.Number))
        {
            var cableSheet = cableTemplate.CopyTo(BuildDirectionCableSheetName(direction.Number));
            WriteLegacyDirectionCounts(cableSheet, direction.ExcelLoads, version);

            var controlTemplate = direction.Profile == LoadProfile.Evenredig
                ? evenredigTemplate
                : lastHalfTemplate;
            var controlSheet = controlTemplate.CopyTo(BuildDirectionControlSheetName(direction));
            WriteControlCableLengths(
                controlSheet,
                direction.Segments,
                controlFirstRow,
                controlLastRow,
                LegacyControlCableNameColumn,
                LegacyControlLengthColumn);
        }

        cableTemplate.Delete();
        evenredigTemplate.Delete();
        lastHalfTemplate.Delete();

        var totals = directions
            .SelectMany(x => x.ExcelLoads)
            .GroupBy(x => x.ExcelLoadKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count), StringComparer.OrdinalIgnoreCase);
        WriteLegacyCounts(transformer, totals, version);
    }

    private static void Export2026(
        XLWorkbook workbook,
        IReadOnlyList<DirectionState> directions,
        KaderVersion version)
    {
        Validate2026Templates(workbook);

        // Kader 3.2 bevat twaalf vaste richtingstabbladen. Maak alle invoervelden
        // in alle twaalf bladen schoon, ook wanneer een richting niet wordt gebruikt.
        for (var number = 1; number <= 12; number++)
        {
            var sheet = workbook.Worksheet($"({number})");
            Clear2026Counts(sheet);
            ClearControlCableLengths(sheet, 18, 36, V32ControlLengthColumn);
            ClearControlCableLengths(sheet, 64, 82, V32ControlLengthColumn);
        }

        foreach (var direction in directions.OrderBy(x => x.Number))
        {
            var sheet = workbook.Worksheet($"({direction.Number})");
            Write2026Counts(sheet, direction.ExcelLoads, version);

            if (direction.Profile == LoadProfile.Evenredig)
            {
                WriteControlCableLengths(
                    sheet,
                    direction.Segments,
                    18,
                    36,
                    V32ControlCableNameColumn,
                    V32ControlLengthColumn);
            }
            else
            {
                WriteControlCableLengths(
                    sheet,
                    direction.Segments,
                    64,
                    82,
                    V32ControlCableNameColumn,
                    V32ControlLengthColumn);
            }
        }

        // In kader 3.2 wordt het tabblad Transformator volledig door formules gevoed
        // vanuit de aantallen in B op de richtingstabbladen (1) t/m (12). Daarom
        // schrijft of wist de plugin bewust niets in Transformator.
    }

    private static Stream OpenEmbeddedTemplate(string embeddedTemplateFileName)
    {
        var assembly = typeof(ExcelDirectionExporter).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(embeddedTemplateFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException($"Het ingebouwde Enexis Excel-template '{embeddedTemplateFileName}' kon niet worden gevonden in de plugin.");

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Het ingebouwde Enexis Excel-template '{embeddedTemplateFileName}' kon niet worden geopend.");
    }

    private static void ValidateLegacyTemplates(XLWorkbook workbook)
    {
        var required = new[]
        {
            CableSheetName,
            TransformerSheetName,
            EvenredigControlSheetName,
            LastHalfControlSheetName
        };

        var missing = required.Where(x => !workbook.Worksheets.Contains(x)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Het Excel-template mist: " + string.Join(", ", missing.Select(x => $"'{x}'")) + ".");
    }

    private static void Validate2026Templates(XLWorkbook workbook)
    {
        var missing = Enumerable.Range(1, 12)
            .Select(number => $"({number})")
            .Where(name => !workbook.Worksheets.Contains(name))
            .ToList();
        if (!workbook.Worksheets.Contains("Transformator"))
            missing.Add("Transformator");

        if (missing.Count > 0)
            throw new InvalidOperationException("Kader 3.2 mist: " + string.Join(", ", missing.Select(x => $"'{x}'")) + ".");
    }

    private static string BuildDirectionCableSheetName(int directionNumber)
    {
        var name = $"Ontwerpstroom_kabel R{directionNumber}";
        return name.Length <= 31 ? name : $"Kabel R{directionNumber}";
    }

    private static string BuildDirectionControlSheetName(DirectionState direction) =>
        direction.Profile == LoadProfile.Evenredig
            ? BuildSafeSheetName($"Controle_evenredig R{direction.Number}", $"Ctrl 50% R{direction.Number}")
            : BuildSafeSheetName($"Controle_laatste_helft R{direction.Number}", $"Ctrl 75% R{direction.Number}");

    private static string BuildSafeSheetName(string preferred, string fallback) =>
        preferred.Length <= 31 ? preferred : fallback;

    private static void ClearLegacyCounts(IXLWorksheet sheet, KaderVersion version)
    {
        var options = ExcelLoadCatalog.For(version);
        if (options.Count == 0)
            return;

        // Kolom A is in de oude kaders de invoerkolom voor aantallen. Wis het
        // volledige invoergebied tussen de eerste en laatste bekende belastingrij,
        // zodat ook eventuele voorbeeldwaarden op tussenliggende invoerrijen verdwijnen.
        var firstRow = options.Min(x => x.Row);
        var lastRow = options.Max(x => x.Row);
        for (var row = firstRow; row <= lastRow; row++)
            sheet.Cell(row, LegacyCountColumn).Clear(XLClearOptions.Contents);
    }

    private static void WriteLegacyDirectionCounts(
        IXLWorksheet sheet,
        IReadOnlyList<ExcelMappedLoad> loads,
        KaderVersion version)
    {
        var counts = loads
            .GroupBy(x => x.ExcelLoadKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count), StringComparer.OrdinalIgnoreCase);
        WriteLegacyCounts(sheet, counts, version);
    }

    private static void WriteLegacyCounts(
        IXLWorksheet sheet,
        IReadOnlyDictionary<string, int> counts,
        KaderVersion version)
    {
        foreach (var pair in counts)
        {
            var option = ExcelLoadCatalog.FindByKey(version, pair.Key);
            if (option is null)
                throw new InvalidOperationException($"Onbekende Excel-belastingcode voor {KaderVersions.Get(version).DisplayName}: {pair.Key}.");

            sheet.Cell(option.Row, LegacyCountColumn).Value = pair.Value;
        }
    }

    private static void Clear2026Counts(IXLWorksheet sheet)
    {
        // In 3.2 is B5:B79 het invoergebied voor aantallen/eenheden. Dit wordt
        // volledig leeggemaakt; de vaste teksten, ontwerpstromen en formules staan elders.
        for (var row = V32CountFirstRow; row <= V32CountLastRow; row++)
            sheet.Cell(row, V32CountColumn).Clear(XLClearOptions.Contents);
    }

    private static void Write2026Counts(
        IXLWorksheet sheet,
        IReadOnlyList<ExcelMappedLoad> loads,
        KaderVersion version)
    {
        var counts = loads
            .GroupBy(x => x.ExcelLoadKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count), StringComparer.OrdinalIgnoreCase);

        foreach (var pair in counts)
        {
            var option = ExcelLoadCatalog.FindByKey(version, pair.Key);
            if (option is null)
                throw new InvalidOperationException($"Onbekende Excel-belastingcode voor kader 3.2: {pair.Key}.");

            sheet.Cell(option.Row, V32CountColumn).Value = pair.Value;
        }
    }

    private static void ClearControlCableLengths(
        IXLWorksheet sheet,
        int firstRow,
        int lastRow,
        int lengthColumn)
    {
        // Alleen inhoud wissen: opmaak, kleuren, validatie en formules buiten het
        // kabel-invoergebied blijven intact.
        for (var row = firstRow; row <= lastRow; row++)
            sheet.Cell(row, lengthColumn).Clear(XLClearOptions.Contents);
    }

    private static void WriteControlCableLengths(
        IXLWorksheet sheet,
        IReadOnlyList<CableSegment> segments,
        int firstRow,
        int lastRow,
        int cableNameColumn,
        int lengthColumn)
    {
        var lengths = segments
            .GroupBy(x => x.CableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => Math.Round(x.Sum(y => y.LengthMeters), 2, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in lengths)
        {
            var row = FindControlCableRow(sheet, pair.Key, firstRow, lastRow, cableNameColumn);
            if (row is null)
            {
                throw new InvalidOperationException(
                    $"Kabeltype '{pair.Key}' uit richtinggegevens is niet gevonden in tabblad '{sheet.Name}'.");
            }

            sheet.Cell(row.Value, lengthColumn).Value = pair.Value;
            sheet.Cell(row.Value, lengthColumn).Style.NumberFormat.Format = "0.00";
        }
    }

    private static int? FindControlCableRow(
        IXLWorksheet sheet,
        string cableName,
        int firstRow,
        int lastRow,
        int cableNameColumn)
    {
        for (var row = firstRow; row <= lastRow; row++)
        {
            var name = sheet.Cell(row, cableNameColumn).GetString().Trim();
            if (name.Equals(cableName, StringComparison.OrdinalIgnoreCase))
                return row;
        }

        return null;
    }
}
