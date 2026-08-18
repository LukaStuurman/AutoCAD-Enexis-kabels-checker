using System.Globalization;
using Enexis.KabelChecker.Core;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class CurrentLoadPanel : UserControl
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly NumericUpDown _radiusMeters = new();
    private readonly ComboBox _kaderVersion = new();
    private readonly DataGridView _grid = new();
    private readonly Label _total = new();
    private readonly Label _assessment = new();
    private readonly Label _details = new();
    private readonly List<LoadRow> _rows = new();
    private readonly Dictionary<ObjectId, double> _selectedTextObjects = new();
    private CalculationResult? _calculation;
    private bool _refreshing;

    public CurrentLoadPanel()
    {
        Dock = DockStyle.Fill;
        BorderStyle = BorderStyle.FixedSingle;
        BuildUi();
        RefreshGrid();
        RefreshAssessment();
    }

    public IReadOnlyList<CurrentLoadInput> GetCurrentLoads() =>
        _rows.Select(x => new CurrentLoadInput(x.Amps, x.Count)).ToArray();

    public void LoadCurrentLoads(IEnumerable<CurrentLoadInput> loads)
    {
        _rows.Clear();
        _selectedTextObjects.Clear();
        foreach (var load in loads.Where(x => x.Amps > 0 && x.Count > 0))
            _rows.Add(new LoadRow(load.Amps, load.Count));
        NormalizeRows();
        RefreshGrid();
        RefreshAssessment("Ontwerpstroom van opgeslagen richting geladen.");
    }

    public void SetCalculation(CalculationResult? calculation)
    {
        _calculation = calculation;
        RefreshAssessment();
    }

    public void ResetAll()
    {
        _rows.Clear();
        _selectedTextObjects.Clear();
        _calculation = null;
        _radiusMeters.Value = 3.0M;
        RefreshGrid();
        RefreshAssessment("Ontwerpstroom volledig gereset.");
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(7),
            ColumnCount = 3,
            RowCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };

        var kader = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        kader.Controls.Add(new Label
        {
            Text = "Kader versie:",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold),
            Padding = new Padding(0, 5, 3, 0)
        });
        _kaderVersion.DropDownStyle = ComboBoxStyle.DropDownList;
        _kaderVersion.Width = 220;
        foreach (var definition in KaderVersions.All)
            _kaderVersion.Items.Add(definition);
        _kaderVersion.SelectedIndexChanged += (_, _) =>
        {
            if (_kaderVersion.SelectedItem is KaderVersionDefinition selected)
            {
                KaderVersionSelection.SetCurrent(selected.Version);
                _details.Text = $"Kaderversie ingesteld op {selected.DisplayName}.";
            }
        };
        _kaderVersion.SelectedItem = KaderVersions.Get(KaderVersionSelection.Current);
        kader.Controls.Add(_kaderVersion);
        actions.Controls.Add(kader);

        actions.Controls.Add(new Label
        {
            Text = "Ontwerpstroom uit tekst",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9.5F, FontStyle.Bold)
        });

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        var brush = new Button { Text = "Cirkel", AutoSize = true };
        brush.Click += (_, _) => SelectWithBrush();
        buttons.Controls.Add(brush);
        var manual = new Button { Text = "Handmatig", AutoSize = true };
        manual.Click += (_, _) => SelectManually();
        buttons.Controls.Add(manual);
        var remove = new Button { Text = "Verwijder rij", AutoSize = true };
        remove.Click += (_, _) => RemoveSelectedRow();
        buttons.Controls.Add(remove);
        actions.Controls.Add(buttons);

        var radius = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        radius.Controls.Add(new Label { Text = "Cirkelstraal [m]:", AutoSize = true, Padding = new Padding(0, 5, 3, 0) });
        _radiusMeters.DecimalPlaces = 1;
        _radiusMeters.Minimum = 0.1M;
        _radiusMeters.Maximum = 100.0M;
        _radiusMeters.Increment = 0.5M;
        _radiusMeters.Value = 3.0M;
        _radiusMeters.Width = 70;
        radius.Controls.Add(_radiusMeters);
        actions.Controls.Add(radius);
        actions.Controls.Add(new Label
        {
            Text = "De gekozen kaderversie bepaalt de Excel-ontwerpstromen en invoerrijen. Unieke ontwerpstromen worden automatisch gekoppeld; bij een dubbele waarde kies je bij opslaan de juiste verdeling.",
            AutoSize = true,
            MaximumSize = new Size(300, 0)
        });
        root.Controls.Add(actions, 0, 0);

        ConfigureGrid();
        var overview = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        overview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        overview.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        overview.Controls.Add(_grid, 0, 0);
        _total.AutoSize = true;
        _total.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold);
        overview.Controls.Add(_total, 0, 1);
        root.Controls.Add(overview, 1, 0);

        var status = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        _assessment.AutoSize = true;
        _assessment.MaximumSize = new Size(330, 0);
        _assessment.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F, FontStyle.Bold);
        _details.AutoSize = true;
        _details.MaximumSize = new Size(330, 0);
        status.Controls.Add(_assessment, 0, 0);
        status.Controls.Add(_details, 0, 1);
        root.Controls.Add(status, 2, 0);

        Controls.Add(root);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Height = 110;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amps", HeaderText = "Ontwerpstroom [A]", FillWeight = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Aantal", FillWeight = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subtotal", HeaderText = "Subtotaal [A]", ReadOnly = true, FillWeight = 90 });
        _grid.CellValidating += Grid_CellValidating;
        _grid.CellEndEdit += Grid_CellEndEdit;
        _grid.DataError += (_, e) => e.ThrowException = false;
    }

    private void SelectWithBrush()
    {
        var result = TextCurrentBrushSelection.Read((double)_radiusMeters.Value, _selectedTextObjects.Keys.ToArray());
        if (result.Cancelled)
        {
            _details.Text = result.Message;
            return;
        }

        foreach (var value in result.RemovedValues)
        {
            if (_selectedTextObjects.Remove(value.ObjectId, out var amps))
                DecrementRow(amps);
        }
        foreach (var value in result.AddedValues)
        {
            if (_selectedTextObjects.ContainsKey(value.ObjectId))
                continue;
            _selectedTextObjects[value.ObjectId] = value.Amps;
            IncrementRow(value.Amps);
        }
        NormalizeRows();
        RefreshGrid();
        RefreshAssessment(result.Message);
    }

    private void SelectManually()
    {
        var result = TextCurrentManualSelection.Read();
        if (result.Cancelled)
        {
            _details.Text = result.Message;
            return;
        }

        var skipped = 0;
        foreach (var value in result.Values)
        {
            if (_selectedTextObjects.ContainsKey(value.ObjectId))
            {
                skipped++;
                continue;
            }
            _selectedTextObjects[value.ObjectId] = value.Amps;
            IncrementRow(value.Amps);
        }
        NormalizeRows();
        RefreshGrid();
        RefreshAssessment(result.Message + (skipped > 0 ? $"{Environment.NewLine}{skipped} al geselecteerde tekstobject(en) overgeslagen." : string.Empty));
    }

    private void RemoveSelectedRow()
    {
        if (_grid.CurrentRow is null)
            return;
        var index = _grid.CurrentRow.Index;
        if (index < 0 || index >= _rows.Count)
            return;
        var amps = _rows[index].Amps;
        foreach (var id in _selectedTextObjects.Where(x => SameAmps(x.Value, amps)).Select(x => x.Key).ToArray())
            _selectedTextObjects.Remove(id);
        _rows.RemoveAt(index);
        RefreshGrid();
        RefreshAssessment("Ontwerpstroomrij verwijderd.");
    }

    private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_refreshing || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;
        var name = _grid.Columns[e.ColumnIndex].Name;
        var text = Convert.ToString(e.FormattedValue);
        if (name == "Amps" && !TryParsePositiveAmps(text, out _))
        {
            e.Cancel = true;
            _details.Text = "Ontwerpstroom moet een positief getal zijn.";
        }
        else if (name == "Count" && !TryParsePositiveCount(text, out _))
        {
            e.Cancel = true;
            _details.Text = "Aantal moet een positief geheel getal zijn.";
        }
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_refreshing || e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.ColumnIndex < 0)
            return;
        var row = _rows[e.RowIndex];
        var name = _grid.Columns[e.ColumnIndex].Name;
        if (name == "Amps")
        {
            if (!TryParsePositiveAmps(Convert.ToString(_grid.Rows[e.RowIndex].Cells["Amps"].Value), out var amps))
                return;
            var old = row.Amps;
            row.Amps = amps;
            foreach (var id in _selectedTextObjects.Where(x => SameAmps(x.Value, old)).Select(x => x.Key).ToArray())
                _selectedTextObjects[id] = amps;
        }
        else if (name == "Count")
        {
            if (!TryParsePositiveCount(Convert.ToString(_grid.Rows[e.RowIndex].Cells["Count"].Value), out var count))
                return;
            row.Count = count;
            var tracked = _selectedTextObjects.Where(x => SameAmps(x.Value, row.Amps)).Select(x => x.Key).ToArray();
            foreach (var id in tracked.Skip(count))
                _selectedTextObjects.Remove(id);
        }
        else
        {
            return;
        }
        NormalizeRows();
        RefreshGrid();
        RefreshAssessment("Ontwerpstroomtabel aangepast.");
    }

    private void IncrementRow(double amps)
    {
        var row = _rows.FirstOrDefault(x => SameAmps(x.Amps, amps));
        if (row is null)
            _rows.Add(new LoadRow(amps, 1));
        else
            row.Count++;
    }

    private void DecrementRow(double amps)
    {
        var row = _rows.FirstOrDefault(x => SameAmps(x.Amps, amps));
        if (row is null)
            return;
        row.Count--;
        if (row.Count <= 0)
            _rows.Remove(row);
    }

    private void NormalizeRows()
    {
        var merged = _rows
            .GroupBy(x => x.Amps)
            .Select(x => new LoadRow(x.Key, x.Sum(y => y.Count)))
            .OrderBy(x => x.Amps)
            .ToArray();
        _rows.Clear();
        _rows.AddRange(merged);
    }

    private void RefreshGrid()
    {
        _refreshing = true;
        try
        {
            _grid.Rows.Clear();
            foreach (var row in _rows)
                _grid.Rows.Add(FormatAmps(row.Amps), row.Count.ToString(DutchCulture), FormatAmps(row.Amps * row.Count));
        }
        finally
        {
            _refreshing = false;
        }
        var total = _rows.Sum(x => x.Amps * x.Count);
        var count = _rows.Sum(x => x.Count);
        _total.Text = count == 0 ? "Totaal: 0 A" : $"Totaal: {FormatAmps(total)} A ({count}×)";
    }

    private void RefreshAssessment(string? message = null)
    {
        var total = _rows.Sum(x => x.Amps * x.Count);
        if (_calculation?.MaxDesignCurrentAmps is not int maxAllowed)
        {
            _assessment.ForeColor = SystemColors.ControlText;
            _assessment.Text = _rows.Count == 0 ? "Ontwerpstroom: —" : $"Ontwerpstroom totaal: {FormatAmps(total)} A";
            _details.Text = string.IsNullOrWhiteSpace(message) ? "Bereken de kabelrichting om de ontwerpstroom te toetsen." : message;
            return;
        }

        var fits = total <= maxAllowed + 1e-9;
        _assessment.ForeColor = fits ? Color.SeaGreen : Color.Firebrick;
        _assessment.Text = _rows.Count == 0
            ? $"Maximaal toegestaan: {maxAllowed} A"
            : fits
                ? $"PAST — {FormatAmps(total)} A ≤ {maxAllowed} A"
                : $"PAST NIET — {FormatAmps(total)} A > {maxAllowed} A";
        _details.Text = message ?? (fits ? $"Marge {FormatAmps(maxAllowed - total)} A." : $"Overschrijding {FormatAmps(total - maxAllowed)} A.");
    }

    private static bool TryParsePositiveAmps(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parsed = double.TryParse(text.Trim(), NumberStyles.Float, DutchCulture, out value) ||
                     double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return parsed && value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool TryParsePositiveCount(string? text, out int value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(text) &&
               int.TryParse(text.Trim(), NumberStyles.Integer, DutchCulture, out value) &&
               value > 0;
    }

    private static bool SameAmps(double left, double right) => Math.Abs(left - right) <= 1e-9;
    private static string FormatAmps(double value) => value.ToString("0.##", DutchCulture);

    private sealed class LoadRow
    {
        public LoadRow(double amps, int count) { Amps = amps; Count = count; }
        public double Amps { get; set; }
        public int Count { get; set; }
    }
}
