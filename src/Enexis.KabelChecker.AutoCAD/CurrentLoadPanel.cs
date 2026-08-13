using System.Globalization;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class CurrentLoadPanel : UserControl
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly Button _selectButton = new();
    private readonly Label _summary = new();
    private readonly Label _details = new();
    private readonly Label _icon = new();
    private CalculationResult? _calculation;

    public CurrentLoadPanel()
    {
        AutoSize = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 4, 0, 8);
        BorderStyle = BorderStyle.FixedSingle;
        BuildUi();
        SetCalculation(null);
    }

    public void SetCalculation(CalculationResult? calculation)
    {
        _calculation = calculation;

        if (calculation is null)
        {
            _selectButton.Enabled = false;
            SetNeutral(
                "Bereken eerst de kabelrichting.",
                "Daarna kun je meerdere TEXT/MTEXT-objecten met stroomwaarden selecteren.");
            return;
        }

        if (calculation.MaxDesignCurrentAmps is null)
        {
            _selectButton.Enabled = false;
            _icon.Text = "✕";
            _icon.ForeColor = Color.Firebrick;
            _summary.Text = "Richting heeft geen toegestane ontwerpstroom";
            _summary.ForeColor = Color.Firebrick;
            _details.Text = "De kabelrichting voldoet zelf nog niet aan de gG-/impedantievoorwaarden.";
            return;
        }

        _selectButton.Enabled = true;
        SetNeutral(
            $"Maximaal toegestaan: {calculation.MaxDesignCurrentAmps.Value} A ontwerpstroom",
            "Klik op de knop en selecteer alle TEXT/MTEXT-objecten met de stroomwaarden van deze richting.");
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));

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
            Text = "Controle belasting uit tekst",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        });
        actionPanel.Controls.Add(new Label
        {
            Text = "Ondersteunt o.a. 25, 25,5, 25.5 en 25,5 A.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        });

        _selectButton.Text = "Tekststromen selecteren + toetsen";
        _selectButton.AutoSize = true;
        _selectButton.Padding = new Padding(8, 3, 8, 3);
        _selectButton.Click += (_, _) => SelectAndCheck();
        actionPanel.Controls.Add(_selectButton);
        root.Controls.Add(actionPanel, 0, 0);

        var resultPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(8, 0, 8, 0)
        };

        _summary.AutoSize = true;
        _summary.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12F, FontStyle.Bold);
        _summary.MaximumSize = new Size(500, 0);
        _summary.Margin = new Padding(0, 5, 0, 4);
        resultPanel.Controls.Add(_summary, 0, 0);

        _details.AutoSize = true;
        _details.MaximumSize = new Size(500, 0);
        _details.Margin = new Padding(0);
        resultPanel.Controls.Add(_details, 0, 1);
        root.Controls.Add(resultPanel, 1, 0);

        _icon.AutoSize = true;
        _icon.TextAlign = ContentAlignment.MiddleCenter;
        _icon.Font = new Font("Segoe UI Symbol", 42F, FontStyle.Bold);
        _icon.Margin = new Padding(4, 0, 4, 0);
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

        var selection = AutoCadSelectionReader.ReadSelectedTextCurrents();
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
            .Take(12)
            .Select(x => FormatAmps(x.Amps))
            .ToList();
        if (selection.Values.Count > 12)
            shownValues.Add("…");

        var marginText = fits
            ? $"Marge over: {FormatAmps(difference)} A."
            : $"Overschrijding: {FormatAmps(difference)} A.";

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
