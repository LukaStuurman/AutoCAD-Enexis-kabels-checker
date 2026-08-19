namespace Enexis.KabelChecker.AutoCAD;

internal static class ExcelLoadResolver
{
    public static IReadOnlyList<ExcelMappedLoad>? Resolve(
        IWin32Window owner,
        IReadOnlyList<CurrentLoadInput> currentLoads,
        IReadOnlyList<ExcelMappedLoad>? existing = null)
    {
        var version = KaderVersionSelection.EnsureSelected(owner);
        return Resolve(owner, currentLoads, version, existing);
    }

    public static IReadOnlyList<ExcelMappedLoad>? Resolve(
        IWin32Window? owner,
        IReadOnlyList<CurrentLoadInput> currentLoads,
        KaderVersion version,
        IReadOnlyList<ExcelMappedLoad>? existing = null)
    {
        var resolved = new List<ExcelMappedLoad>();

        foreach (var load in currentLoads)
        {
            var matches = ExcelLoadCatalog.FindByAmps(version, load.Amps);
            if (matches.Count == 0)
            {
                var versionName = KaderVersions.Get(version).DisplayName;
                ShowMessage(
                    owner,
                    $"Ontwerpstroom {load.Amps:0.##} A komt niet voor in de invoertabel van {versionName}.",
                    "Geen Excel-koppeling",
                    MessageBoxIcon.Warning);
                return null;
            }

            if (matches.Count == 1)
            {
                resolved.Add(new ExcelMappedLoad(matches[0].Key, load.Amps, load.Count));
                continue;
            }

            var reusable = existing?
                .Where(x => Math.Abs(x.Amps - load.Amps) <= 1e-9 && matches.Any(m => m.Key.Equals(x.ExcelLoadKey, StringComparison.OrdinalIgnoreCase)))
                .ToArray() ?? Array.Empty<ExcelMappedLoad>();
            if (reusable.Sum(x => x.Count) == load.Count)
            {
                resolved.AddRange(reusable.Select(x => new ExcelMappedLoad(x.ExcelLoadKey, x.Amps, x.Count)));
                continue;
            }

            using var dialog = new ExcelLoadDistributionDialog(load, matches, KaderVersions.Get(version).DisplayName);
            var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            if (result != DialogResult.OK)
                return null;

            resolved.AddRange(dialog.Result);
        }

        return resolved;
    }

    private static void ShowMessage(IWin32Window? owner, string text, string title, MessageBoxIcon icon)
    {
        if (owner is null)
            MessageBox.Show(text, title, MessageBoxButtons.OK, icon);
        else
            MessageBox.Show(owner, text, title, MessageBoxButtons.OK, icon);
    }
}

internal sealed class ExcelLoadDistributionDialog : Form
{
    private readonly CurrentLoadInput _load;
    private readonly IReadOnlyList<ExcelLoadOption> _options;
    private readonly List<NumericUpDown> _counts = new();

    public IReadOnlyList<ExcelMappedLoad> Result { get; private set; } = Array.Empty<ExcelMappedLoad>();

    public ExcelLoadDistributionDialog(CurrentLoadInput load, IReadOnlyList<ExcelLoadOption> options, string? versionName = null)
    {
        _load = load;
        _options = options;
        Text = $"Verdeel {load.Count} × {load.Amps:0.##} A";
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            Padding = new Padding(10),
            ColumnCount = 2,
            RowCount = options.Count + 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Text = $"{load.Amps:0.##} A komt meerdere keren voor{(string.IsNullOrWhiteSpace(versionName) ? string.Empty : $" in {versionName}")}. Verdeel het totale aantal {load.Count} over de juiste invoerrijen."
        };
        root.Controls.Add(intro, 0, 0);
        root.SetColumnSpan(intro, 2);

        for (var i = 0; i < options.Count; i++)
        {
            root.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                Text = options[i].DisplayName,
                Padding = new Padding(0, 6, 10, 0)
            }, 0, i + 1);

            var count = new NumericUpDown
            {
                Minimum = 0,
                Maximum = load.Count,
                Width = 70
            };
            if (i == 0)
                count.Value = load.Count;
            _counts.Add(count);
            root.Controls.Add(count, 1, i + 1);
        }

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        var cancel = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "Opslaan", AutoSize = true };
        ok.Click += (_, _) => AcceptDistribution();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, options.Count + 1);
        root.SetColumnSpan(buttons, 2);

        Controls.Add(root);
        CancelButton = cancel;
        AcceptButton = ok;
    }

    private void AcceptDistribution()
    {
        var total = _counts.Sum(x => (int)x.Value);
        if (total != _load.Count)
        {
            MessageBox.Show(
                this,
                $"De verdeling is nu {total}; dit moet precies {_load.Count} zijn.",
                "Aantal klopt niet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Result = _options
            .Select((option, index) => new ExcelMappedLoad(option.Key, _load.Amps, (int)_counts[index].Value))
            .Where(x => x.Count > 0)
            .ToArray();
        DialogResult = DialogResult.OK;
        Close();
    }
}
