using System.Globalization;
using System.Text;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class KabelCheckerForm : Form
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly KabelCheckerEngine _engine = new();
    private readonly ComboBox _profile = new();
    private readonly ComboBox _cablePicker = new();
    private readonly DataGridView _grid = new();
    private readonly Label _fuseResult = new();
    private readonly Label _designResult = new();
    private readonly Label _impedanceResult = new();
    private readonly Label _componentsResult = new();
    private readonly Label _ampacityResult = new();
    private readonly TextBox _details = new();
    private readonly Label _message = new();
    private readonly CurrentLoadPanel _currentLoadPanel = new();
    private readonly List<CableSegment> _segments = new();
    private bool _refreshingGrid;

    public KabelCheckerForm(
        IReadOnlyDictionary<string, double>? presetLengths = null,
        string? message = null)
    {
        Text = "Enexis kabel checker";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 840;
        Height = 720;
        MinimumSize = new Size(760, 620);
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
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
        root.Controls.Add(_grid, 0, 2);

        _message.AutoSize = true;
        _message.MaximumSize = new Size(800, 0);
        _message.Padding = new Padding(1, 4, 1, 4);
        _message.Margin = new Padding(0);
        root.Controls.Add(_message, 0, 3);

        var resultPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 2, 0, 4)
        };
        resultPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        resultPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61));

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(6)
        };

        summary.Controls.Add(CreateSectionLabel("Maximaal toegestaan"));
        ConfigureLargeResult(_fuseResult, "— gG");
        ConfigureLargeResult(_designResult, "— A ontwerpstroom");
        summary.Controls.Add(_fuseResult);
        summary.Controls.Add(_designResult);

        _impedanceResult.AutoSize = true;
        _componentsResult.AutoSize = true;
        _ampacityResult.AutoSize = true;
        summary.Controls.Add(_impedanceResult);
        summary.Controls.Add(_componentsResult);
        summary.Controls.Add(_ampacityResult);
        resultPanel.Controls.Add(summary, 0, 0);

        _details.Dock = DockStyle.Fill;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.Font = new Font("Consolas", 8.25F);
        _details.Text = "Bouw eerst de richting op en druk daarna op Bereken richting.";
        resultPanel.Controls.Add(_details, 1, 0);
        root.Controls.Add(resultPanel, 0, 4);

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

        var reset = new Button { Text = "Reset", AutoSize = true };
        reset.Click += (_, _) => ResetDirection();
        footer.Controls.Add(reset);

        var calculate = new Button { Text = "Bereken richting", AutoSize = true };
        calculate.Click += (_, _) => Calculate();
        footer.Controls.Add(calculate);

        root.Controls.Add(footer, 0, 6);
        Controls.Add(root);
        AcceptButton = calculate;
        CancelButton = close;
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

        _message.Text =
            $"Toegevoegd: {selected.CableName} — {picked.LengthMeters:0.00} m." +
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
        if (_refreshingGrid || e.RowIndex < 0 || e.RowIndex >= _segments.Count)
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
        if (_refreshingGrid || e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Length")
            return;

        if (TryParsePositiveLength(Convert.ToString(e.FormattedValue), out _))
            return;

        e.Cancel = true;
        _message.Text = "Lengte moet een positief getal zijn. Zowel 12,5 als 12.5 wordt geaccepteerd.";
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_refreshingGrid || e.RowIndex < 0 || e.RowIndex >= _segments.Count || _grid.Columns[e.ColumnIndex].Name != "Length")
            return;

        var value = Convert.ToString(_grid.Rows[e.RowIndex].Cells["Length"].Value);
        if (!TryParsePositiveLength(value, out var length))
            return;

        var current = _segments[e.RowIndex];
        if (Math.Abs(current.LengthMeters - length) <= 1e-9)
            return;

        _segments[e.RowIndex] = new CableSegment(current.CableName, length);
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
    }

    private void ResetDirection()
    {
        _segments.Clear();
        RefreshSegmentGrid();
        ResetResult();
        _message.Text = "Richting gereset. Voeg de eerste hele polyline of een deel tot knippunt toe.";
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
        if (result.MaximumAllowed is null)
        {
            _fuseResult.Text = "Geen gG toegestaan";
            _designResult.Text = "—";
        }
        else
        {
            _fuseResult.Text = $"{result.FuseAmps} A gG";
            _designResult.Text = $"max. {result.MaxDesignCurrentAmps} A ontwerpstroom";
        }

        _impedanceResult.Text = $"Z: {result.TotalImpedanceOhm:0.000000} Ω";
        _componentsResult.Text = $"R: {result.TotalResistanceOhm:0.000000} Ω | X: {result.TotalReactanceOhm:0.000000} Ω";
        _ampacityResult.Text = $"Laagste kabel-I: {result.LimitingCableAmpacityA:0.0} A";

        var sb = new StringBuilder();
        sb.AppendLine("Richting (gelijke typen samengevoegd):");
        foreach (var segment in result.Segments)
            sb.AppendLine($"- {segment.CableName}: {segment.LengthMeters:0.00} m");

        sb.AppendLine();
        sb.AppendLine("gG   ontwerp   Z-limiet   Z   kabel-I   resultaat");
        sb.AppendLine("--------------------------------------------------");
        foreach (var assessment in result.Assessments)
        {
            sb.AppendLine(
                $"{assessment.Option.FuseAmps,3}A  " +
                $"{assessment.Option.MaxDesignCurrentAmps,3}A     " +
                $"{assessment.Option.MaxImpedanceOhm,7:0.000}Ω  " +
                $"{(assessment.ImpedanceOk ? "OK" : "NEE"),3}   " +
                $"{(assessment.AmpacityOk ? "OK" : "NEE"),5}   " +
                $"{(assessment.Allowed ? "JA" : "NEE")}");
        }

        sb.AppendLine();
        sb.AppendLine("Beide Excel-controles moeten voldoen.");
        sb.AppendLine("Kabelverjonging blijft een aparte ontwerpvoorwaarde.");
        _details.Text = sb.ToString();
        _currentLoadPanel.SetCalculation(result);
    }

    private void ResetResult()
    {
        _fuseResult.Text = "— gG";
        _designResult.Text = "— A ontwerpstroom";
        _impedanceResult.Text = string.Empty;
        _componentsResult.Text = string.Empty;
        _ampacityResult.Text = string.Empty;
        _details.Text = "Bouw de richting op en druk op Bereken richting.";
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

    private static Label CreateSectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9.5F, FontStyle.Bold)
    };

    private static void ConfigureLargeResult(Label label, string initialText)
    {
        label.Text = initialText;
        label.AutoSize = true;
        label.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13.5F, FontStyle.Bold);
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
