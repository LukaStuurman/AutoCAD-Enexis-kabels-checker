using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System.Windows.Forms;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed record TextCurrentBrushSample(Point3d Center, bool RemoveMode);

internal sealed class TextCurrentBrushJig : DrawJig
{
    private readonly double _radiusDrawingUnits;
    private readonly List<TextCurrentBrushSample> _brushSamples = new();
    private Point3d _center = Point3d.Origin;
    private Point3d? _lastBrushPoint;
    private bool _hasCenter;

    public TextCurrentBrushJig(double radiusDrawingUnits)
    {
        if (radiusDrawingUnits <= 0 || double.IsNaN(radiusDrawingUnits) || double.IsInfinity(radiusDrawingUnits))
            throw new ArgumentOutOfRangeException(nameof(radiusDrawingUnits));

        _radiusDrawingUnits = radiusDrawingUnits;
    }

    public Point3d Center => _center;
    public bool FinishedByEnter { get; private set; }
    public bool ShiftPressed { get; private set; }
    public IReadOnlyList<TextCurrentBrushSample> BrushSamples => _brushSamples;

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
        var options = new JigPromptPointOptions(
            "\nKlik/sleep = toevoegen, Shift+klik/sleep = deselecteren; Enter rondt af: ")
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

        ShiftPressed = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

        var previousCenter = _center;
        var hadCenter = _hasCenter;
        _center = result.Value;
        _hasCenter = true;

        if ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left)
            RecordBrushPath(_center, ShiftPressed);
        else
            _lastBrushPoint = null;

        if (hadCenter && result.Value.DistanceTo(previousCenter) <= 1e-10)
            return SamplerStatus.NoChange;

        return SamplerStatus.OK;
    }

    public IReadOnlyList<TextCurrentBrushSample> GetCompletedStrokeSamples()
    {
        if (_brushSamples.Count > 0)
            return _brushSamples;

        return _hasCenter
            ? new[] { new TextCurrentBrushSample(_center, ShiftPressed) }
            : Array.Empty<TextCurrentBrushSample>();
    }

    private void RecordBrushPath(Point3d point, bool removeMode)
    {
        if (_lastBrushPoint is not Point3d previous)
        {
            AddSample(point, removeMode);
            _lastBrushPoint = point;
            return;
        }

        var distance = previous.DistanceTo(point);
        if (distance <= 1e-10)
            return;

        // Houd de tussenafstand ruim kleiner dan de diameter, zodat de geverfde
        // cirkels elkaar overlappen en er bij snel slepen geen gaten ontstaan.
        var step = Math.Max(_radiusDrawingUnits * 0.35, 1e-9);
        var steps = Math.Max(1, (int)Math.Ceiling(distance / step));
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var interpolated = new Point3d(
                previous.X + ((point.X - previous.X) * t),
                previous.Y + ((point.Y - previous.Y) * t),
                previous.Z + ((point.Z - previous.Z) * t));
            AddSample(interpolated, removeMode);
        }

        _lastBrushPoint = point;
    }

    private void AddSample(Point3d point, bool removeMode)
    {
        if (_brushSamples.Count > 0)
        {
            var last = _brushSamples[^1];
            if (last.RemoveMode == removeMode && last.Center.DistanceTo(point) <= 1e-10)
                return;
        }

        _brushSamples.Add(new TextCurrentBrushSample(point, removeMode));
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
        if (_hasCenter)
            draw.Geometry.Circle(_center, _radiusDrawingUnits, Vector3d.ZAxis);

        return true;
    }
}
