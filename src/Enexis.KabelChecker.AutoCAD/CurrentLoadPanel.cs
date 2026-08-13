using System.Globalization;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class CurrentLoadPanel : UserControl
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly Button _brushButton = new();
    private readonly Button _manualButton = new();
    private readonly Button _clearButton = new();
    private readonly NumericUpDown _radiusMeters = new();
    private readonly DataGridView _overview = new();
    private readonly Label _totalLabel = new();
    private readonly Label _summary = new();
    private readonly Label _details = new();
    private readonly Label _icon = new();
    private readonly List<TextCurrentValue> _selectedValues = new();
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
        _selectedValues.Clear();
        RefreshOverview();

        if (calculation is null)
        {
            SetSelectionButtonsEnabled(false);
            SetNeutral(
                "Bereken eerst de kabelrichting.",
                "Daarna kun je ontwerpstroomteksten met de cirkel of handmatig toevoegen.");
            return;
        }

        if (calculation.MaxDesignCurrentAmps is null)
        {
            SetSelectionButtonsEnabled(false);
            _icon.Text = "✕";
            _icon.ForeColor = Color.Firebrick;
            _summary.Text = "Richting heeft geen toegestane ontwerpstroom";
            _summary.ForeColor = Color.Firebrick;
            _details.Text = "De kabelrichting voldoet zelf nog niet aan de gG-/impedantievoorwaarden.";
            return;
        }

        SetSelectionButtonsEnabled(true);
        SetNeutral(
            $"Maximaal toegestaan: {calculation.MaxDesignCurrentAmps.Value} A",
            "Voeg ontwerpstroomteksten toe met Cirkel of Handmatig. Nieuwe selecties worden bij de bestaande selectie opgeteld.");
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(7)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));

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

        var selectionButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };

        _brushButton.Text = "Cirkel";
        _brushButton.AutoSize = true;
        _brushButton.Padding = new Padding(4, 1, 4, 1);
        _brushButton.Margin = new Padding(0, 0, 5, 0);
        _brushButton.Click += (_, _) => SelectWithBrush();
        selectionButtons.Controls.Add(_brushButton);

        _manualButton.Text = "Handmatig";
        _manualButton.AutoSize = true;
        _manualButton.Padding = new Padding(4, 1, 4, 1);
        _manualButton.Margin = new Padding(0, 0, 5, 0);
        _manualButton.Click += (_, _) => SelectManually();
        selectionButtons.Controls.Add(_manualButton);

        _clearButton.Text = "Wis";
        _clearButton.AutoSize = true;
        _clearButton.Padding = new Padding(4, 1, 4, 1);
        _clearButton.Margin = new Padding(0);
        _clearButton.Click += (_, _) => ClearSelection();
        selectionButtons.Controls.Add(_clearButton);
        actionPanel.Controls.Add(selectionButtons);

        var radiusControls = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0)
        };
        radiusControls.Controls.Add(new Label
        {
            Text = "Cirkelstraal [m]:",
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
        radiusControls.Controls.Add(_radiusMeters);
        actionPanel.Controls.Add(radiusControls);

        actionPanel.Controls.Add(new Label
        {
            Text = "Cirkel: klik ruim naast tekst. Handmatig: normale AutoCAD-selectie. Beide voegen toe.",
            AutoSize = true,
            MaximumSize = new Size(300, 0),
            Margin = new Padding(0, 4, 0, 0)
        });
        root.Controls.Add(actionPanel, 0, 0);

        var overviewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(5, 0, 5, 0)
        };

        _overview.AllowUserToAddRows = false;
        _overview.AllowUserToDeleteRows = false;
        _overview.AllowUserToResizeRows = false;
        _overview.ReadOnly = true;
        _overview.RowHeadersVisible = false;
        _overview.MultiSelect = false;
        _overview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _overview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _overview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _overview.ColumnHeadersHeight = 23;
        _overview.RowTemplate.Height = 21;
        _overview.Height = 92;
        _overview.Dock = DockStyle.Fill;
        _overview.Margin = new Padding(0);
        _overview.ScrollBars = ScrollBars.Vertical;
        _overview.BackgroundColor = SystemColors.Window;
        _overview.BorderStyle = BorderStyle.FixedSingle;
        _overview.Columns.Add("Amps", "Ontwerpstroom [A]");
        _overview.Columns.Add("Count", "Aantal");
        _overview.Columns.Add("Subtotal", "Subtotaal [A]");
        overviewPanel.Controls.Add(_overview, 0, 0);

        _totalLabel.AutoSize = true;
        _totalLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold);
        _totalLabel.Margin = new Padding(0, 3, 0, 0);
        overviewPanel.Controls.Add(_totalLabel, 0, 1);
        root.Controls.Add(overviewPanel, 1, 0);

        var resultPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(5, 0, 5, 0)
        };

        _summary.AutoSize = true;
        _summary.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F, FontStyle.Bold);
        _summary.MaximumSize = new Size(330, 0);
        _summary.Margin = new Padding(0, 2, 0, 2);
        resultPanel.Controls.Add(_summary, 0, 0);

        _details.AutoSize = true;
        _details.MaximumSize = new Size(330, 0);
        _details.Margin = new Padding(0);
        resultPanel.Controls.Add(_details, 0, 1);
        root.Controls.Add(resultPanel, 2, 0);

        _icon.AutoSize = true;
        _icon.TextAlign = ContentAlignment.MiddleCenter;
        _icon.Font = new Font("Segoe UI Symbol", 30F, FontStyle.Bold);
        _icon.Margin = new Padding(2, 0, 2, 0);
        _icon.Anchor = AnchorStyles.None;
        root.Controls.Add(_icon, 3, 0);

        Controls.Add(root);
    }

    private void SelectWithBrush()
    {
        if (!CanSelect())
            return;

        AddSelection(TextCurrentBrushSelection.Read((double)_radiusMeters.Value));
    }

    private void SelectManually()
    {
        if (!CanSelect())
            return;

        AddSelection(AutoCadSelectionReader.ReadSelectedTextCurrents(TextCurrentSelectionMode.Manual));
    }

    private bool CanSelect()
    {
        if (_calculation?.MaxDesignCurrentAmps is int)
            return true;

        MessageBox.Show(
            this,
            "Bereken eerst de kabelrichting zodat de maximale ontwerpstroom bekend is.",
            "Eerst richting berekenen",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
    }

    private void AddSelection(TextCurrentSelectionResult selection)
    {
        if (selection.Cancelled)
        {
            _details.Text = selection.Message;
            return;
        }

        if (selection.Values.Count == 0)
        {
            if (_selectedValues.Count == 0)
            {
                SetNeutral(
                    $"Maximaal toegestaan: {_calculation?.MaxDesignCurrentAmps} A",
                    selection.Message);
            }
            else
            {
                RefreshAssessment(selection.Message);
            }
            return;
        }

        _selectedValues.AddRange(selection.Values);
        RefreshOverview();
        RefreshAssessment(selection.Message);
    }

    private void ClearSelection()
    {
        _selectedValues.Clear();
        RefreshOverview();

        if (_calculation?.MaxDesignCurrentAmps is int maxAllowed)
        {
            SetNeutral(
                $"Maximaal toegestaan: {maxAllowed} A",
                "Selectie gewist. Voeg nieuwe ontwerpstroomteksten toe met Cirkel of Handmatig.");
        }
    }

    private void RefreshOverview()
    {
        _overview.Rows.Clear();

        foreach (var group in _selectedValues
                     .GroupBy(x => x.Amps)
                     .OrderBy(x => x.Key))
        {
            var count = group.Count();
            var subtotal = group.Key * count;
            _overview.Rows.Add(
                FormatAmps(group.Key),
                count.ToString(DutchCulture),
                FormatAmps(subtotal));
        }

        var total = _selectedValues.Sum(x => x.Amps);
        _totalLabel.Text = _selectedValues.Count == 0
            ? "Totaal: 0 A"
            : $"Totaal: {FormatAmps(total)} A  ({_selectedValues.Count} tekst{(_selectedValues.Count == 1 ? string.Empty : "en")})";

        _clearButton.Enabled = _selectedValues.Count > 0 && _calculation?.MaxDesignCurrentAmps is int;
    }

    private void RefreshAssessment(string? selectionMessage = null)
    {
        if (_calculation?.MaxDesignCurrentAmps is not int maxAllowed)
            return;

        var total = _selectedValues.Sum(x => x.Amps);
        if (_selectedValues.Count == 0)
        {
            SetNeutral(
                $"Maximaal toegestaan: {maxAllowed} A",
                selectionMessage ?? "Nog geen ontwerpstroomteksten geselecteerd.");
            return;
        }

        var fits = total <= maxAllowed + 1e-9;
        var difference = Math.Abs(maxAllowed - total);

        _icon.Text = fits ? "✓" : "✕";
        _icon.ForeColor = fits ? Color.SeaGreen : Color.Firebrick;
        _summary.ForeColor = fits ? Color.SeaGreen : Color.Firebrick;
        _summary.Text = fits
            ? $"PAST — {FormatAmps(total)} A ≤ {maxAllowed} A"
            : $"PAST NIET — {FormatAmps(total)} A > {maxAllowed} A";

        var marginText = fits
            ? $"Marge {FormatAmps(difference)} A."
            : $"Overschrijding {FormatAmps(difference)} A.";

        _details.Text =
            $"{_selectedValues.Count} ontwerpstroomtekst{(_selectedValues.Count == 1 ? string.Empty : "en")} in totaal. {marginText}" +
            (string.IsNullOrWhiteSpace(selectionMessage)
                ? string.Empty
                : Environment.NewLine + selectionMessage);
    }

    private void SetSelectionButtonsEnabled(bool enabled)
    {
        _brushButton.Enabled = enabled;
        _manualButton.Enabled = enabled;
        _radiusMeters.Enabled = enabled;
        _clearButton.Enabled = enabled && _selectedValues.Count > 0;
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
