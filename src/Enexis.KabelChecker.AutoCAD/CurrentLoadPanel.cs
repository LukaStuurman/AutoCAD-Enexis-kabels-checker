using System.Globalization;
using Enexis.KabelChecker.Core;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;

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
    private readonly Dictionary<ObjectId, double> _selectedTextObjects = new();
    private CalculationResult? _calculation;
    private bool _refreshingOverview;

    public CurrentLoadPanel()
    {
        AutoSize = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 2, 0, 4);
        BorderStyle = BorderStyle.FixedSingle;
        BuildUi();
        RefreshOverview();
        SetCalculation(null);
    }

    public void SetCalculation(CalculationResult? calculation)
    {
        _calculation = calculation;
        RefreshAssessment();
        UpdateRemoveButtonState();
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
            Text = "Cirkel: klik = toevoegen, Shift+klik = eerder geselecteerde teksten binnen de cirkel deselecteren. Ontwerpstroom mag ook vóór de kabelberekening.",
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
        _overview.SelectionMode = DataGridViewSelectionMode.CellSelect;
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
        var result = TextCurrentBrushSelection.Read(
            (double)_radiusMeters.Value,
            _selectedTextObjects.Keys.ToArray());
        ApplyBrushSelection(result);
    }

    private void SelectManually()
    {
        AddObjectSelection(TextCurrentManualSelection.Read());
    }

    private void ApplyBrushSelection(TextCurrentBrushSelectionResult selection)
    {
        if (selection.Cancelled)
        {
            _details.Text = selection.Message;
            return;
        }

        var removed = 0;
        foreach (var value in selection.RemovedValues)
        {
            if (!_selectedTextObjects.Remove(value.ObjectId, out var mappedAmps))
                continue;

            DecrementRow(mappedAmps);
            removed++;
        }

        var added = 0;
        foreach (var value in selection.AddedValues)
        {
            if (_selectedTextObjects.ContainsKey(value.ObjectId))
                continue;

            _selectedTextObjects[value.ObjectId] = value.Amps;
            IncrementRow(value.Amps);
            added++;
        }

        NormalizeRows();
        RefreshOverview();
        RefreshAssessment(
            $"Cirkel: {added} toegevoegd, {removed} gedeselecteerd." +
            (string.IsNullOrWhiteSpace(selection.Message)
                ? string.Empty
                : Environment.NewLine + selection.Message));
    }

    private void AddObjectSelection(TextCurrentObjectSelectionResult selection)
    {
        if (selection.Cancelled)
        {
            _details.Text = selection.Message;
            return;
        }

        var added = 0;
        var alreadySelected = 0;
        foreach (var value in selection.Values)
        {
            if (_selectedTextObjects.ContainsKey(value.ObjectId))
            {
                alreadySelected++;
                continue;
            }

            _selectedTextObjects[value.ObjectId] = value.Amps;
            IncrementRow(value.Amps);
            added++;
        }

        NormalizeRows();
        RefreshOverview();

        var extra = alreadySelected > 0
            ? Environment.NewLine + $"{alreadySelected} al geselecteerde tekstobject(en) niet dubbel toegevoegd."
            : string.Empty;
        RefreshAssessment(selection.Message + extra);
    }

    private void IncrementRow(double amps)
    {
        var existing = _rows.FirstOrDefault(x => SameAmps(x.Amps, amps));
        if (existing is null)
            _rows.Add(new CurrentLoadRow(amps, 1));
        else
            existing.Count++;
    }

    private void DecrementRow(double amps)
    {
        var existing = _rows.FirstOrDefault(x => SameAmps(x.Amps, amps));
        if (existing is null)
            return;

        existing.Count--;
        if (existing.Count <= 0)
            _rows.Remove(existing);
    }

    private void RemoveSelectedRow()
    {
        if (_overview.CurrentRow is null)
            return;

        var index = _overview.CurrentRow.Index;
        if (index < 0 || index >= _rows.Count)
            return;

        var row = _rows[index];
        RemoveTrackedObjectsForAmps(row.Amps);
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
            if (!TryParsePositiveAmps(text, out var amps))
                return;

            var oldAmps = row.Amps;
            row.Amps = amps;
            ReassignTrackedObjects(oldAmps, amps);
        }
        else if (columnName == "Count")
        {
            var text = Convert.ToString(_overview.Rows[e.RowIndex].Cells["Count"].Value);
            if (!TryParsePositiveCount(text, out var count))
                return;

            row.Count = count;
            TrimTrackedObjectsForAmps(row.Amps, count);
        }
        else
        {
            return;
        }

        UpdateGridRowInPlace(e.RowIndex);
        UpdateOverviewTotals();
        UpdateRemoveButtonState();
        RefreshAssessment("Ontwerpstroomtabel aangepast.");
    }

    private void UpdateGridRowInPlace(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || rowIndex >= _overview.Rows.Count)
            return;

        var row = _rows[rowIndex];
        _refreshingOverview = true;
        try
        {
            var gridRow = _overview.Rows[rowIndex];
            gridRow.Cells["Amps"].Value = FormatAmps(row.Amps);
            gridRow.Cells["Count"].Value = row.Count.ToString(DutchCulture);
            gridRow.Cells["Subtotal"].Value = FormatAmps(row.Amps * row.Count);
        }
        finally
        {
            _refreshingOverview = false;
        }
    }

    private void ReassignTrackedObjects(double oldAmps, double newAmps)
    {
        var ids = _selectedTextObjects
            .Where(x => SameAmps(x.Value, oldAmps))
            .Select(x => x.Key)
            .ToList();

        foreach (var id in ids)
            _selectedTextObjects[id] = newAmps;
    }

    private void TrimTrackedObjectsForAmps(double amps, int maximumTrackedCount)
    {
        var trackedIds = _selectedTextObjects
            .Where(x => SameAmps(x.Value, amps))
            .Select(x => x.Key)
            .ToList();

        if (trackedIds.Count <= maximumTrackedCount)
            return;

        foreach (var id in trackedIds.Skip(maximumTrackedCount))
            _selectedTextObjects.Remove(id);
    }

    private void RemoveTrackedObjectsForAmps(double amps)
    {
        var ids = _selectedTextObjects
            .Where(x => SameAmps(x.Value, amps))
            .Select(x => x.Key)
            .ToList();

        foreach (var id in ids)
            _selectedTextObjects.Remove(id);
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

        UpdateOverviewTotals();
        UpdateRemoveButtonState();
    }

    private void UpdateOverviewTotals()
    {
        var total = _rows.Sum(x => x.Amps * x.Count);
        var count = _rows.Sum(x => x.Count);
        _totalLabel.Text = count == 0
            ? "Totaal: 0 A"
            : $"Totaal: {FormatAmps(total)} A  ({count}× ontwerpstroom)";
    }

    private void RefreshAssessment(string? selectionMessage = null)
    {
        var count = _rows.Sum(x => x.Count);
        var total = _rows.Sum(x => x.Amps * x.Count);

        if (_calculation is null)
        {
            SetNeutral(
                count == 0 ? "Ontwerpstroom: —" : $"Ontwerpstroom totaal: {FormatAmps(total)} A",
                BuildMessage(
                    selectionMessage,
                    "Nog geen actuele kabelrichting berekend. Je kunt ontwerpstroom eerst opbouwen en later de kabelrichting berekenen."));
            return;
        }

        if (_calculation.MaxDesignCurrentAmps is not int maxAllowed)
        {
            _icon.Text = "✕";
            _icon.ForeColor = Color.Firebrick;
            _summary.ForeColor = Color.Firebrick;
            _summary.Text = count == 0
                ? "Richting heeft geen toegestane ontwerpstroom"
                : $"{FormatAmps(total)} A — richting heeft geen toegestane ontwerpstroom";
            _details.Text = BuildMessage(
                selectionMessage,
                "Voor deze kabelrichting is geen geldige maximale ontwerpstroom beschikbaar.");
            return;
        }

        if (count == 0)
        {
            SetNeutral(
                $"Maximaal toegestaan: {maxAllowed} A",
                BuildMessage(selectionMessage, "Nog geen ontwerpstroom toegevoegd."));
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

        _details.Text = BuildMessage(
            selectionMessage,
            $"{count} ontwerpstroomwaarde{(count == 1 ? string.Empty : "n")} in totaal. {marginText}");
    }

    private void UpdateRemoveButtonState()
    {
        _removeRowButton.Enabled = _rows.Count > 0 && _overview.CurrentRow is not null;
    }

    private void SetNeutral(string summary, string details)
    {
        _icon.Text = "—";
        _icon.ForeColor = SystemColors.GrayText;
        _summary.ForeColor = SystemColors.ControlText;
        _summary.Text = summary;
        _details.Text = details;
    }

    private static string BuildMessage(string? first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return second;
        if (string.IsNullOrWhiteSpace(second))
            return first;
        return first + Environment.NewLine + second;
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

    private static bool SameAmps(double left, double right) => Math.Abs(left - right) <= 1e-9;
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
