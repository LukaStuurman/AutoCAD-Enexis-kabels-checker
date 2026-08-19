using System.Reflection;
using System.Runtime.CompilerServices;

namespace Enexis.KabelChecker.AutoCAD;

internal static class DeleteAllDirectionsButtonInstaller
{
    private const string ButtonName = "DeleteAllDirectionsButton";
    private static readonly ConditionalWeakTable<KabelCheckerForm, object> AttachedForms = new();

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += (_, _) => AttachToOpenForms();
    }

    private static void AttachToOpenForms()
    {
        foreach (var form in Application.OpenForms.Cast<Form>().OfType<KabelCheckerForm>().ToArray())
        {
            if (AttachedForms.TryGetValue(form, out _))
                continue;

            var panel = FindDirectionPanel(form);
            if (panel is null)
                continue;

            var deleteAll = new Button
            {
                Name = ButtonName,
                Text = "Alle richtingen verwijderen",
                AutoSize = true,
                BackColor = Color.Firebrick,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            deleteAll.FlatAppearance.BorderColor = Color.DarkRed;
            deleteAll.FlatAppearance.MouseOverBackColor = Color.DarkRed;
            deleteAll.FlatAppearance.MouseDownBackColor = Color.Maroon;
            deleteAll.Click += (_, _) => DeleteAllDirections(form);

            var exportButton = panel.Controls
                .OfType<Button>()
                .FirstOrDefault(button => button.Text == "Excel exporteren");

            panel.Controls.Add(deleteAll);
            if (exportButton is not null)
                panel.Controls.SetChildIndex(deleteAll, panel.Controls.GetChildIndex(exportButton));

            AttachedForms.Add(form, new object());
        }
    }

    private static FlowLayoutPanel? FindDirectionPanel(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is FlowLayoutPanel panel)
            {
                var buttons = panel.Controls.OfType<Button>().ToArray();
                if (buttons.Any(button => button.Text == "Nieuwe richting") &&
                    buttons.Any(button => button.Text == "Richting opslaan") &&
                    buttons.Any(button => button.Text == "Excel exporteren"))
                {
                    return panel;
                }
            }

            var nested = FindDirectionPanel(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static void DeleteAllDirections(KabelCheckerForm form)
    {
        var directions = DirectionStore.Instance.Directions.ToArray();
        if (directions.Length == 0)
        {
            MessageBox.Show(
                form,
                "Er zijn geen opgeslagen richtingen om te verwijderen.",
                "Geen richtingen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(
            form,
            $"Alle {directions.Length} opgeslagen richting(en) verwijderen?\n\n" +
            "Dit wist de huidige richtingenset. Een eerder opgeslagen station wordt pas aangepast als je daarna 'Station opslaan' kiest.",
            "Alle richtingen verwijderen",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        foreach (var direction in directions)
            DirectionStore.Instance.Delete(direction.Number);

        try
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(KabelCheckerForm).GetMethod("StartNewDirection", flags)?.Invoke(form, null);
            typeof(KabelCheckerForm).GetMethod("RefreshSavedDirections", flags)?.Invoke(form, new object?[] { null });
        }
        catch (TargetInvocationException ex)
        {
            MessageBox.Show(
                form,
                ex.InnerException?.Message ?? ex.Message,
                "Richtingen verwijderd, venster kon niet volledig worden vernieuwd",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
