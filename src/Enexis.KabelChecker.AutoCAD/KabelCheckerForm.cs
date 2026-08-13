using System.Globalization;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class KabelCheckerForm : Form
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly KabelCheckerEngine _engine = new();
    private readonly ComboBox _profile = new();
    private readonly ComboBox _cablePicker = new();
    private readonly DataGridView _grid = new();
    private readonly Label _totalLengthLabel = new();
    private readonly Label _fuseResult = new();
    private readonly Label _message = new();
    private readonly CurrentLoadPanel _currentLoadPanel = new();
    private readonly List<CableSegment> _segments = new();
    private bool _refreshingGrid;

    public KabelCheckerForm(
        IReadOnlyDictionary<string, double>? presetLengths = null,
        string? message = null)
    {
        Text = "Enexis kabel checker — gemaakt door Luka Stuurman";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 840;
        Height = 690;
        MinimumSize = new Size(760, 600);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 8.75F);

        BuildUi();
        FillCablePicker();
        LoadPresetSegments(presetLengths);

        _message.Text = message ??
            "Kies een kabeltype en voeg een hele polyline of alleen een deel tot een virtueel knippunt toe. " +
            "Kabeltype en lengte kun je daarna direct in de tabel wijzigen.";
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 7
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 4)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));

        header.Controls.Add(new Label
        {
            Text = "LS-richting controle",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 12.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 0, 0)
        }, 0, 0);

        var profilePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };

        _profile.DropDownStyle = ComboBoxStyle.DropDownList;
        _profile.Width = 285;
        _profile.Items.Add(new ProfileItem("Evenredig verdeeld (50%)", LoadProfile.Evenredig));
        _profile.Items.Add(new ProfileItem("Laatste helft geconcentreerd (75%)", LoadProfile.LaatsteHelft));
        _profile.SelectedIndex = 0;
        _profile.SelectedIndexChanged += (_, _) =>
        {
            ResetResult();
            _message.Text = "Belastingsituatie gewijzigd; bereken de richting opnieuw.";
        };
        profilePanel.Controls.Add(_profile);
        profilePanel.Controls.Add(new Label
        {
            Text = "Profiel:",
            AutoSize = true,
            Padding = new Padding(0, 5, 4, 0)
        });
        header.Controls.Add(profilePanel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var addPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(5),
            Margin = new Padding(0, 0, 0, 4),
            BorderStyle = BorderStyle.FixedSingle
        };

        addPanel.Controls.Add(new Label
        {
            Text = "Kabel:",
            AutoSize = true,
            Padding = new Padding(0, 5, 3, 0)
        });

        _cablePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _cablePicker.Width = 205;
        addPanel.Controls.Add(_cablePicker);

        var pickFull = new Button
        {
            Text = "Hele polyline",
            AutoSize = true,
            Margin = new Padding(6, 0, 0, 0)
        };
        pickFull.Click += (_, _) => PickPolylineForSelectedCable(useVirtualCut: false);
        addPanel.Controls.Add(pickFull);

        var pickPart = new Button
        {
            Text = "Deel tot knippunt",
            AutoSize = true,
            Margin = new Padding(5, 0, 0, 0)
        };
        pickPart.Click += (_, _) => PickPolylineForSelectedCable(useVirtualCut: true);
        addPanel.Controls.Add(pickPart);

        var remove = new Button
        {
            Text = "Verwijder rij",
            AutoSize = true,
            Margin = new Padding(5, 0, 0, 0)
        };
        remove.Click += (_, _) => RemoveSelectedSegment();
        addPanel.Controls.Add(remove);
        root.Controls.Add(addPanel, 0, 1);

        ConfigureGrid();
        var cableTablePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        cableTablePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cableTablePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cableTablePanel.Controls.Add(_grid, 0, 0);

        _totalLengthLabel.AutoSize = true;
        _totalLengthLabel.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
        _totalLengthLabel.Anchor = AnchorStyles.Right;
        _totalLengthLabel.Margin = new Padding(0, 4, 2, 2);
        cableTablePanel.Controls.Add(_totalLengthLabel, 0, 1);
        root.Controls.Add(cableTablePanel, 0, 2);

        _message.AutoSize = true;
        _message.MaximumSize = new Size(800, 0);
        _message.Padding = new Padding(1, 4, 1, 4);
        _message.Margin = new Padding(0);
        root.Controls.Add(_message, 0, 3);

        var fusePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6),
            Margin = new Padding(0, 2, 0, 4),
            BorderStyle = BorderStyle.FixedSingle
        };
        fusePanel.Controls.Add(new Label
        {
            Text = "Hoogste toegestane zekering:",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
            Padding = new Padding(0, 6, 8, 0)
        });
        ConfigureLargeResult(_fuseResult, "— gG");
        fusePanel.Controls.Add(_fuseResult);
        root.Controls.Add(fusePanel, 0, 4);

        root.Controls.Add(_currentLoadPanel, 0, 5);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        };

        var close = new Button { Text = "Sluiten", AutoSize = true };
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);

        var reset = new Button { Text = "Reset", AutoSize = true, CausesValidation = false };
        reset.Click += (_, _) => ResetDirection();
        footer.Controls.Add(reset);

        var calculate = new Button { Text = "Bereken richting", AutoSize = true };
        calculate.Click += (_, _) => Calculate();
        footer.Controls.Add(calculate);

        footer.Controls.Add(new Label
        {
            Text = "Gemaakt door Luka Stuurman",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(0, 6, 0, 0),
            Margin = new Padding(14, 0, 0, 0)
        });

        root.Controls.Add(footer, 0, 6);
        Controls.Add(root);
        AcceptButton = calculate;
        CancelButton = close;

        UpdateTotalLengthLabel();
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.ReadOnly = false;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.RowTemplate.Height = 23;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Order",
            HeaderText = "#",
            FillWeight = 25,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        var cableColumn = new DataGridViewComboBoxColumn
        {
            Name = "Cable",
            HeaderText = "Kabeltype (bewerkbaar)",
            FillWeight = 165,
            FlatStyle = FlatStyle.Flat,
            DisplayStyleForCurrentCellOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        foreach (var cable in CableCatalog.All)
            cableColumn.Items.Add(cable.Name);
        _grid.Columns.Add(cableColumn);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Length",
            HeaderText = "Lengte [m] (bewerkbaar)",
            FillWeight = 105,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" }
        });

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += Grid_CellValueChanged;
        _grid.CellValidating += Grid_CellValidating;
        _grid.CellEndEdit += Grid_CellEndEdit;
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            _message.Text = "Ongeldige tabelwaarde. Kies een geldig kabeltype of voer een positieve lengte in.";
        };
    }

    private void FillCablePicker()
    {
        _cablePicker.Items.Clear();
        foreach (var cable in CableCatalog.All)
        {
            _cablePicker.Items.Add(new CableItem(
                $"{cable.CrossSectionMm2}{cable.Material}  ({cable.Name})",
                cable.Name));
        }

        var defaultIndex = CableCatalog.All
            .Select((cable, index) => (cable, index))
            .FirstOrDefault(x => x.cable.CrossSectionMm2 == 150 && x.cable.Material == "Al")
            .index;

        _cablePicker.SelectedIndex = _cablePicker.Items.Count > 0
            ? Math.Clamp(defaultIndex, 0, _cablePicker.Items.Count - 1)
            : -1;
    }

    private void LoadPresetSegments(IReadOnlyDictionary<string, double>? presetLengths)
    {
        if (presetLengths is null)
            return;

        foreach (var pair in presetLengths)
        {
            if (pair.Value > 0 && CableCatalog.TryGet(pair.Key, out _))
                _segments.Add(new CableSegment(pair.Key, pair.Value));
        }
        RefreshSegmentGrid();
    }

    private void PickPolylineForSelectedCable(bool useVirtualCut)
    {
        if (_cablePicker.SelectedItem is not CableItem selected)
        {
            MessageBox.Show(this, "Kies eerst een kabeltype.", "Kabeltype ontbreekt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var picked = useVirtualCut
            ? AutoCadSelectionReader.PickPolylinePartToVirtualCut(selected.CableName)
            : AutoCadSelectionReader.PickSinglePolyline(selected.CableName);

        if (picked.Cancelled)
        {
            if (!string.IsNullOrWhiteSpace(picked.Message))
                _message.Text = picked.Message;
            return;
        }

        _segments.Add(new CableSegment(selected.CableName, picked.LengthMeters));
        RefreshSegmentGrid();
        ResetResult();
        _message.Text = $"Toegevoegd: {selected.CableName} — {picked.LengthMeters:0.00} m." +
                        (string.IsNullOrWhiteSpace(picked.Message) ? string.Empty : " " + picked.Message);
    }

    private void RemoveSelectedSegment()
    {
        if (_grid.CurrentRow is null)
            return;

        var index = _grid.CurrentRow.Index;
        if (index < 0 || index >= _segments.Count)
            return;

        _segments.RemoveAt(index);
        RefreshSegmentGrid();
        ResetResult();
        _message.Text = "Segment verwijderd; bereken de richting opnieuw.";
    }

    private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_refreshingGrid || e.RowIndex < 0 || e.RowIndex >= _segments.Count || e.ColumnIndex < 0)
            return;
        if (_grid.Columns[e.ColumnIndex].Name != "Cable")
            return;

        var cableName = Convert.ToString(_grid.Rows[e.RowIndex].Cells["Cable"].Value);
        if (string.IsNullOrWhiteSpace(cableName) || !CableCatalog.TryGet(cableName, out _))
            return;

        var current = _segments[e.RowIndex];
        if (current.CableName.Equals(cableName, StringComparison.OrdinalIgnoreCase))
            return;

        _segments[e.RowIndex] = new CableSegment(cableName, current.LengthMeters);
        ResetResult();
        _message.Text = $"Rij {e.RowIndex + 1}: kabeltype aangepast naar {cableName}; bereken opnieuw.";
    }

    private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_refreshingGrid || e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Length")
            return;

        if (TryParsePositiveLength(Convert.ToString(e.FormattedValue), out _))
            return;

        e.Cancel = true;
        _message.Text = "Lengte moet een positief getal zijn. Zowel 12,5 als 12.5 wordt geaccepteerd.";
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_refreshingGrid || e.RowIndex < 0 || e.RowIndex >= _segments.Count || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Length")
            return;

        var value = Convert.ToString(_grid.Rows[e.RowIndex].Cells["Length"].Value);
        if (!TryParsePositiveLength(value, out var length))
            return;

        var current = _segments[e.RowIndex];
        if (Math.Abs(current.LengthMeters - length) <= 1e-9)
            return;

        _segments[e.RowIndex] = new CableSegment(current.CableName, length);
        UpdateTotalLengthLabel();
        ResetResult();
        _message.Text = $"Rij {e.RowIndex + 1}: lengte aangepast naar {length:0.00} m; bereken opnieuw.";
    }

    private void RefreshSegmentGrid()
    {
        _refreshingGrid = true;
        try
        {
            _grid.Rows.Clear();
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                _grid.Rows.Add(i + 1, segment.CableName, segment.LengthMeters);
            }
        }
        finally
        {
            _refreshingGrid = false;
        }
        UpdateTotalLengthLabel();
    }

    private void UpdateTotalLengthLabel()
    {
        var total = _segments.Sum(x => x.LengthMeters);
        _totalLengthLabel.Text = $"Totale kabellengte: {total.ToString("0.00", DutchCulture)} m";
    }

    private void ResetDirection()
    {
        _segments.Clear();
        RefreshSegmentGrid();

        if (_profile.Items.Count > 0)
            _profile.SelectedIndex = 0;
        FillCablePicker();

        _fuseResult.Text = "— gG";
        _currentLoadPanel.ResetAll();
        _message.Text = "Alles gereset: kabelrichting, berekeningsresultaat en ontwerpstroom zijn leeggemaakt.";
    }

    private void Calculate()
    {
        try
        {
            _grid.EndEdit();
            var selectedProfile = ((ProfileItem)_profile.SelectedItem!).Profile;
            var result = _engine.Calculate(_segments, selectedProfile);
            ShowResult(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kan niet berekenen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowResult(CalculationResult result)
    {
        _fuseResult.Text = result.MaximumAllowed is null
            ? "Geen gG toegestaan"
            : $"{result.FuseAmps} A gG";

        _currentLoadPanel.SetCalculation(result);
        _message.Text = result.MaximumAllowed is null
            ? "Berekening afgerond: voor deze richting is geen gG-zekering toegestaan."
            : $"Berekening afgerond. Hoogste toegestane zekering: {result.FuseAmps} A gG.";
    }

    private void ResetResult()
    {
        _fuseResult.Text = "— gG";
        _currentLoadPanel.SetCalculation(null);
    }

    private static bool TryParsePositiveLength(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var parsed = double.TryParse(trimmed, NumberStyles.Float, DutchCulture, out value) ||
                     double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return parsed && value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static void ConfigureLargeResult(Label label, string initialText)
    {
        label.Text = initialText;
        label.AutoSize = true;
        label.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14F, FontStyle.Bold);
        label.Padding = new Padding(0, 2, 0, 1);
    }

    private sealed record ProfileItem(string Text, LoadProfile Profile)
    {
        public override string ToString() => Text;
    }

    private sealed record CableItem(string Text, string CableName)
    {
        public override string ToString() => Text;
    }
}
