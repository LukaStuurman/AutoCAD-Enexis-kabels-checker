using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Enexis.KabelChecker.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(Enexis.KabelChecker.AutoCAD.PluginEntry))]
[assembly: CommandClass(typeof(Enexis.KabelChecker.AutoCAD.KabelCheckerCommands))]

namespace Enexis.KabelChecker.AutoCAD;

public sealed class PluginEntry : IExtensionApplication
{
    public void Initialize()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage("\nEnexis Kabel Checker geladen. Commando: ENEXISKABELCHECK");
    }

    public void Terminate()
    {
        KabelCheckerWindowManager.Close();
    }
}

public sealed class KabelCheckerCommands
{
    [CommandMethod("ENEXISKABELCHECK", CommandFlags.Modal)]
    public void OpenDirectionBuilder()
    {
        KabelCheckerWindowManager.Show();
    }

    [CommandMethod("ENEXISKABELCHECKSEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void OpenFromSelection()
    {
        var result = AutoCadSelectionReader.ReadCurrentDrawing();
        if (result.Cancelled)
            return;

        KabelCheckerWindowManager.Show(result.LengthsByCable, result.Message, replaceExisting: true);
    }
}

internal static class KabelCheckerWindowManager
{
    private static KabelCheckerForm? _form;

    public static void Show(
        IReadOnlyDictionary<string, double>? presetLengths = null,
        string? message = null,
        bool replaceExisting = false)
    {
        if (_form is { IsDisposed: false })
        {
            if (!replaceExisting)
            {
                if (_form.WindowState == FormWindowState.Minimized)
                    _form.WindowState = FormWindowState.Normal;

                _form.Show();
                _form.BringToFront();
                _form.Activate();
                return;
            }

            _form.Close();
            _form = null;
        }

        var form = new KabelCheckerForm(presetLengths, message);
        _form = form;
        form.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_form, form))
                _form = null;
        };

        AcApp.ShowModelessDialog(form);
    }

    public static void Close()
    {
        if (_form is not { IsDisposed: false })
        {
            _form = null;
            return;
        }

        _form.Close();
        _form = null;
    }
}

internal sealed record SelectionReadResult(
    bool Cancelled,
    IReadOnlyDictionary<string, double> LengthsByCable,
    string Message);

internal sealed record PolylinePickResult(
    bool Cancelled,
    double LengthMeters,
    string Message);

internal sealed record TextCurrentValue(
    string SourceText,
    double Amps);

internal sealed record TextCurrentSelectionResult(
    bool Cancelled,
    IReadOnlyList<TextCurrentValue> Values,
    int InvalidTextObjects,
    int NonTextObjects,
    string Message);

internal static class AutoCadSelectionReader
{
    private static readonly Regex MTextFormatCode = new(
        @"\\[A-Za-z][^;]*;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static PolylinePickResult PickSinglePolyline(Form modelessForm, string cableName)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return new PolylinePickResult(true, 0, "Geen actieve tekening.");

        var editor = doc.Editor;
        var database = doc.Database;

        // Een modeless venster draait buiten de normale command-context. Autodesk
        // adviseert daarom het actieve document te locken tijdens interactie vanuit
        // een modeless UI. De lock bestaat alleen zolang de polyline wordt gekozen
        // en de lengte wordt uitgelezen.
        using var documentLock = doc.LockDocument();

        var options = new PromptEntityOptions(
            $"\nSelecteer de polyline voor {cableName}: ");
        options.SetRejectMessage("\nSelecteer een 2D- of 3D-polyline.");
        options.AddAllowedClass(typeof(Polyline), false);
        options.AddAllowedClass(typeof(Polyline2d), false);
        options.AddAllowedClass(typeof(Polyline3d), false);

        var picked = editor.GetEntity(options);
        if (picked.Status != PromptStatus.OK)
            return new PolylinePickResult(true, 0, "Polyline-selectie geannuleerd.");

        var unitFactor = GetMetersPerDrawingUnit(database.Insunits, out var unitWarning);

        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(picked.ObjectId, OpenMode.ForRead) is not Curve curve)
            return new PolylinePickResult(true, 0, "Het geselecteerde object kon niet als polyline worden gelezen.");

        double rawLength;
        try
        {
            rawLength = Math.Abs(
                curve.GetDistanceAtParameter(curve.EndParam) -
                curve.GetDistanceAtParameter(curve.StartParam));
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            return new PolylinePickResult(true, 0, $"Lengte kon niet worden bepaald: {ex.Message}");
        }

        var lengthMeters = rawLength * unitFactor;
        if (lengthMeters <= 0 || double.IsNaN(lengthMeters) || double.IsInfinity(lengthMeters))
            return new PolylinePickResult(true, 0, "De geselecteerde polyline heeft geen geldige lengte.");

