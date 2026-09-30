using System.Globalization;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed class KaderLoadPicker : Form
{
    private readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown _count = new() { Minimum = 1, Maximum = 100000, Value = 1, Dock = DockStyle.Fill };
    private readonly Label _values = new() { AutoSize = true, Dock = DockStyle.Fill };
    public ExcelLoadOption? SelectedOption => (_type.SelectedItem as OptionItem)?.Option;
    public int Count => Decimal.ToInt32(_count.Value);

    public KaderLoadPicker(KaderVersion version)
    {
        Text = "Aansluiting uit " + KaderVersions.Get(version).DisplayName;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 210);
        MinimumSize = new Size(600, 250);
        Font = new Font("Segoe UI", 9F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Aansluittype", AutoSize = true }, 0, 0);
        layout.Controls.Add(_type, 1, 0);
        layout.Controls.Add(new Label { Text = "Aantal", AutoSize = true }, 0, 1);
        layout.Controls.Add(_count, 1, 1);
        layout.Controls.Add(_values, 0, 2);
        layout.SetColumnSpan(_values, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var add = new Button { Text = "Toevoegen", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(add);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = add;
        CancelButton = cancel;
        _type.SelectedIndexChanged += (_, _) =>
        {
            if (SelectedOption is not { } option) return;
            var culture = CultureInfo.GetCultureInfo("nl-NL");
            string F(double value) => value.ToString("0.###", culture);
            _values.Text = $"Bijdrage per aansluiting: kabel verbruik {F(option.CableConsumptionAmps * option.CablePhaseFactor)} A / opwek {F(option.CableGenerationAmps * option.CablePhaseFactor)} A.\n" +
                $"Trafo: verbruik {F(option.StationConsumptionAmps)} A / opwek {F(option.StationGenerationAmps)} A.";
        };
        foreach (var option in ExcelLoadCatalog.For(version)) _type.Items.Add(new OptionItem(option));
        _type.SelectedIndex = 0;
    }

    private sealed record OptionItem(ExcelLoadOption Option)
    {
        public override string ToString() => Option.DisplayName;
    }
}
