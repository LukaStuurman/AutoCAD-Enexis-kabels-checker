using System.Text.Json;
using ClosedXML.Excel;

namespace Enexis.KabelChecker.ExcelWorker;

public static class ExcelWorker
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

    public static void Export(string outputPath, byte[] templateBytes, string requestJson)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Uitvoerpad ontbreekt.", nameof(outputPath));
        if (templateBytes is null || templateBytes.Length == 0)
            throw new ArgumentException("Excel-template ontbreekt.", nameof(templateBytes));

        var request = JsonSerializer.Deserialize<ExportRequest>(requestJson)
            ?? throw new InvalidOperationException("Excel-exportverzoek kon niet worden gelezen.");
        if (request.CurrentMode is not ("Automatisch" or "Verbruik" or "Opwek"))
            throw new InvalidOperationException("Onbekende stroombasis.");
        if (request.Directions.Count == 0)
            throw new InvalidOperationException("Sla eerst minimaal één richting op.");

        using var templateStream = new MemoryStream(templateBytes, writable: false);
        using var workbook = new XLWorkbook(templateStream);

        if (request.Layout.Equals("V30", StringComparison.OrdinalIgnoreCase)
            || request.Layout.Equals("V32", StringComparison.OrdinalIgnoreCase))
            Export2026(workbook, request);
        else if (request.Layout.Equals("Legacy2024", StringComparison.OrdinalIgnoreCase)
                 || request.Layout.Equals("Legacy2025", StringComparison.OrdinalIgnoreCase))
            ExportLegacy(workbook, request);
        else
            throw new InvalidOperationException($"Onbekende Excel-layout: {request.Layout}.");

        workbook.SaveAs(outputPath);
    }

    private static void ExportLegacy(XLWorkbook workbook, ExportRequest request)
    {
        ValidateLegacyTemplates(workbook);

        var cableTemplate = workbook.Worksheet(CableSheetName);
        var transformer = workbook.Worksheet(TransformerSheetName);
        var evenredigTemplate = workbook.Worksheet(EvenredigControlSheetName);
        var lastHalfTemplate = workbook.Worksheet(LastHalfControlSheetName);
        var (controlFirstRow, controlLastRow) = request.Layout.Equals("Legacy2024", StringComparison.OrdinalIgnoreCase)
            ? (17, 34)
            : (18, 37);

        ClearLegacyCounts(cableTemplate, request.CountFirstRow, request.CountLastRow);
        ClearLegacyCounts(transformer, request.CountFirstRow, request.CountLastRow);
        ClearControlCableLengths(evenredigTemplate, controlFirstRow, controlLastRow, LegacyControlLengthColumn);
        ClearControlCableLengths(lastHalfTemplate, controlFirstRow, controlLastRow, LegacyControlLengthColumn);

        foreach (var direction in request.Directions.OrderBy(x => x.Number))
        {
            var cableSheet = cableTemplate.CopyTo(BuildDirectionCableSheetName(direction.Number));
            WriteLegacyCounts(cableSheet, direction.Loads);
            var totalRow = request.Layout.Equals("Legacy2024", StringComparison.OrdinalIgnoreCase) ? 38 : 45;
            SetCableTotal(cableSheet.Cell(totalRow, 3), request.CurrentMode,
                $"SUM(E{request.CountFirstRow}:E{request.CountLastRow})", $"SUM(F{request.CountFirstRow}:F{request.CountLastRow})");

            var controlTemplate = direction.Evenredig ? evenredigTemplate : lastHalfTemplate;
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

        var totals = request.Directions
            .SelectMany(x => x.Loads)
            .GroupBy(x => x.StationRow > 0 ? x.StationRow : x.Row)
            .Select(x => new RowCount(x.Key, x.Sum(y => y.Count)))
            .ToArray();
        WriteLegacyCounts(transformer, totals);
    }

    private static void Export2026(XLWorkbook workbook, ExportRequest request)
    {
        Validate2026Templates(workbook, request.Layout);
        var isV30 = request.Layout.Equals("V30", StringComparison.OrdinalIgnoreCase);
        var countLastRow = isV30 ? 75 : V32CountLastRow;
        var lastHalfFirstRow = isV30 ? 62 : 64;
        var lastHalfLastRow = isV30 ? 80 : 82;

        for (var number = 1; number <= 12; number++)
        {
            var sheet = workbook.Worksheet($"({number})");
            Clear2026Counts(sheet, countLastRow);
            SetCableTotal(sheet.Cell(isV30 ? 77 : 81, 4), request.CurrentMode,
                $"SUM(H5:H{countLastRow})", $"SUM(I5:I{countLastRow})");
            ClearControlCableLengths(sheet, 18, 36, V32ControlLengthColumn);
            ClearControlCableLengths(sheet, lastHalfFirstRow, lastHalfLastRow, V32ControlLengthColumn);
        }

        // Restore the station's live direction links, including templates with flattened zero values.
        // 3.2 has two extra cable rows before the shared station rows.
        var transformer = workbook.Worksheet("Transformator");
        // The 3.2 source has two power sums pointing at an external copy of these direction sheets.
        foreach (var cell in transformer.CellsUsed().Where(x => x.HasFormula && x.FormulaA1.Contains("'[1](")))
            cell.FormulaA1 = cell.FormulaA1.Replace("'[1](", "'(");
        var stationLastRow = isV30 ? 75 : 77;
        for (var row = 5; row <= stationLastRow; row++)
        {
            var cableRow = !isV30 && row >= 43 ? row + 2 : row;
            var source = workbook.Worksheet("(1)");
            if (source.Cell(cableRow, 6).DataType != XLDataType.Number
                && source.Cell(cableRow, 7).DataType != XLDataType.Number) continue;
            transformer.Cell(row, 2).FormulaA1 = string.Join("+", Enumerable.Range(1, 12).Select(n => $"'({n})'!B{cableRow}"));
        }

        foreach (var direction in request.Directions.OrderBy(x => x.Number))
        {
            var sheet = workbook.Worksheet($"({direction.Number})");
            Write2026Counts(sheet, direction.Loads);

            if (direction.Evenredig)
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
                    lastHalfFirstRow,
                    lastHalfLastRow,
                    V32ControlCableNameColumn,
                    V32ControlLengthColumn);
            }
        }
    }

    private static void SetCableTotal(IXLCell cell, string mode, string consumption, string generation) =>
        cell.FormulaA1 = mode switch
        {
            "Verbruik" => consumption,
            "Opwek" => generation,
            _ => $"MAX({consumption},{generation})"
        };

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

    private static void Validate2026Templates(XLWorkbook workbook, string layout)
    {
        var missing = Enumerable.Range(1, 12)
            .Select(number => $"({number})")
            .Where(name => !workbook.Worksheets.Contains(name))
            .ToList();
        if (!workbook.Worksheets.Contains("Transformator"))
            missing.Add("Transformator");

        if (missing.Count > 0)
            throw new InvalidOperationException($"Kader {(layout.Equals("V30", StringComparison.OrdinalIgnoreCase) ? "3.0" : "3.2")} mist: " + string.Join(", ", missing.Select(x => $"'{x}'")) + ".");
    }

    private static string BuildDirectionCableSheetName(int directionNumber)
    {
        var name = $"Ontwerpstroom_kabel R{directionNumber}";
        return name.Length <= 31 ? name : $"Kabel R{directionNumber}";
    }

    private static string BuildDirectionControlSheetName(DirectionRequest direction) =>
        direction.Evenredig
            ? BuildSafeSheetName($"Controle_evenredig R{direction.Number}", $"Ctrl 50% R{direction.Number}")
            : BuildSafeSheetName($"Controle_laatste_helft R{direction.Number}", $"Ctrl 75% R{direction.Number}");

    private static string BuildSafeSheetName(string preferred, string fallback) =>
        preferred.Length <= 31 ? preferred : fallback;

    private static void ClearLegacyCounts(IXLWorksheet sheet, int firstRow, int lastRow)
    {
        if (firstRow <= 0 || lastRow < firstRow)
            return;

        for (var row = firstRow; row <= lastRow; row++)
            sheet.Cell(row, LegacyCountColumn).Clear(XLClearOptions.Contents);
    }

    private static void WriteLegacyCounts(IXLWorksheet sheet, IReadOnlyList<RowCount> loads)
    {
        foreach (var item in loads)
            sheet.Cell(item.Row, LegacyCountColumn).Value = item.Count;
    }

    private static void Clear2026Counts(IXLWorksheet sheet, int lastRow)
    {
        for (var row = V32CountFirstRow; row <= lastRow; row++)
            sheet.Cell(row, V32CountColumn).Clear(XLClearOptions.Contents);
    }

    private static void Write2026Counts(IXLWorksheet sheet, IReadOnlyList<RowCount> loads)
    {
        foreach (var item in loads)
            sheet.Cell(item.Row, V32CountColumn).Value = item.Count;
    }

    private static void ClearControlCableLengths(
        IXLWorksheet sheet,
        int firstRow,
        int lastRow,
        int lengthColumn)
    {
        for (var row = firstRow; row <= lastRow; row++)
            sheet.Cell(row, lengthColumn).Clear(XLClearOptions.Contents);
    }

    private static void WriteControlCableLengths(
        IXLWorksheet sheet,
        IReadOnlyList<SegmentRequest> segments,
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

    private sealed record ExportRequest(
        string Layout,
        int CountFirstRow,
        int CountLastRow,
        List<DirectionRequest> Directions,
        string CurrentMode = "Automatisch");

    private sealed record DirectionRequest(
        int Number,
        bool Evenredig,
        List<RowCount> Loads,
        List<SegmentRequest> Segments);

    private sealed record RowCount(int Row, int Count, int StationRow = 0);

    private sealed record SegmentRequest(string CableName, double LengthMeters);
}
