using System.Globalization;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class KabelCheckerForm : Form
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly KabelCheckerEngine _engine = new();
    private readonly DirectionStore _store = DirectionStore.Instance;
    private readonly ComboBox _profile = new();
    private readonly ComboBox _cablePicker = new();
    private readonly DataGridView _grid = new();
    private readonly CurrentLoadPanel _currentLoadPanel = new();
    private readonly Label _totalLengthLabel = new();
    private readonly Label _fuseResult = new();
    private readonly Label _message = new();
    private readonly ComboBox _savedDirections = new();
    private readonly NumericUpDown _directionNumber = new();
    private readonly Label _editingLabel = new();
    private readonly List<CableSegment> _segments = new();
    private bool _refreshingGrid;
    private bool _refreshingDirections;
    private int? _editingDirectionNumber;

    public KabelCheckerForm(IReadOnlyDictionary<string, double>? presetLengths = null, string? message = null)
    {
        Text = "Enexis kabel checker — richtingen & Excel export";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 980;
        Height = 780;
        MinimumSize = new Size(860, 680);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 8.75F);

        BuildUi();
        FillCablePicker();
        LoadPresetSegments(presetLengths);
        _directionNumber.Value = _store.FirstAvailableNumber();
        RefreshSavedDirections();
        _message.Text = message ?? "Bouw een richting op, lees ontwerpstroom uit tekst en sla de richting op. Het richtingsnummer wordt bij de eerste kabel automatisch uit bijvoorbeeld K02/K12 gehaald en kan vóór opslaan worden aangepast.";
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 1, RowCount = 8 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildDirectionManager(), 0, 0);
        root.Controls.Add(BuildCableActions(), 0, 1);

        ConfigureGrid();
        var gridPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        gridPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gridPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gridPanel.Controls.Add(_grid, 0, 0);
        _totalLengthLabel.AutoSize = true;
        _totalLengthLabel.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
        _totalLengthLabel.Anchor = AnchorStyles.Right;
        gridPanel.Controls.Add(_totalLengthLabel, 0, 1);
        root.Controls.Add(gridPanel, 0, 2);

        _message.AutoSize = true;
        _message.MaximumSize = new Size(930, 0);
        _message.Padding = new Padding(1, 4, 1, 4);
        root.Controls.Add(_message, 0, 3);

        var fusePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6) };
        fusePanel.Controls.Add(new Label { Text = "Hoogste toegestane zekering:", AutoSize = true, Font = new Font(Font.FontFamily, 10F, FontStyle.Bold), Padding = new Padding(0, 6, 8, 0) });
        _fuseResult.Text = "— gG";
        _fuseResult.AutoSize = true;
        _fuseResult.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14F, FontStyle.Bold);
        fusePanel.Controls.Add(_fuseResult);
        root.Controls.Add(fusePanel, 0, 4);

        root.Controls.Add(_currentLoadPanel, 0, 5);
        root.Controls.Add(BuildFooter(), 0, 6);

        var hint = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "Excel-export: per opgeslagen richting een tab 'Ontwerpstroom_kabel R#'; 'Ontwerpstroom_trafo' bevat de opgetelde aantallen van alle richtingen."
        };
        root.Controls.Add(hint, 0, 7);
        Controls.Add(root);
        UpdateTotalLengthLabel();
    }

    private Control BuildDirectionManager()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6) };
        _editingLabel.Text = "Nieuwe richting";
        _editingLabel.AutoSize = true;
        _editingLabel.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
        _editingLabel.Padding = new Padding(0, 5, 10, 0);
        panel.Controls.Add(_editingLabel);

        panel.Controls.Add(new Label { Text = "RichtingNR:", AutoSize = true, Padding = new Padding(5, 5, 2, 0) });
        _directionNumber.Minimum = 1;
        _directionNumber.Maximum = 12;
        _directionNumber.Value = 1;
        _directionNumber.Width = 55;
        panel.Controls.Add(_directionNumber);

        panel.Controls.Add(new Label { Text = "Opgeslagen:", AutoSize = true, Padding = new Padding(8, 5, 2, 0) });
        _savedDirections.DropDownStyle = ComboBoxStyle.DropDownList;
        _savedDirections.Width = 170;
        _savedDirections.SelectedIndexChanged += (_, _) => LoadSelectedDirection();
        panel.Controls.Add(_savedDirections);

        var fresh = new Button { Text = "Nieuwe richting", AutoSize = true };
        fresh.Click += (_, _) => StartNewDirection();
        panel.Controls.Add(fresh);
        var save = new Button { Text = "Richting opslaan", AutoSize = true };
        save.Click += (_, _) => SaveDirection();
        panel.Controls.Add(save);
        var delete = new Button { Text = "Verwijderen", AutoSize = true };
        delete.Click += (_, _) => DeleteDirection();
        panel.Controls.Add(delete);
        var export = new Button { Text = "Excel exporteren", AutoSize = true };
        export.Click += (_, _) => ExportExcel();
        panel.Controls.Add(export);
        return panel;
    }

    private Control BuildCableActions()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(5) };
        panel.Controls.Add(new Label { Text = "Profiel:", AutoSize = true, Padding = new Padding(0, 5, 3, 0) });
        _profile.DropDownStyle = ComboBoxStyle.DropDownList;
        _profile.Width = 245;
        _profile.Items.Add(new ProfileItem("Evenredig verdeeld (50%)", LoadProfile.Evenredig));
        _profile.Items.Add(new ProfileItem("Laatste helft geconcentreerd (75%)", LoadProfile.LaatsteHelft));
        _profile.SelectedIndex = 0;
        _profile.SelectedIndexChanged += (_, _) => ResetResult();
        panel.Controls.Add(_profile);

        panel.Controls.Add(new Label { Text = "Kabel:", AutoSize = true, Padding = new Padding(10, 5, 3, 0) });
        _cablePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _cablePicker.Width = 190;
        panel.Controls.Add(_cablePicker);

        var full = new Button { Text = "Hele polyline", AutoSize = true };
        full.Click += (_, _) => PickPolyline(false);
        panel.Controls.Add(full);
        var part = new Button { Text = "Deel tot knippunt", AutoSize = true };
        part.Click += (_, _) => PickPolyline(true);
        panel.Controls.Add(part);
        var remove = new Button { Text = "Verwijder kabelrij", AutoSize = true };
        remove.Click += (_, _) => RemoveSelectedSegment();
        panel.Controls.Add(remove);
        return panel;
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var close = new Button { Text = "Sluiten", AutoSize = true };
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);
        var reset = new Button { Text = "Huidige richting leegmaken", AutoSize = true };
        reset.Click += (_, _) => ClearEditor();
        footer.Controls.Add(reset);
        var calculate = new Button { Text = "Bereken richting", AutoSize = true };
        calculate.Click += (_, _) => Calculate();
        footer.Controls.Add(calculate);
        return footer;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Order", HeaderText = "#", FillWeight = 25, ReadOnly = true });
        var cableColumn = new DataGridViewComboBoxColumn { Name = "Cable", HeaderText = "Kabeltype", FillWeight = 165, FlatStyle = FlatStyle.Flat };
        foreach (var cable in CableCatalog.All) cableColumn.Items.Add(cable.Name);
        _grid.Columns.Add(cableColumn);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Length", HeaderText = "Lengte [m]", FillWeight = 100 });
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellValueChanged += Grid_CellValueChanged;
        _grid.CellValidating += Grid_CellValidating;
        _grid.CellEndEdit += Grid_CellEndEdit;
        _grid.DataError += (_, e) => e.ThrowException = false;
    }

    private void FillCablePicker()
    {
        _cablePicker.Items.Clear();
        foreach (var cable in CableCatalog.All)
            _cablePicker.Items.Add(new CableItem($"{cable.CrossSectionMm2}{cable.Material} ({cable.Name})", cable.Name));
        _cablePicker.SelectedIndex = _cablePicker.Items.Count > 0 ? 0 : -1;
    }

    private void LoadPresetSegments(IReadOnlyDictionary<string, double>? presetLengths)
    {
        if (presetLengths is null) return;
        foreach (var pair in presetLengths)
            if (pair.Value > 0 && CableCatalog.TryGet(pair.Key, out _)) _segments.Add(new CableSegment(pair.Key, RoundLengthMeters(pair.Value)));
        RefreshSegmentGrid();
    }

    private void PickPolyline(bool useVirtualCut)
    {
        if (_cablePicker.SelectedItem is not CableItem selected) return;
        var firstCableInNewDirection = _editingDirectionNumber is null && _segments.Count == 0;
        var picked = useVirtualCut
            ? DirectionPolylineSelection.PickPolylinePartToVirtualCut(selected.CableName)
            : DirectionPolylineSelection.PickSinglePolyline(selected.CableName);
        if (picked.Cancelled) { _message.Text = picked.Message; return; }

        if (firstCableInNewDirection && picked.SuggestedDirectionNumber is int suggested)
            _directionNumber.Value = suggested;

        var roundedLength = RoundLengthMeters(picked.LengthMeters);
        _segments.Add(new CableSegment(selected.CableName, roundedLength));
        RefreshSegmentGrid();
        ResetResult();
        var directionMessage = firstCableInNewDirection && picked.SuggestedDirectionNumber is int detected
            ? $" Richtingnummer automatisch ingesteld op {detected} vanuit laag '{picked.LayerName}'."
            : string.Empty;
        _message.Text = $"Toegevoegd: {selected.CableName} — {roundedLength:0.00} m.{directionMessage} {picked.Message}";
    }

    private void RemoveSelectedSegment()
    {
        if (_grid.CurrentRow is null) return;
        var index = _grid.CurrentRow.Index;
        if (index < 0 || index >= _segments.Count) return;
        _segments.RemoveAt(index);
        RefreshSegmentGrid();
        ResetResult();
    }

    private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_refreshingGrid || e.RowIndex < 0 || e.RowIndex >= _segments.Count || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Cable") return;
        var cable = Convert.ToString(_grid.Rows[e.RowIndex].Cells["Cable"].Value);
        if (string.IsNullOrWhiteSpace(cable) || !CableCatalog.TryGet(cable, out _)) return;
        _segments[e.RowIndex] = new CableSegment(cable, _segments[e.RowIndex].LengthMeters);
        ResetResult();
    }

    private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_refreshingGrid || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Length") return;
        if (!TryParsePositiveLength(Convert.ToString(e.FormattedValue), out _)) { e.Cancel = true; _message.Text = "Lengte moet een positief getal zijn."; }
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_refreshingGrid || e.RowIndex < 0 || e.RowIndex >= _segments.Count || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Length") return;
        if (!TryParsePositiveLength(Convert.ToString(_grid.Rows[e.RowIndex].Cells["Length"].Value), out var length)) return;
        _segments[e.RowIndex] = new CableSegment(_segments[e.RowIndex].CableName, RoundLengthMeters(length));
        UpdateTotalLengthLabel();
        ResetResult();
    }

    private void RefreshSegmentGrid()
    {
        _refreshingGrid = true;
        try
        {
            _grid.Rows.Clear();
            for (var i = 0; i < _segments.Count; i++) _grid.Rows.Add(i + 1, _segments[i].CableName, _segments[i].LengthMeters.ToString("0.00", DutchCulture));
        }
        finally { _refreshingGrid = false; }
        UpdateTotalLengthLabel();
    }

    private void Calculate()
    {
        try
        {
            _grid.EndEdit();
            var result = _engine.Calculate(_segments, ((ProfileItem)_profile.SelectedItem!).Profile);
            _fuseResult.Text = result.MaximumAllowed is null ? "Geen gG toegestaan" : $"{result.FuseAmps} A gG";
            _currentLoadPanel.SetCalculation(result);
            _message.Text = "Richting berekend.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kan niet berekenen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveDirection()
    {
        try
        {
            _grid.EndEdit();
            var selectedDirectionNumber = Decimal.ToInt32(_directionNumber.Value);
            var profile = ((ProfileItem)_profile.SelectedItem!).Profile;
            var calculation = _engine.Calculate(_segments, profile);
            _currentLoadPanel.SetCalculation(calculation);
            var currentLoads = _currentLoadPanel.GetCurrentLoads();
            if (currentLoads.Count == 0)
            {
                MessageBox.Show(this, "Voeg eerst ontwerpstroom toe.", "Ontwerpstroom ontbreekt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var existing = _editingDirectionNumber is int number ? _store.Get(number)?.ExcelLoads : null;
            var mapped = ExcelLoadResolver.Resolve(this, currentLoads, existing);
            if (mapped is null) return;

            var saved = _store.Save(selectedDirectionNumber, _editingDirectionNumber, profile, _segments, currentLoads, mapped);
            _editingDirectionNumber = saved.Number;
            _directionNumber.Value = saved.Number;
            _editingLabel.Text = $"Richting {saved.Number} bewerken";
            RefreshSavedDirections(saved.Number);
            _message.Text = $"Richting {saved.Number} opgeslagen. Het nummer kan later bij openen worden gewijzigd (1 t/m 12).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Richting niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void StartNewDirection()
    {
        _editingDirectionNumber = null;
        ClearEditor();
        _directionNumber.Value = _store.FirstAvailableNumber();
        _editingLabel.Text = "Nieuwe richting";
        _savedDirections.SelectedIndex = -1;
    }

    private void ClearEditor()
    {
        _segments.Clear();
        RefreshSegmentGrid();
        if (_profile.Items.Count > 0) _profile.SelectedIndex = 0;
        _currentLoadPanel.ResetAll();
        _fuseResult.Text = "— gG";
        _message.Text = "Huidige invoer leeggemaakt.";
    }

    private void DeleteDirection()
    {
        var number = SelectedDirectionNumber ?? _editingDirectionNumber;
        if (number is not int value) return;
        if (MessageBox.Show(this, $"Richting {value} verwijderen?", "Richting verwijderen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _store.Delete(value);
        if (_editingDirectionNumber == value) StartNewDirection();
        RefreshSavedDirections();
        _message.Text = $"Richting {value} verwijderd.";
    }

    private void LoadSelectedDirection(bool force = false)
    {
        if (_refreshingDirections) return;
        if (!force && _savedDirections.Focused == false) return;
        if (SelectedDirectionNumber is not int number) return;
        var state = _store.Get(number);
        if (state is null) return;

        _editingDirectionNumber = state.Number;
        _directionNumber.Value = state.Number;
        _editingLabel.Text = $"Richting {state.Number} bewerken";
        _profile.SelectedIndex = state.Profile == LoadProfile.Evenredig ? 0 : 1;
        _segments.Clear();
        _segments.AddRange(state.Segments.Select(x => new CableSegment(x.CableName, RoundLengthMeters(x.LengthMeters))));
        RefreshSegmentGrid();
        _currentLoadPanel.LoadCurrentLoads(state.CurrentLoads);
        ResetResult();
        _message.Text = $"Richting {state.Number} geladen. Het nummer bovenaan kan vóór opslaan worden gewijzigd.";
    }

    private void RefreshSavedDirections(int? selectNumber = null)
    {
        _refreshingDirections = true;
        try
        {
            _savedDirections.Items.Clear();
            foreach (var direction in _store.Directions) _savedDirections.Items.Add(new DirectionItem(direction.Number));
            if (selectNumber is int target)
            {
                for (var i = 0; i < _savedDirections.Items.Count; i++)
                    if (((DirectionItem)_savedDirections.Items[i]!).Number == target) { _savedDirections.SelectedIndex = i; break; }
            }
            else _savedDirections.SelectedIndex = -1;
        }
        finally { _refreshingDirections = false; }
    }

    private void ExportExcel()
    {
        if (_store.Directions.Count == 0)
        {
            MessageBox.Show(this, "Sla eerst minimaal één richting op.", "Geen richtingen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel werkmap (*.xlsx)|*.xlsx",
            FileName = $"Enexis_ontwerpstroom_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            AddExtension = true,
            DefaultExt = "xlsx"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            ExcelDirectionExporter.Export(dialog.FileName, _store.Directions);
            _message.Text = $"Excel geëxporteerd: {dialog.FileName}";
            MessageBox.Show(this, "Excel-export is aangemaakt.", "Export gereed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Excel export mislukt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int? SelectedDirectionNumber => _savedDirections.SelectedItem is DirectionItem item ? item.Number : null;

    private void ResetResult()
    {
        _fuseResult.Text = "— gG";
        _currentLoadPanel.SetCalculation(null);
    }

    private void UpdateTotalLengthLabel() =>
        _totalLengthLabel.Text = $"Totale kabellengte: {_segments.Sum(x => x.LengthMeters).ToString("0.00", DutchCulture)} m";

    private static double RoundLengthMeters(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool TryParsePositiveLength(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parsed = double.TryParse(text.Trim(), NumberStyles.Float, DutchCulture, out value) || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return parsed && value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private sealed record ProfileItem(string Text, LoadProfile Profile) { public override string ToString() => Text; }
    private sealed record CableItem(string Text, string CableName) { public override string ToString() => Text; }
    private sealed record DirectionItem(int Number) { public override string ToString() => $"Richting {Number}"; }
}