        return new PolylinePickResult(false, lengthMeters, unitWarning);
    }

    public static TextCurrentSelectionResult ReadSelectedTextCurrents()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return new TextCurrentSelectionResult(
                true,
                Array.Empty<TextCurrentValue>(),
                0,
                0,
                "Geen actieve tekening.");
        }

        var editor = doc.Editor;
        var database = doc.Database;

        using var documentLock = doc.LockDocument();

        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\nSelecteer TEXT/MTEXT met stroomwaarden en druk Enter: ",
            MessageForRemoval = "\nVerwijder TEXT/MTEXT uit de selectie: "
        };

        var selection = editor.GetSelection(options);
        if (selection.Status != PromptStatus.OK)
        {
            return new TextCurrentSelectionResult(
                true,
                Array.Empty<TextCurrentValue>(),
                0,
                0,
                "Tekstselectie geannuleerd.");
        }

        var values = new List<TextCurrentValue>();
        var invalidTextObjects = 0;
        var nonTextObjects = 0;
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
            {
                nonTextObjects++;
                continue;
            }

            sourceText = sourceText.Trim();
            if (!CurrentTextParser.TryParseSingleCurrent(sourceText, out var amps))
            {
                invalidTextObjects++;
                if (invalidExamples.Count < 3)
                    invalidExamples.Add(sourceText);
                continue;
            }

            values.Add(new TextCurrentValue(sourceText, amps));
        }

        transaction.Commit();

        var messages = new List<string>();
        if (values.Count > 0)
            messages.Add($"{values.Count} geldige stroomwaarde(n) gelezen uit TEXT/MTEXT.");
        else
            messages.Add("Geen eenduidige stroomwaarden gevonden in de geselecteerde TEXT/MTEXT-objecten.");

        if (invalidTextObjects > 0)
        {
            var exampleText = invalidExamples.Count > 0
                ? " Voorbeeld(en): " + string.Join(" | ", invalidExamples.Select(x => $"'{x}'")) + "."
                : string.Empty;
            messages.Add(
                $"{invalidTextObjects} tekstobject(en) overgeslagen omdat er niet precies één geldig getal in stond." +
                exampleText);
        }

        if (nonTextObjects > 0)
            messages.Add($"{nonTextObjects} niet-TEXT/MTEXT object(en) genegeerd.");

        return new TextCurrentSelectionResult(
            false,
            values,
            invalidTextObjects,
            nonTextObjects,
            string.Join(Environment.NewLine, messages));
    }

    public static SelectionReadResult ReadCurrentDrawing()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return new SelectionReadResult(true, new Dictionary<string, double>(), "Geen actieve tekening.");

        var editor = doc.Editor;
        var database = doc.Database;

        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\nSelecteer kabel-lijnen/polylijnen van één richting: "
        };

        var selection = editor.GetSelection(options);
        if (selection.Status != PromptStatus.OK)
            return new SelectionReadResult(true, new Dictionary<string, double>(), "Selectie geannuleerd.");

        var lengths = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var unknownLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unsupported = 0;
        var unitFactor = GetMetersPerDrawingUnit(database.Insunits, out var unitWarning);

        using var transaction = database.TransactionManager.StartTransaction();

        foreach (var id in selection.Value.GetObjectIds())
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not Curve curve)
            {
                unsupported++;
                continue;
            }

            if (!CableCatalog.TryRecognize(curve.Layer, out var cable))
            {
                unknownLayers.Add(curve.Layer);
                continue;
            }

            try
            {
                var rawLength = Math.Abs(
                    curve.GetDistanceAtParameter(curve.EndParam) -
                    curve.GetDistanceAtParameter(curve.StartParam));

                var lengthMeters = rawLength * unitFactor;
                lengths[cable.Name] = lengths.TryGetValue(cable.Name, out var current)
                    ? current + lengthMeters
                    : lengthMeters;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                unsupported++;
            }
        }

        transaction.Commit();

        var messages = new List<string>();
        if (!string.IsNullOrWhiteSpace(unitWarning))
            messages.Add(unitWarning);

        if (unknownLayers.Count > 0)
        {
            messages.Add(
                "Niet herkende kabellaag/lagen: " +
                string.Join(", ", unknownLayers.OrderBy(x => x)) +
                ". Voeg deze segmenten handmatig via de kabeltype/polyline-workflow toe.");
        }

        if (unsupported > 0)
            messages.Add($"{unsupported} geselecteerde object(en) konden niet als kabelcurve worden verwerkt.");

        if (lengths.Count == 0)
            messages.Add("Er zijn geen kabeltypen automatisch herkend. Voeg de richting op via kabeltype + polyline.");
        else
            messages.Add("Herkende lengtes zijn als segmenten overgenomen. Je kunt extra kabeldelen toevoegen voordat je berekent.");

        return new SelectionReadResult(false, lengths, string.Join(Environment.NewLine, messages));
    }

    private static string CleanMTextContents(string contents)
    {
        var cleaned = contents
            .Replace(@"\P", " ", StringComparison.OrdinalIgnoreCase)
            .Replace(@"\~", " ", StringComparison.Ordinal);

        cleaned = MTextFormatCode.Replace(cleaned, string.Empty);
        return cleaned
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static double GetMetersPerDrawingUnit(UnitsValue units, out string warning)
    {
        warning = string.Empty;

        return units switch
        {
            UnitsValue.Millimeters => 0.001,
            UnitsValue.Centimeters => 0.01,
            UnitsValue.Meters => 1.0,
            UnitsValue.Kilometers => 1000.0,
            UnitsValue.Inches => 0.0254,
            UnitsValue.Feet => 0.3048,
            _ => AssumeMeters(out warning)
        };
    }

    private static double AssumeMeters(out string warning)
    {
        warning = "INSUNITS is niet als een ondersteunde lengteeenheid herkend; teken-eenheden worden als meters behandeld.";
        return 1.0;
    }
}
