using System.Text;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class KabelCheckerForm : Form
{
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
    private readonly List<CableSegment> _segments = new();

    public KabelCheckerForm(
        IReadOnlyDictionary<string, double>? presetLengths = null,
        string? message = null)
    {
        Text = "Enexis kabel checker - richting opbouwen";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 980;
        Height = 780;
        MinimumSize = new Size(900, 650);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);

        BuildUi();
        FillCablePicker();
        LoadPresetSegments(presetLengths);

        _message.Text = message ??
            "Kies een kabeltype, klik op 'Polyline kiezen + toevoegen' en selecteer de bijbehorende polyline in AutoCAD. " +
            "Herhaal dit voor alle kabeldelen van dezelfde richting en druk daarna op 'Bereken richting'.";
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 7
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Controle laagspanningsrichting",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(title, 0, 0);

        var profilePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        profilePanel.Controls.Add(new Label
        {
            Text = "Belastingsituatie:",
            AutoSize = true,
            Padding = new Padding(0, 6, 6, 0)
        });

        _profile.DropDownStyle = ComboBoxStyle.DropDownList;
        _profile.Width = 330;
        _profile.Items.Add(new ProfileItem("Evenredig verdeeld over kabel (50%)", LoadProfile.Evenredig));
        _profile.Items.Add(new ProfileItem("Geconcentreerd op laatste helft (75%)", LoadProfile.LaatsteHelft));
        _profile.SelectedIndex = 0;
        profilePanel.Controls.Add(_profile);
        root.Controls.Add(profilePanel, 0, 1);

        var addPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8),
            BorderStyle = BorderStyle.FixedSingle
        };
        addPanel.Controls.Add(new Label
        {
            Text = "Kabeltype:",
            AutoSize = true,
            Padding = new Padding(0, 7, 5, 0)
        });

        _cablePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _cablePicker.Width = 245;
        addPanel.Controls.Add(_cablePicker);

        var pickPolyline = new Button
        {
            Text = "Polyline kiezen + toevoegen",
            AutoSize = true,
            Margin = new Padding(10, 1, 0, 0)
        };
        pickPolyline.Click += (_, _) => PickPolylineForSelectedCable();
        addPanel.Controls.Add(pickPolyline);

        var remove = new Button
        {
            Text = "Geselecteerd segment verwijderen",
            AutoSize = true,
            Margin = new Padding(10, 1, 0, 0)
        };
        remove.Click += (_, _) => RemoveSelectedSegment();
        addPanel.Controls.Add(remove);

        root.Controls.Add(addPanel, 0, 2);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 3);

        _message.AutoSize = true;
        _message.MaximumSize = new Size(920, 0);
        _message.Padding = new Padding(2, 8, 2, 8);
        root.Controls.Add(_message, 0, 4);

        var resultPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 8)
        };
        resultPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        resultPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(10)
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
        _details.Font = new Font("Consolas", 9F);
        _details.Text = "Bouw eerst de richting op en druk daarna op Bereken richting.";
        resultPanel.Controls.Add(_details, 1, 0);
        root.Controls.Add(resultPanel, 0, 5);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var close = new Button { Text = "Sluiten", AutoSize = true };
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);

        var reset = new Button { Text = "Reset richting", AutoSize = true };
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
        _grid.MultiSelect = true;
        _grid.ReadOnly = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Order",
            HeaderText = "#",
            FillWeight = 35
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Cable",
            HeaderText = "Kabeltype",
            FillWeight = 180
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Length",
            HeaderText = "Polyline lengte [m]",
            FillWeight = 100,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" }
        });
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

    private void PickPolylineForSelectedCable()
    {
        if (_cablePicker.SelectedItem is not CableItem selected)
        {
            MessageBox.Show(this, "Kies eerst een kabeltype.", "Kabeltype ontbreekt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var picked = AutoCadSelectionReader.PickSinglePolyline(this, selected.CableName);
        if (picked.Cancelled)
        {
            if (!string.IsNullOrWhiteSpace(picked.Message))
                _message.Text = picked.Message;
            return;
        }

        _segments.Add(new CableSegment(selected.CableName, picked.LengthMeters));
        RefreshSegmentGrid();
        ResetResult();

        var totalForType = _segments
            .Where(x => x.CableName.Equals(selected.CableName, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.LengthMeters);

        _message.Text =
            $"Toegevoegd: {selected.CableName} — {picked.LengthMeters:0.00} m. " +
            $"Totaal {selected.CableName} in deze richting: {totalForType:0.00} m." +
            (string.IsNullOrWhiteSpace(picked.Message) ? string.Empty : Environment.NewLine + picked.Message);
    }

    private void RemoveSelectedSegment()
    {
        if (_grid.SelectedRows.Count == 0)
            return;

        var indices = _grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.Index)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        foreach (var index in indices)
        {
            if (index >= 0 && index < _segments.Count)
                _segments.RemoveAt(index);
        }

        RefreshSegmentGrid();
        ResetResult();
        _message.Text = indices.Length == 1
            ? "Segment verwijderd."
            : $"{indices.Length} segmenten verwijderd.";
    }

    private void RefreshSegmentGrid()
    {
        _grid.Rows.Clear();
        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            _grid.Rows.Add(i + 1, segment.CableName, segment.LengthMeters);
        }
    }

    private void ResetDirection()
    {
        _segments.Clear();
        RefreshSegmentGrid();
        ResetResult();
        _message.Text = "Richting gereset. Kies een kabeltype en voeg de eerste polyline van de nieuwe richting toe.";
    }

    private void Calculate()
    {
        try
        {
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

        _impedanceResult.Text = $"Z totaal: {result.TotalImpedanceOhm:0.000000} Ω";
        _componentsResult.Text = $"R totaal: {result.TotalResistanceOhm:0.000000} Ω   |   X totaal: {result.TotalReactanceOhm:0.000000} Ω";
        _ampacityResult.Text = $"Laagste stroombelastbaarheid in richting: {result.LimitingCableAmpacityA:0.0} A";

        var sb = new StringBuilder();
        sb.AppendLine("Opgebouwde richting (gelijke typen samengevoegd):");
        foreach (var segment in result.Segments)
            sb.AppendLine($"- {segment.CableName}: {segment.LengthMeters:0.00} m");

        sb.AppendLine();
        sb.AppendLine("gG   ontwerp   Z-limiet   imped.  kabel-I  resultaat");
        sb.AppendLine("------------------------------------------------------");
        foreach (var assessment in result.Assessments)
        {
            sb.AppendLine(
                $"{assessment.Option.FuseAmps,3}A  " +
                $"{assessment.Option.MaxDesignCurrentAmps,3}A     " +
                $"{assessment.Option.MaxImpedanceOhm,7:0.000}Ω   " +
                $"{(assessment.ImpedanceOk ? "OK" : "NEE"),5}   " +
                $"{(assessment.AmpacityOk ? "OK" : "NEE"),5}   " +
                $"{(assessment.Allowed ? "JA" : "NEE")}");
        }

        sb.AppendLine();
        sb.AppendLine("Excelvoorwaarde: beide controles moeten voldoen.");
        sb.AppendLine("Kabelverjonging (zwaar naar dun) blijft een aparte ontwerpvoorwaarde.");
        _details.Text = sb.ToString();
    }

    private void ResetResult()
    {
        _fuseResult.Text = "— gG";
        _designResult.Text = "— A ontwerpstroom";
        _impedanceResult.Text = string.Empty;
        _componentsResult.Text = string.Empty;
        _ampacityResult.Text = string.Empty;
        _details.Text = "Bouw eerst de richting op en druk daarna op Bereken richting.";
    }

    private static Label CreateSectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F, FontStyle.Bold)
    };

    private static void ConfigureLargeResult(Label label, string initialText)
    {
        label.Text = initialText;
        label.AutoSize = true;
        label.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 16F, FontStyle.Bold);
        label.Padding = new Padding(0, 4, 0, 2);
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
