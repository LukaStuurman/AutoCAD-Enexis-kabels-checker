using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Enexis.KabelChecker.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

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
    }
}

public sealed class KabelCheckerCommands
{
    [CommandMethod("ENEXISKABELCHECK", CommandFlags.Modal)]
    public void OpenManualCalculator()
    {
        using var form = new KabelCheckerForm();
        AcApp.ShowModalDialog(form);
    }

    [CommandMethod("ENEXISKABELCHECKSEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void OpenFromSelection()
    {
        var result = AutoCadSelectionReader.ReadCurrentDrawing();
        if (result.Cancelled)
            return;

        using var form = new KabelCheckerForm(result.LengthsByCable, result.Message);
        AcApp.ShowModalDialog(form);
    }
}

internal sealed record SelectionReadResult(
    bool Cancelled,
    IReadOnlyDictionary<string, double> LengthsByCable,
    string Message);

internal static class AutoCadSelectionReader
{
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
                ". Vul deze lengtes handmatig aan.");
        }

        if (unsupported > 0)
            messages.Add($"{unsupported} geselecteerde object(en) konden niet als kabelcurve worden verwerkt.");

        if (lengths.Count == 0)
            messages.Add("Er zijn geen kabeltypen automatisch herkend. Vul de kabellengtes handmatig in.");
        else
            messages.Add("Herkende lengtes zijn uit de selectie overgenomen. Controleer de waarden vóór berekenen.");

        return new SelectionReadResult(false, lengths, string.Join(Environment.NewLine, messages));
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
