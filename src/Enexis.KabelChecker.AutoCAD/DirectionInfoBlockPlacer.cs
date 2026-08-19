using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Enexis.KabelChecker.AutoCAD.DirectionInfoCommands))]

namespace Enexis.KabelChecker.AutoCAD;

public sealed class DirectionInfoCommands
{
    [CommandMethod("ENEXISRICHTINGINFO", CommandFlags.Modal)]
    public void PlaceDirectionInfo()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        var directions = DirectionStore.Instance.Directions;
        if (directions.Count == 0)
        {
            doc.Editor.WriteMessage("\nSla eerst minimaal één richting op in ENEXISKABELCHECK.");
            return;
        }

        var options = new PromptIntegerOptions("\nRichtingnummer voor info (1-12): ")
        {
            LowerLimit = 1,
            UpperLimit = 12,
            DefaultValue = directions[0].Number,
            UseDefaultValue = true,
            AllowNone = true
        };

        var result = doc.Editor.GetInteger(options);
        if (result.Status is not PromptStatus.OK and not PromptStatus.None)
            return;

        var number = result.Status == PromptStatus.None ? options.DefaultValue : result.Value;
        var direction = DirectionStore.Instance.Get(number);
        if (direction is null)
        {
            doc.Editor.WriteMessage($"\nRichting {number} is niet opgeslagen in de huidige pluginsessie.");
            return;
        }

        try
        {
            var message = DirectionInfoBlockPlacer.Place(direction);
            doc.Editor.WriteMessage($"\n{message}");
        }
        catch (System.Exception ex)
        {
            doc.Editor.WriteMessage($"\nRichting-info kon niet worden geplaatst: {ex.Message}");
        }
    }
}

internal static class DirectionInfoBlockPlacer
{
    private const double TextHeight = 1.0;
    private const double LineSpacing = 1.1150990076130256;
    private const string TextStyleName = "ARIAL";
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    public static string Place(DirectionState direction)
    {
        ArgumentNullException.ThrowIfNull(direction);

        if (direction.Number is < 1 or > 12)
            throw new InvalidOperationException("Richtingnummer moet tussen 1 en 12 liggen.");

        var doc = AcApp.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("Geen actieve AutoCAD-tekening.");
        var editor = doc.Editor;
        var database = doc.Database;
        var layerName = $"Aansluiting LS K{direction.Number:00}";

        using var documentLock = doc.LockDocument();
        using var transaction = database.TransactionManager.StartTransaction();

        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (!layerTable.Has(layerName))
        {
            throw new InvalidOperationException(
                $"Laag '{layerName}' bestaat niet in deze tekening. De richting-info gebruikt bewust alleen de bestaande richtingslaag zodat kleur en laageigenschappen kloppen.");
        }

        var textStyleId = GetOrCreateArialTextStyle(database, transaction);

        var pointOptions = new PromptPointOptions(
            $"\nKies middelpunt van de bovenste richting-info tekst K{direction.Number:00}: ");
        var pointResult = editor.GetPoint(pointOptions);
        if (pointResult.Status != PromptStatus.OK)
            return "Plaatsen van richting-info geannuleerd.";

        var totalCurrent = direction.CurrentLoads.Sum(x => x.Amps * x.Count);
        var totalLength = direction.Segments.Sum(x => x.LengthMeters);

        // Exact hetzelfde tekstformaat als het aangeleverde DXF-voorbeeld.
        var currentText = $"{totalCurrent.ToString("0.##", DutchCulture)}Amp.";
        var lengthText = $"{totalLength.ToString("0.00", DutchCulture)}Met.";

        var currentSpace = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        var topCenter = pointResult.Value;
        var bottomCenter = new Point3d(topCenter.X, topCenter.Y - LineSpacing, topCenter.Z);

        AppendCenteredText(currentSpace, transaction, database, currentText, topCenter, layerName, textStyleId);
        AppendCenteredText(currentSpace, transaction, database, lengthText, bottomCenter, layerName, textStyleId);

        transaction.Commit();
        editor.Regen();

        return $"Richting-info K{direction.Number:00} geplaatst op laag '{layerName}'.";
    }

    private static ObjectId GetOrCreateArialTextStyle(Database database, Transaction transaction)
    {
        var textStyleTable = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        if (textStyleTable.Has(TextStyleName))
            return textStyleTable[TextStyleName];

        textStyleTable.UpgradeOpen();
        var style = new TextStyleTableRecord
        {
            Name = TextStyleName,
            FileName = "arial.ttf"
        };
        var styleId = textStyleTable.Add(style);
        transaction.AddNewlyCreatedDBObject(style, true);
        return styleId;
    }

    private static void AppendCenteredText(
        BlockTableRecord space,
        Transaction transaction,
        Database database,
        string text,
        Point3d center,
        string layerName,
        ObjectId textStyleId)
    {
        var entity = new DBText
        {
            Height = TextHeight,
            TextString = text,
            TextStyleId = textStyleId,
            HorizontalMode = TextHorizontalMode.TextCenter,
            VerticalMode = TextVerticalMode.TextVerticalMid,
            Position = center,
            AlignmentPoint = center,
            Layer = layerName,
            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };

        space.AppendEntity(entity);
        transaction.AddNewlyCreatedDBObject(entity, true);
        entity.AdjustAlignment(database);
    }
}
