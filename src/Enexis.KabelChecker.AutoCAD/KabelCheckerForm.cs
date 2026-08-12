using System.Globalization;
using System.Text;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class KabelCheckerForm : Form
{
    private readonly KabelCheckerEngine _engine = new();
    private readonly ComboBox _profile = new();
    private readonly DataGridView _grid = new();
    private readonly Label _fuseResult = new();
    private readonly Label _designResult = new();
    private readonly Label _impedanceResult = new();
    private readonly Label _componentsResult = new();
    private readonly Label _ampacityResult = new();
    private readonly TextBox _details = new();
    private readonly Label _message = new();

    public KabelCheckerForm(
        IReadOnlyDictionary<string, double>? presetLengths = null,
        string? message = null)
    {
        Text = "Enexis kabel checker";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 980;
        Height = 780;
        MinimumSize = new Size(900, 650);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);

        BuildUi();
        FillCableRows(presetLengths);

        _message.Text = message ??
            "Vul per gebruikte kabeldoorsnede de totale lengte binnen deze richting in. " +
            "Controleer kabelverjonging handmatig: zwaar aan het begin, dunner richting einde.";
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Controle laagspanningskabel",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(title, 0, 0);

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        header.Controls.Add(new Label
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
        header.Controls.Add(_profile);

        var calculateTop = new Button
        {
            Text = "Bereken",
            AutoSize = true,
            Margin = new Padding(12, 2, 0, 0)
        };
        calculateTop.Click += (_, _) => Calculate();
        header.Controls.Add(calculateTop);
        root.Controls.Add(header, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        _message.AutoSize = true;
        _message.MaximumSize = new Size(920, 0);
        _message.Padding = new Padding(2, 8, 2, 8);
        root.Controls.Add(_message, 0, 3);

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
        _details.Text = "Druk op Bereken om alle gG-stappen te controleren.";
        resultPanel.Controls.Add(_details, 1, 0);
        root.Controls.Add(resultPanel, 0, 4);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var close = new Button { Text = "Sluiten", AutoSize = true };
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);

        var clear = new Button { Text = "Lengtes wissen", AutoSize = true };
        clear.Click += (_, _) => ClearLengths();
        footer.Controls.Add(clear);

        var calculate = new Button { Text = "Bereken", AutoSize = true };
        calculate.Click += (_, _) => Calculate();
        footer.Controls.Add(calculate);

        root.Controls.Add(footer, 0, 5);
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
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Cable",
            HeaderText = "Kabeltype",
            ReadOnly = true,
            FillWeight = 160
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Material",
            HeaderText = "Materiaal",
            ReadOnly = true,
            FillWeight = 65
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Ampacity",
            HeaderText = "Zomer [A]",
            ReadOnly = true,
            FillWeight = 75,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0" }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Resistance",
            HeaderText = "R [Ω/km]",
            ReadOnly = true,
            FillWeight = 75,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0000" }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Reactance",
            HeaderText = "X [Ω/km]",
            ReadOnly = true,
            FillWeight = 75,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0000" }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Length",
            HeaderText = "Lengte [m]",
            ReadOnly = false,
            FillWeight = 90,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" }
        });
    }

    private void FillCableRows(IReadOnlyDictionary<string, double>? presetLengths)
    {
        _grid.Rows.Clear();
        foreach (var cable in CableCatalog.All)
        {
            var length = presetLengths is not null && presetLengths.TryGetValue(cable.Name, out var found)
                ? found
                : 0.0;

            _grid.Rows.Add(
                cable.Name,
                cable.Material,
                cable.SummerAmpacityA,
                cable.ResistanceOhmPerKm,
                cable.ReactanceOhmPerKm,
                length > 0 ? length.ToString("0.###", CultureInfo.CurrentCulture) : string.Empty);
        }
    }

    private void ClearLengths()
    {
        foreach (DataGridViewRow row in _grid.Rows)
            row.Cells["Length"].Value = string.Empty;

        ResetResult();
    }

    private void Calculate()
    {
        try
        {
            var segments = ReadSegments();
            var selectedProfile = ((ProfileItem)_profile.SelectedItem!).Profile;
            var result = _engine.Calculate(segments, selectedProfile);
            ShowResult(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kan niet berekenen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private IReadOnlyList<CableSegment> ReadSegments()
    {
        var segments = new List<CableSegment>();

        foreach (DataGridViewRow row in _grid.Rows)
        {
            var cableName = Convert.ToString(row.Cells["Cable"].Value, CultureInfo.InvariantCulture) ?? string.Empty;
            var raw = Convert.ToString(row.Cells["Length"].Value, CultureInfo.CurrentCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out var length) &&
                !double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out length))
            {
                throw new InvalidOperationException($"Ongeldige lengte bij {cableName}: '{raw}'.");
            }

            if (length < 0)
                throw new InvalidOperationException($"Lengte kan niet negatief zijn bij {cableName}.");

            if (length > 0)
                segments.Add(new CableSegment(cableName, length));
        }

        return segments;
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
        _details.Text = "Druk op Bereken om alle gG-stappen te controleren.";
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
}
