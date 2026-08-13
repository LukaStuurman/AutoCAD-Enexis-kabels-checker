using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Enexis.KabelChecker.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed record TextCurrentObjectValue(
    ObjectId ObjectId,
    string SourceText,
    double Amps);

internal sealed record TextCurrentObjectSelectionResult(
    bool Cancelled,
    IReadOnlyList<TextCurrentObjectValue> Values,
    int InvalidTextObjects,
    string Message);

internal static class TextCurrentManualSelection
{
    private static readonly Regex MTextFormatCode = new(
        @"\\[A-Za-z][^;]*;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TextCurrentObjectSelectionResult Read()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return new TextCurrentObjectSelectionResult(
                true,
                Array.Empty<TextCurrentObjectValue>(),
                0,
                "Geen actieve tekening.");
        }

        var editor = doc.Editor;
        var database = doc.Database;
        using var documentLock = doc.LockDocument();

        var filter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.Start, "TEXT,MTEXT")
        });

        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\nSelecteer één of meer TEXT/MTEXT met stroomwaarden en druk Enter: ",
            MessageForRemoval = "\nVerwijder tekst uit de selectie: "
        };

        var selection = editor.GetSelection(options, filter);
        if (selection.Status != PromptStatus.OK)
        {
            return new TextCurrentObjectSelectionResult(
                true,
                Array.Empty<TextCurrentObjectValue>(),
                0,
                "Tekstselectie geannuleerd.");
        }

        var values = new List<TextCurrentObjectValue>();
        var invalidTextObjects = 0;
        var invalidExamples = new List<string>();

        using var transaction = database.TransactionManager.StartTransaction();
        foreach (var id in selection.Value.GetObjectIds())
        {
            var dbObject = transaction.GetObject(id, OpenMode.ForRead);
            string? sourceText = dbObject switch
            {
                MText mText => CleanMTextContents(mText.Contents),
                DBText dbText => dbText.TextString,
                _ => null
            };

            if (sourceText is null)
                continue;

            sourceText = sourceText.Trim();
            if (!CurrentTextParser.TryParseSingleCurrent(sourceText, out var amps))
            {
                invalidTextObjects++;
                if (invalidExamples.Count < 3)
                    invalidExamples.Add(sourceText);
                continue;
            }

            values.Add(new TextCurrentObjectValue(id, sourceText, amps));
        }
        transaction.Commit();

        var messages = new List<string>();
        messages.Add(values.Count > 0
            ? $"{values.Count} geldige stroomwaarde(n) gelezen uit TEXT/MTEXT."
            : "Geen eenduidige stroomwaarden gevonden in de geselecteerde TEXT/MTEXT-objecten.");

        if (invalidTextObjects > 0)
        {
            var exampleText = invalidExamples.Count > 0
                ? " Voorbeeld(en): " + string.Join(" | ", invalidExamples)
                : string.Empty;
            messages.Add($"{invalidTextObjects} tekstobject(en) overgeslagen omdat er niet precies één eenduidig getal in stond.{exampleText}");
        }

        return new TextCurrentObjectSelectionResult(
            false,
            values,
            invalidTextObjects,
            string.Join(Environment.NewLine, messages));
    }

    private static string CleanMTextContents(string contents)
    {
        if (string.IsNullOrEmpty(contents))
            return string.Empty;

        var cleaned = MTextFormatCode.Replace(contents, string.Empty);
        return cleaned
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Replace("\\P", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("\\~", " ", StringComparison.Ordinal);
    }
}
