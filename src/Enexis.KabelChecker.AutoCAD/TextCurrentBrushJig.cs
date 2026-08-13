using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class TextCurrentBrushJig : DrawJig
{
    private readonly double _radiusDrawingUnits;
    private Point3d _center = Point3d.Origin;
    private bool _hasCenter;

    public TextCurrentBrushJig(double radiusDrawingUnits)
    {
        if (radiusDrawingUnits <= 0 || double.IsNaN(radiusDrawingUnits) || double.IsInfinity(radiusDrawingUnits))
            throw new ArgumentOutOfRangeException(nameof(radiusDrawingUnits));

        _radiusDrawingUnits = radiusDrawingUnits;
    }

    public Point3d Center => _center;
    public bool FinishedByEnter { get; private set; }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
        var options = new JigPromptPointOptions(
            "\nKlik om alle geldige stroomteksten binnen de cirkel toe te voegen; Enter rondt af: ")
        {
            UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted
        };

        var result = prompts.AcquirePoint(options);
        if (result.Status == PromptStatus.None)
        {
            FinishedByEnter = true;
            return SamplerStatus.Cancel;
        }

        if (result.Status != PromptStatus.OK)
            return SamplerStatus.Cancel;

        if (_hasCenter && result.Value.DistanceTo(_center) <= 1e-10)
            return SamplerStatus.NoChange;

        _center = result.Value;
        _hasCenter = true;
        return SamplerStatus.OK;
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
        if (_hasCenter)
            draw.Geometry.Circle(_center, _radiusDrawingUnits, Vector3d.ZAxis);

        return true;
    }
}
