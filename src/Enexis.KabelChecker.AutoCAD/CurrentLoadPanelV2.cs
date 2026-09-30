using System.Globalization;
using Enexis.KabelChecker.Core;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class CurrentLoadPanel : UserControl
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly NumericUpDown _radiusMeters = new();
    private readonly ComboBox _kaderVersion = new();
    private readonly ComboBox _currentMode = new();
    private readonly Label _stationTotal = new();
    private readonly DataGridView _grid = new();
    private readonly Label _total = new();
    private readonly Label _assessment = new();
    private readonly Label _details = new();
    private readonly List<LoadRow> _rows = new();
    private readonly Dictionary<ObjectId, double> _selectedTextObjects = new();
    private CalculationResult? _calculation;
    private bool _refreshing;
    private bool _settingSelection;
    private IReadOnlyList<ExcelMappedLoad> _mapped = Array.Empty<ExcelMappedLoad>();

    public event Action? InputsChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<KaderVersion, bool>? CanChangeKader { get; set; }
    public bool HasInputs => _rows.Any(x => x.Count > 0 && (x.Amps > 0 || x.AllowZero));

    public IReadOnlyList<ExcelMappedLoad>? GetMappedLoads() =>
        DesignCurrentCalculator.MatchesInputs(GetCurrentLoads(), _mapped)
        && _mapped.All(x => ExcelLoadCatalog.FindByKey(KaderVersionSelection.Current, x.ExcelLoadKey) is not null)
            ? _mapped : null;

    public IReadOnlyList<ExcelMappedLoad>? ResolveMappedLoads(IWin32Window owner)
    {
        _grid.EndEdit();
        var mapped = ExcelLoadResolver.Resolve(owner, GetCurrentLoads(), _mapped);
        if (mapped is null) return null;
        _mapped = mapped;
        NormalizeRows();
        RefreshGrid();
        RefreshAssessment();
        InputsChanged?.Invoke();
        return mapped;
    }

    public void SetStationTotals(DesignCurrentTotals? totals, int directions)
    {
        var mode = KaderVersionSelection.CurrentMode;
        _stationTotal.Text = totals is null
            ? "Station: koppel de invoer aan aansluittype via Bereken richting."
            : $"Station ({directions} richting(en)): verbruik {FormatAmps(totals.StationConsumptionAmps)} A / opwek {FormatAmps(totals.StationGenerationAmps)} A.\n" +
              $"Gekozen: {totals.StationBasis(mode)} — {FormatAmps(totals.StationCurrent(mode))} A (trafowaarden).";
    }

    public CurrentLoadPanel()
    {
        Dock = DockStyle.Fill;
        BorderStyle = BorderStyle.FixedSingle;
        BuildUi();
        RefreshGrid();
        RefreshAssessment();
    }

    public IReadOnlyList<CurrentLoadInput> GetCurrentLoads() =>
        _rows
            .Where(x => (x.Amps > 0 || x.AllowZero) && x.Count > 0)
            .GroupBy(x => x.Amps)
            .Select(x => new CurrentLoadInput(x.Key, x.Sum(y => y.Count)))
            .OrderBy(x => x.Amps)
            .ToArray();

    public void CommitPendingEdit() => _grid.EndEdit();

    public void SetKaderVersion(KaderVersion version)
    {
        _settingSelection = true;
        KaderVersionSelection.SetCurrent(version);
        var definition = KaderVersions.Get(version);
        if (!Equals(_kaderVersion.SelectedItem, definition))
            _kaderVersion.SelectedItem = definition;
        _currentMode.SelectedItem = KaderVersionSelection.CurrentMode;
        _settingSelection = false;
    }

    public void SetCurrentMode(DesignCurrentMode mode)
    {
        _settingSelection = true;
        KaderVersionSelection.SetMode(mode);
        _currentMode.SelectedItem = mode;
        _settingSelection = false;
        RefreshGrid();
        RefreshAssessment();
    }

    public void LoadCurrentLoads(IEnumerable<CurrentLoadInput> loads, IReadOnlyList<ExcelMappedLoad>? mapped = null)
    {
        _rows.Clear();
        _selectedTextObjects.Clear();
        _mapped = mapped ?? Array.Empty<ExcelMappedLoad>();
        foreach (var load in loads.Where(x => x.Amps >= 0 && x.Count > 0))
            _rows.Add(new LoadRow(load.Amps, load.Count, load.Amps == 0));
        NormalizeRows();
        RefreshGrid();
        RefreshAssessment("Ontwerpstroom van opgeslagen richting geladen.");
        InputsChanged?.Invoke();
    }

    public void SetCalculation(CalculationResult? calculation)
    {
        _calculation = calculation;
        RefreshAssessment();
    }

    public void ResetAll()
    {
        _rows.Clear();
        _mapped = Array.Empty<ExcelMappedLoad>();
        _selectedTextObjects.Clear();
        _calculation = null;
        _radiusMeters.Value = 3.0M;
        RefreshGrid();
        RefreshAssessment("Ontwerpstroom volledig gereset.");
        InputsChanged?.Invoke();
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var kader = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.TopDown };
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
                if (!_settingSelection && CanChangeKader?.Invoke(selected.Version) == false)
                {
                    _settingSelection = true;
                    _kaderVersion.SelectedItem = KaderVersions.Get(KaderVersionSelection.Current);
                    _settingSelection = false;
                    return;
                }
                KaderVersionSelection.SetCurrent(selected.Version);
                _currentMode.SelectedItem = KaderVersionSelection.CurrentMode;
                RefreshGrid();
                RefreshAssessment();
                _details.Text = $"Kaderversie ingesteld op {selected.DisplayName}.";
                if (!_settingSelection) InputsChanged?.Invoke();
            }
        };
        _kaderVersion.SelectedItem = KaderVersions.Get(KaderVersionSelection.Current);
        kader.Controls.Add(_kaderVersion);
        actions.Controls.Add(kader);

        var basis = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        basis.Controls.Add(new Label { Text = "Stroombasis:", AutoSize = true, Padding = new Padding(0, 5, 3, 0) });
        _currentMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _currentMode.Width = 150;
        foreach (var mode in Enum.GetValues<DesignCurrentMode>()) _currentMode.Items.Add(mode);
        _currentMode.SelectedIndexChanged += (_, _) =>
        {
            if (_settingSelection || _currentMode.SelectedItem is not DesignCurrentMode mode) return;
            _grid.EndEdit();
            KaderVersionSelection.SetMode(mode);
            RefreshGrid();
            RefreshAssessment();
            InputsChanged?.Invoke();
        };
        _currentMode.SelectedItem = KaderVersionSelection.CurrentMode;
        basis.Controls.Add(_currentMode);
        actions.Controls.Add(basis);

        actions.Controls.Add(new Label
        {
            Text = "Ontwerpstroom",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9.5F, FontStyle.Bold)
        });

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        var brush = new Button { Text = "Cirkel", AutoSize = true };
        brush.Click += (_, _) => SelectWithBrush();
        buttons.Controls.Add(brush);
        var manual = new Button { Text = "Handmatig", AutoSize = true };
        manual.Click += (_, _) => AddManualRow();
        buttons.Controls.Add(manual);
        var fromKader = new Button { Text = "Uit kader", AutoSize = true };
        fromKader.Click += (_, _) => AddKaderRow();
        buttons.Controls.Add(fromKader);
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
        var instructions = new Label
        {
            Text = "Uit kader kiest direct een aansluittype. Cirkel/Handmatig leest waarden uit de gekozen kolom. Automatisch vergelijkt eerst de totalen van verbruik en opwek, per kabel en station apart.",
            AutoSize = true,
            MaximumSize = new Size(300, 0)
        };
        actions.Controls.Add(instructions);
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

        var status = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        _assessment.AutoSize = true;
        _assessment.MaximumSize = new Size(330, 0);
        _assessment.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F, FontStyle.Bold);
        _details.AutoSize = true;
        _details.MaximumSize = new Size(330, 0);
        status.Controls.Add(_assessment, 0, 0);
        status.Controls.Add(_details, 0, 1);
        _stationTotal.AutoSize = true;
        _stationTotal.MaximumSize = new Size(270, 0);
        _stationTotal.Padding = new Padding(0, 10, 0, 0);
        status.Controls.Add(_stationTotal, 0, 2);
        root.Controls.Add(status, 2, 0);

        Resize += (_, _) =>
        {
            var width = Math.Max(100, (ClientSize.Width - root.Padding.Horizontal) * 27 / 100 - 16);
            _assessment.MaximumSize = _details.MaximumSize = _stationTotal.MaximumSize = new Size(width, 0);
            instructions.MaximumSize = new Size(width, 0);
            _kaderVersion.Width = Math.Min(220, width);
        };

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
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amps", HeaderText = "Tekst / invoer [A]", FillWeight = 85 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Aantal", FillWeight = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Consumption", HeaderText = "Verbruik totaal [A]", ReadOnly = true, FillWeight = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Generation", HeaderText = "Opwek totaal [A]", ReadOnly = true, FillWeight = 95 });
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
        InputsChanged?.Invoke();
    }

    private void AddManualRow()
    {
        var draftIndex = _rows.FindIndex(x => x.Amps <= 0 && !x.AllowZero);
        if (draftIndex < 0)
        {
            _rows.Add(new LoadRow(0, 1));
            draftIndex = _rows.Count - 1;
            RefreshGrid();
        }

        if (draftIndex >= 0 && draftIndex < _grid.Rows.Count)
        {
            _grid.CurrentCell = _grid.Rows[draftIndex].Cells["Amps"];
            _grid.Focus();
            _grid.BeginEdit(true);
        }

        RefreshAssessment("Nieuwe handmatige rij toegevoegd. Vul de ontwerpstroom en het aantal in.");
    }

    private void AddKaderRow()
    {
        _grid.EndEdit();
        if (HasInputs && GetMappedLoads() is null && ResolveMappedLoads(FindForm()!) is null) return;
        using var dialog = new KaderLoadPicker(KaderVersionSelection.Current);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.SelectedOption is not ExcelLoadOption option) return;
        var amps = option.InputAmps(KaderVersionSelection.CurrentMode);
        var row = _rows.FirstOrDefault(x => SameAmps(x.Amps, amps) && (amps > 0 || x.AllowZero));
        if (row is null) _rows.Add(new LoadRow(amps, dialog.Count, amps == 0));
        else row.Count += dialog.Count;
        _mapped = _mapped.Concat(new[] { new ExcelMappedLoad(option.Key, amps, dialog.Count) }).ToArray();
        NormalizeRows();
        RefreshGrid();
        RefreshAssessment();
        InputsChanged?.Invoke();
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
        _mapped = _mapped.Where(x => !SameAmps(x.Amps, amps)).ToArray();
        RefreshGrid();
        RefreshAssessment("Ontwerpstroomrij verwijderd.");
        InputsChanged?.Invoke();
    }

    private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_refreshing || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;
        var name = _grid.Columns[e.ColumnIndex].Name;
        var text = Convert.ToString(e.FormattedValue);
        if (name == "Amps")
        {
            if (string.IsNullOrWhiteSpace(text) && e.RowIndex < _rows.Count && _rows[e.RowIndex].Amps <= 0)
                return;
            if (!(e.RowIndex < _rows.Count && _rows[e.RowIndex].AllowZero && text?.Trim() == "0") && !TryParsePositiveAmps(text, out _))
            {
                e.Cancel = true;
                _details.Text = "Ontwerpstroom moet een positief getal zijn.";
            }
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
            var text = Convert.ToString(_grid.Rows[e.RowIndex].Cells["Amps"].Value);
            if (string.IsNullOrWhiteSpace(text) && row.Amps <= 0)
                return;
            if (!TryParsePositiveAmps(text, out var amps))
                return;

            var old = row.Amps;
            row.Amps = amps;
            row.AllowZero = false;
            if (!SameAmps(old, amps)) _mapped = _mapped.Where(x => !SameAmps(x.Amps, old)).ToArray();
            foreach (var id in _selectedTextObjects.Where(x => SameAmps(x.Value, old)).Select(x => x.Key).ToArray())
                _selectedTextObjects[id] = amps;
        }
        else if (name == "Count")
        {
            if (!TryParsePositiveCount(Convert.ToString(_grid.Rows[e.RowIndex].Cells["Count"].Value), out var count))
                return;

            row.Count = count;
            var mappedTypes = _mapped.Where(x => SameAmps(x.Amps, row.Amps))
                .Select(x => ExcelLoadCatalog.FindByKey(KaderVersionSelection.Current, x.ExcelLoadKey))
                .ToArray();
            if (mappedTypes.Length > 0 && mappedTypes.All(x => x is not null) && mappedTypes.DistinctBy(x => x!.Key).Count() == 1)
                _mapped = _mapped.Where(x => !SameAmps(x.Amps, row.Amps))
                    .Append(new ExcelMappedLoad(mappedTypes[0]!.Key, row.Amps, _rows.Where(x => SameAmps(x.Amps, row.Amps)).Sum(x => x.Count))).ToArray();
            var tracked = _selectedTextObjects.Where(x => SameAmps(x.Value, row.Amps)).Select(x => x.Key).ToArray();
            foreach (var id in tracked.Skip(count))
                _selectedTextObjects.Remove(id);
        }
        else
        {
            return;
        }

        // Bouw de DataGridView hier bewust niet opnieuw op. Rows.Clear() zou de huidige
        // cel terugzetten naar linksboven en maakt navigeren naar andere rijen/kolommen
        // onmogelijk. Alleen de bewerkte rij en totalen worden bijgewerkt.
        UpdateRowDisplay(e.RowIndex);
        UpdateTotal();
        RefreshAssessment("Ontwerpstroomtabel aangepast.");
        InputsChanged?.Invoke();
    }

    private void UpdateRowDisplay(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || rowIndex >= _grid.Rows.Count)
            return;

        var row = _rows[rowIndex];
        _refreshing = true;
        try
        {
            _grid.Rows[rowIndex].Cells["Amps"].Value = row.Amps > 0 || row.AllowZero ? FormatAmps(row.Amps) : string.Empty;
            _grid.Rows[rowIndex].Cells["Count"].Value = row.Count.ToString(DutchCulture);
            UpdateMappedColumns(rowIndex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void IncrementRow(double amps)
    {
        var row = _rows.FirstOrDefault(x => SameAmps(x.Amps, amps) && (amps > 0 || x.AllowZero));
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
            .GroupBy(x => (x.Amps, x.AllowZero))
            .Select(x => new LoadRow(x.Key.Amps, x.Sum(y => y.Count), x.Key.AllowZero))
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
            {
                var ampsText = row.Amps > 0 || row.AllowZero ? FormatAmps(row.Amps) : string.Empty;
                var index = _grid.Rows.Add(ampsText, row.Count.ToString(DutchCulture), "—", "—");
                UpdateMappedColumns(index);
            }
        }
        finally
        {
            _refreshing = false;
        }
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        var mapped = GetMappedLoads();
        if (mapped is null) { _total.Text = "Koppeling nodig — druk Bereken richting."; return; }
        var totals = DesignCurrentCalculator.Calculate(KaderVersionSelection.Current, mapped);
        var mode = KaderVersionSelection.CurrentMode;
        _total.Text = $"Kabel: {totals.CableBasis(mode)} {FormatAmps(totals.CableCurrent(mode))} A ({totals.Count}×)";
    }

    private void UpdateMappedColumns(int index)
    {
        var row = _rows[index];
        var loads = _mapped.Where(x => SameAmps(x.Amps, row.Amps)).ToArray();
        var complete = loads.Sum(x => x.Count) == row.Count && loads.Length > 0
            && loads.All(x => ExcelLoadCatalog.FindByKey(KaderVersionSelection.Current, x.ExcelLoadKey) is not null);
        var totals = complete ? DesignCurrentCalculator.Calculate(KaderVersionSelection.Current, loads) : null;
        _grid.Rows[index].Cells["Consumption"].Value = totals is null ? "—" : FormatAmps(totals.CableConsumptionAmps);
        _grid.Rows[index].Cells["Generation"].Value = totals is null ? "—" : FormatAmps(totals.CableGenerationAmps);
        _grid.Rows[index].Cells["Amps"].ToolTipText = complete
            ? string.Join("\n", loads.Select(x => $"{x.Count}× {ExcelLoadCatalog.FindByKey(KaderVersionSelection.Current, x.ExcelLoadKey)!.DisplayName}"))
            : "Koppel deze invoer via Bereken richting.";
    }

    private void RefreshAssessment(string? message = null)
    {
        var mapped = GetMappedLoads();
        if (mapped is null)
        {
            _assessment.ForeColor = SystemColors.ControlText;
            _assessment.Text = "Ontwerpstroom nog niet gekoppeld";
            _details.Text = message ?? "Druk Bereken richting of kies een aansluittype via Uit kader.";
            return;
        }
        var totals = DesignCurrentCalculator.Calculate(KaderVersionSelection.Current, mapped);
        var total = totals.CableCurrent(KaderVersionSelection.CurrentMode);
        if (_calculation?.MaxDesignCurrentAmps is not int maxAllowed)
        {
            _assessment.ForeColor = SystemColors.ControlText;
            _assessment.Text = totals.Count == 0 ? "Ontwerpstroom: —" : $"Kabel {totals.CableBasis(KaderVersionSelection.CurrentMode)}: {FormatAmps(total)} A";
            _details.Text = string.IsNullOrWhiteSpace(message) ? "Bereken de kabelrichting om de ontwerpstroom te toetsen." : message;
            return;
        }

        var fits = total <= maxAllowed + 1e-9;
        _assessment.ForeColor = fits ? Color.SeaGreen : Color.Firebrick;
        _assessment.Text = totals.Count == 0
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
        public LoadRow(double amps, int count, bool allowZero = false) { Amps = amps; Count = count; AllowZero = allowZero; }
        public double Amps { get; set; }
        public int Count { get; set; }
        public bool AllowZero { get; set; }
    }
}
