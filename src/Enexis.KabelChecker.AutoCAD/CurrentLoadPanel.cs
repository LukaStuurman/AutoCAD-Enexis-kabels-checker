using System.Globalization;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class CurrentLoadPanel : UserControl
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    private readonly Button _brushButton = new();
    private readonly Button _manualButton = new();
    private readonly Button _removeRowButton = new();
    private readonly NumericUpDown _radiusMeters = new();
    private readonly DataGridView _overview = new();
    private readonly Label _totalLabel = new();
    private readonly Label _summary = new();
    private readonly Label _details = new();
    private readonly Label _icon = new();
    private readonly List<CurrentLoadRow> _rows = new();
    private CalculationResult? _calculation;
    private bool _refreshingOverview;
    private bool _overviewRefreshQueued;

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
        _rows.Clear();
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
            _details.Text = "Voor deze kabelrichting is geen geldige ontwerpstroom beschikbaar.";
            return;
        }

        SetSelectionButtonsEnabled(true);
        SetNeutral(
            $"Maximaal toegestaan: {calculation.MaxDesignCurrentAmps.Value} A",
            "Voeg teksten toe met Cirkel of Handmatig. Ontwerpstroom en aantal zijn daarna direct in de tabel aanpasbaar.");
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
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
            Text = "Ontwerpstroom uit tekst",
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

        _removeRowButton.Text = "Verwijder rij";
        _removeRowButton.AutoSize = true;
        _removeRowButton.Padding = new Padding(4, 1, 4, 1);
        _removeRowButton.Margin = new Padding(0);
        _removeRowButton.Click += (_, _) => RemoveSelectedRow();
        selectionButtons.Controls.Add(_removeRowButton);
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
            Text = "Cirkel: één klik voegt alle geldige TEXT/MTEXT binnen de cirkel toe. Handmatig blijft ook beschikbaar.",
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

        ConfigureOverviewGrid();
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

    private void ConfigureOverviewGrid()
    {
        _overview.AllowUserToAddRows = false;
        _overview.AllowUserToDeleteRows = false;
        _overview.AllowUserToResizeRows = false;
        _overview.ReadOnly = false;
        _overview.EditMode = DataGridViewEditMode.EditOnEnter;
        _overview.RowHeadersVisible = false;
        _overview.MultiSelect = false;
        _overview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _overview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _overview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _overview.ColumnHeadersHeight = 23;
        _overview.RowTemplate.Height = 21;
        _overview.Height = 106;
        _overview.Dock = DockStyle.Fill;
        _overview.Margin = new Padding(0);
        _overview.ScrollBars = ScrollBars.Vertical;
        _overview.BackgroundColor = SystemColors.Window;
        _overview.BorderStyle = BorderStyle.FixedSingle;

        _overview.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Amps",
            HeaderText = "Ontwerpstroom [A]",
            ReadOnly = false,
            FillWeight = 115,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _overview.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Count",
            HeaderText = "Aantal",
            ReadOnly = false,
            FillWeight = 65,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _overview.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Subtotal",
            HeaderText = "Subtotaal [A]",
            ReadOnly = true,
            FillWeight = 95,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _overview.CellValidating += Overview_CellValidating;
        _overview.CellEndEdit += Overview_CellEndEdit;
        _overview.SelectionChanged += (_, _) => UpdateRemoveButtonState();
        _overview.DataError += (_, e) => e.ThrowException = false;
    }

    private void SelectWithBrush()
    {
        if (!CanSelectCurrents())
            return;
        AddSelection(TextCurrentBrushSelection.Read((double)_radiusMeters.Value));
    }

    private void SelectManually()
    {
        if (!CanSelectCurrents())
            return;
        AddSelection(AutoCadSelectionReader.ReadSelectedTextCurrents(TextCurrentSelectionMode.Manual));
    }

    private bool CanSelectCurrents()
    {
        if (_calculation?.MaxDesignCurrentAmps is int)
            return true;

        MessageBox.Show(this, "Bereken eerst de kabelrichting zodat de maximale ontwerpstroom bekend is.", "Eerst richting berekenen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private void AddSelection(TextCurrentSelectionResult selection)
    {
        if (selection.Cancelled)
        {
            _details.Text = selection.Message;
            return;
        }

        foreach (var value in selection.Values)
        {
            var existing = _rows.FirstOrDefault(x => Math.Abs(x.Amps - value.Amps) <= 1e-9);
            if (existing is null)
                _rows.Add(new CurrentLoadRow(value.Amps, 1));
            else
                existing.Count++;
        }

        NormalizeRows();
        RefreshOverview();
        RefreshAssessment(selection.Message);
    }

    private void RemoveSelectedRow()
    {
        if (_overview.CurrentRow is null)
            return;

        var index = _overview.CurrentRow.Index;
        if (index < 0 || index >= _rows.Count)
            return;

        _rows.RemoveAt(index);
        RefreshOverview();
        RefreshAssessment("Ontwerpstroomrij verwijderd.");
    }

    private void Overview_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_refreshingOverview || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var columnName = _overview.Columns[e.ColumnIndex].Name;
        var text = Convert.ToString(e.FormattedValue);

        if (columnName == "Amps" && !TryParsePositiveAmps(text, out _))
        {
            e.Cancel = true;
            _details.Text = "Ontwerpstroom moet een positief getal zijn. Zowel 25,5 als 25.5 wordt geaccepteerd.";
        }
        else if (columnName == "Count" && !TryParsePositiveCount(text, out _))
        {
            e.Cancel = true;
            _details.Text = "Aantal moet een positief geheel getal zijn.";
        }
    }

    private void Overview_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_refreshingOverview || e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.ColumnIndex < 0)
            return;

        var row = _rows[e.RowIndex];
        var columnName = _overview.Columns[e.ColumnIndex].Name;

        if (columnName == "Amps")
        {
            var text = Convert.ToString(_overview.Rows[e.RowIndex].Cells["Amps"].Value);
            if (TryParsePositiveAmps(text, out var amps))
                row.Amps = amps;
        }
        else if (columnName == "Count")
        {
            var text = Convert.ToString(_overview.Rows[e.RowIndex].Cells["Count"].Value);
            if (TryParsePositiveCount(text, out var count))
                row.Count = count;
        }
        else
        {
            return;
        }

        NormalizeRows();
        QueueOverviewRefresh("Ontwerpstroomtabel aangepast.");
    }

    private void QueueOverviewRefresh(string assessmentMessage)
    {
        if (_overviewRefreshQueued || IsDisposed || Disposing)
            return;

        _overviewRefreshQueued = true;
        BeginInvoke(new Action(() =>
        {
            _overviewRefreshQueued = false;
            if (IsDisposed || Disposing)
                return;

            RefreshOverview();
            RefreshAssessment(assessmentMessage);
        }));
    }

    private void NormalizeRows()
    {
        if (_rows.Count <= 1)
            return;

        var normalized = _rows
            .GroupBy(x => x.Amps)
            .Select(x => new CurrentLoadRow(x.Key, x.Sum(y => y.Count)))
            .OrderBy(x => x.Amps)
            .ToList();

        _rows.Clear();
        _rows.AddRange(normalized);
    }

    private void RefreshOverview()
    {
        _refreshingOverview = true;
        try
        {
            _overview.Rows.Clear();
            foreach (var row in _rows)
            {
                _overview.Rows.Add(
                    FormatAmps(row.Amps),
                    row.Count.ToString(DutchCulture),
                    FormatAmps(row.Amps * row.Count));
            }
        }
        finally
        {
            _refreshingOverview = false;
        }

        var total = _rows.Sum(x => x.Amps * x.Count);
        var count = _rows.Sum(x => x.Count);
        _totalLabel.Text = count == 0
            ? "Totaal: 0 A"
            : $"Totaal: {FormatAmps(total)} A  ({count}× ontwerpstroom)";
        UpdateRemoveButtonState();
    }

    private void RefreshAssessment(string? selectionMessage = null)
    {
        if (_calculation?.MaxDesignCurrentAmps is not int maxAllowed)
            return;

        var count = _rows.Sum(x => x.Count);
        var total = _rows.Sum(x => x.Amps * x.Count);
        if (count == 0)
        {
            SetNeutral(
                $"Maximaal toegestaan: {maxAllowed} A",
                selectionMessage ?? "Nog geen ontwerpstroom toegevoegd.");
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
            $"{count} ontwerpstroomwaarde{(count == 1 ? string.Empty : "n")} in totaal. {marginText}" +
            (string.IsNullOrWhiteSpace(selectionMessage)
                ? string.Empty
                : Environment.NewLine + selectionMessage);
    }

    private void SetSelectionButtonsEnabled(bool enabled)
    {
        _brushButton.Enabled = enabled;
        _manualButton.Enabled = enabled;
        _radiusMeters.Enabled = enabled;
        UpdateRemoveButtonState();
    }

    private void UpdateRemoveButtonState()
    {
        _removeRowButton.Enabled =
            _calculation?.MaxDesignCurrentAmps is int &&
            _rows.Count > 0 &&
            _overview.CurrentRow is not null;
    }

    private void SetNeutral(string summary, string details)
    {
        _icon.Text = "—";
        _icon.ForeColor = SystemColors.GrayText;
        _summary.ForeColor = SystemColors.ControlText;
        _summary.Text = summary;
        _details.Text = details;
    }

    private static bool TryParsePositiveAmps(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var parsed =
            double.TryParse(trimmed, NumberStyles.Float, DutchCulture, out value) ||
            double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        return parsed && value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool TryParsePositiveCount(string? text, out int value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(text) &&
               int.TryParse(text.Trim(), NumberStyles.Integer, DutchCulture, out value) &&
               value > 0;
    }

    private static string FormatAmps(double value) => value.ToString("0.##", DutchCulture);

    private sealed class CurrentLoadRow
    {
        public CurrentLoadRow(double amps, int count)
        {
            Amps = amps;
            Count = count;
        }

        public double Amps { get; set; }
        public int Count { get; set; }
    }
}
