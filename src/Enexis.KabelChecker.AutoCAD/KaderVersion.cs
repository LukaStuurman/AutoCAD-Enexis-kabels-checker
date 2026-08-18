namespace Enexis.KabelChecker.AutoCAD;

internal enum KaderVersion
{
    K2024_1_0,
    K2025_2_0,
    K2026_3_2
}

internal sealed record KaderVersionDefinition(
    KaderVersion Version,
    string DisplayName,
    string ResourceFileName);

internal static class KaderVersions
{
    public static IReadOnlyList<KaderVersionDefinition> All { get; } = new[]
    {
        new KaderVersionDefinition(KaderVersion.K2024_1_0, "2024 — Eea-0205.K 1.0", "Eea-0205.K 1.0 - Copy.xlsx"),
        new KaderVersionDefinition(KaderVersion.K2025_2_0, "2025 — Eea-0205.K 2.0", "Eea-0205.K_2.0.xlsx"),
        new KaderVersionDefinition(KaderVersion.K2026_3_2, "2026 — Eea-0205.K 3.2", "Eea-0205.K 3.2.xlsx")
    };

    public static KaderVersionDefinition Get(KaderVersion version) =>
        All.Single(x => x.Version == version);
}

internal static class KaderVersionSelection
{
    public static KaderVersion? Current { get; private set; }

    public static KaderVersion EnsureSelected(IWin32Window? owner)
    {
        if (Current is KaderVersion selected)
            return selected;

        Current = ShowSelector(owner, KaderVersion.K2026_3_2);
        return Current.Value;
    }

    public static KaderVersion SelectForExport(IWin32Window? owner)
    {
        var initial = Current ?? KaderVersion.K2026_3_2;
        Current = ShowSelector(owner, initial);
        return Current.Value;
    }

    private static KaderVersion ShowSelector(IWin32Window? owner, KaderVersion initial)
    {
        using var dialog = new Form
        {
            Text = "Kader versie",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            Padding = new Padding(10)
        };

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(4)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = "Kader versie:",
            AutoSize = true,
            Padding = new Padding(0, 6, 10, 0)
        }, 0, 0);

        var picker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 270
        };
        foreach (var definition in KaderVersions.All)
            picker.Items.Add(new VersionItem(definition));
        picker.SelectedIndex = Math.Max(0, KaderVersions.All.ToList().FindIndex(x => x.Version == initial));
        root.Controls.Add(picker, 1, 0);

        var explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Margin = new Padding(0, 10, 0, 8),
            Text = "De gekozen kaderversie bepaalt zowel de ontwerpstroom-koppeling als de Excel-template en de cellen die worden ingevuld."
        };
        root.Controls.Add(explanation, 0, 1);
        root.SetColumnSpan(explanation, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var ok = new Button { Text = "Gebruiken", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 2);
        root.SetColumnSpan(buttons, 2);

        dialog.Controls.Add(root);
        dialog.AcceptButton = ok;

        if (owner is null)
            dialog.ShowDialog();
        else
            dialog.ShowDialog(owner);

        return picker.SelectedItem is VersionItem item ? item.Definition.Version : initial;
    }

    private sealed record VersionItem(KaderVersionDefinition Definition)
    {
        public override string ToString() => Definition.DisplayName;
    }
}
