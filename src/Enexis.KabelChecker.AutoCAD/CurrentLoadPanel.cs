using System.Globalization;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class CurrentLoadPanel : UserControl
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly Button _brushButton = new();
    private readonly NumericUpDown _radiusMeters = new();
    private readonly Label _summary = new();
    private readonly Label _details = new();
    private readonly Label _icon = new();
    private CalculationResult? _calculation;

    public CurrentLoadPanel()
    {
        AutoSize = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 2, 0, 4);
        BorderStyle = BorderStyle.FixedSingle;
        BuildUi();
        SetCalculation(null);
    }

    public void SetCalculation(CalculationResult? calculation)
    {
        _calculation = calculation;

        if (calculation is null)
        {
            _brushButton.Enabled = false;
            SetNeutral(
                "Bereken eerst de kabelrichting.",
                "Daarna kun je stroomteksten met de ronde selectieborstel aanklikken.");
            return;
        }

        if (calculation.MaxDesignCurrentAmps is null)
        {
            _brushButton.Enabled = false;
            _icon.Text = "✕";
            _icon.ForeColor = Color.Firebrick;
            _summary.Text = "Richting heeft geen toegestane ontwerpstroom";
            _summary.ForeColor = Color.Firebrick;
            _details.Text = "De kabelrichting voldoet zelf nog niet aan de gG-/impedantievoorwaarden.";
            return;
        }

        _brushButton.Enabled = true;
        SetNeutral(
            $"Maximaal toegestaan: {calculation.MaxDesignCurrentAmps.Value} A",
            "Klik met de cirkel nabij elke stroomtekst; Enter rondt de selectie af.");
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(7)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9));

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0)
        };
        actionPanel.Controls.Add(new Label
        {
            Text = "Belasting uit tekst",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });

        var brushControls = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };

        _brushButton.Text = "Cirkel selecteren";
        _brushButton.AutoSize = true;
        _brushButton.Padding = new Padding(4, 1, 4, 1);
        _brushButton.Margin = new Padding(0, 0, 7, 0);
        _brushButton.Click += (_, _) => SelectAndCheck();
        brushControls.Controls.Add(_brushButton);

        brushControls.Controls.Add(new Label
        {
            Text = "Straal [m]:",
            AutoSize = true,
            Padding = new Padding(0, 5, 3, 0),
            Margin = new Padding(0)
        });

        _radiusMeters.DecimalPlaces = 1;
        _radiusMeters.Minimum = 0.1M;
        _radiusMeters.Maximum = 100.0M;
        _radiusMeters.Increment = 0.5M;
        _radiusMeters.Value = 2.0M;
        _radiusMeters.Width = 67;
        _radiusMeters.Margin = new Padding(0, 1, 0, 0);
        brushControls.Controls.Add(_radiusMeters);

        actionPanel.Controls.Add(brushControls);
        actionPanel.Controls.Add(new Label
        {
            Text = "Per klik wordt de dichtstbijzijnde geldige TEXT/MTEXT binnen de cirkel toegevoegd.",
            AutoSize = true,
            MaximumSize = new Size(390, 0),
            Margin = new Padding(0, 4, 0, 0)
        });
        root.Controls.Add(actionPanel, 0, 0);

        var resultPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(6, 0, 6, 0)
        };

        _summary.AutoSize = true;
        _summary.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10.5F, FontStyle.Bold);
        _summary.MaximumSize = new Size(410, 0);
        _summary.Margin = new Padding(0, 2, 0, 2);
        resultPanel.Controls.Add(_summary, 0, 0);

        _details.AutoSize = true;
        _details.MaximumSize = new Size(410, 0);
        _details.Margin = new Padding(0);
        resultPanel.Controls.Add(_details, 0, 1);
        root.Controls.Add(resultPanel, 1, 0);

        _icon.AutoSize = true;
        _icon.TextAlign = ContentAlignment.MiddleCenter;
        _icon.Font = new Font("Segoe UI Symbol", 32F, FontStyle.Bold);
        _icon.Margin = new Padding(2, 0, 2, 0);
        _icon.Anchor = AnchorStyles.None;
        root.Controls.Add(_icon, 2, 0);

        Controls.Add(root);
    }

    private void SelectAndCheck()
    {
        if (_calculation?.MaxDesignCurrentAmps is not int maxAllowed)
        {
            MessageBox.Show(
                this,
                "Bereken eerst de kabelrichting zodat de maximale ontwerpstroom bekend is.",
                "Eerst richting berekenen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var selection = AutoCadSelectionReader.ReadSelectedTextCurrentsByBrush((double)_radiusMeters.Value);
        if (selection.Cancelled)
        {
            _details.Text = selection.Message;
            return;
        }

        if (selection.Values.Count == 0)
        {
            SetNeutral(
                "Geen geldige stroomwaarden gevonden",
                selection.Message);
            return;
        }

        var total = selection.Values.Sum(x => x.Amps);
        var fits = total <= maxAllowed + 1e-9;
        var difference = Math.Abs(maxAllowed - total);

        _icon.Text = fits ? "✓" : "✕";
        _icon.ForeColor = fits ? Color.SeaGreen : Color.Firebrick;
        _summary.ForeColor = fits ? Color.SeaGreen : Color.Firebrick;
        _summary.Text = fits
            ? $"PAST — {FormatAmps(total)} A ≤ {maxAllowed} A"
            : $"PAST NIET — {FormatAmps(total)} A > {maxAllowed} A";

        var shownValues = selection.Values
            .Take(10)
            .Select(x => FormatAmps(x.Amps))
            .ToList();
        if (selection.Values.Count > 10)
            shownValues.Add("…");

        var marginText = fits
            ? $"Marge {FormatAmps(difference)} A."
            : $"Overschrijding {FormatAmps(difference)} A.";

        _details.Text =
            $"{selection.Values.Count} waarde(n): {string.Join(" + ", shownValues)} = {FormatAmps(total)} A. " +
            marginText + Environment.NewLine + selection.Message;
    }

    private void SetNeutral(string summary, string details)
    {
        _icon.Text = "—";
        _icon.ForeColor = SystemColors.GrayText;
        _summary.ForeColor = SystemColors.ControlText;
        _summary.Text = summary;
        _details.Text = details;
    }

    private static string FormatAmps(double value) =>
        value.ToString("0.##", DutchCulture);
}
