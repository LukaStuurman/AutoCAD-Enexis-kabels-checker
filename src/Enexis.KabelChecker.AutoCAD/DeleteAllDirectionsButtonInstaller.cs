using System.Reflection;
using System.Runtime.CompilerServices;

namespace Enexis.KabelChecker.AutoCAD;

internal static class DeleteAllDirectionsButtonInstaller
{
    private const string ButtonName = "DeleteAllDirectionsButton";
    private static readonly ConditionalWeakTable<KabelCheckerForm, FormAttachmentState> AttachedForms = new();

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

            var exportButton = panel.Controls
                .OfType<Button>()
                .FirstOrDefault(button => button.Text == "Excel exporteren");
            if (exportButton is not null)
            {
                ApplyActionButtonStyle(
                    exportButton,
                    Color.ForestGreen,
                    Color.DarkGreen,
                    Color.DarkGreen,
                    Color.SeaGreen);
            }

            var deleteAll = new Button
            {
                Name = ButtonName,
                Text = "Alle richtingen verwijderen",
                AutoSize = true
            };
            ApplyActionButtonStyle(
                deleteAll,
                Color.Firebrick,
                Color.DarkRed,
                Color.DarkRed,
                Color.Maroon);
            deleteAll.Click += (_, _) => DeleteAllDirections(form);

            panel.Controls.Add(deleteAll);
            if (exportButton is not null)
                panel.Controls.SetChildIndex(deleteAll, panel.Controls.GetChildIndex(exportButton));

            var state = new FormAttachmentState();
            var profile = GetPrivateField<ComboBox>(form, "_profile");
            var fuseResult = GetPrivateField<Label>(form, "_fuseResult");
            if (profile is not null && fuseResult is not null)
            {
                profile.SelectedIndexChanged += (_, _) => UpdateFuseProfile(profile, fuseResult, state);
                fuseResult.TextChanged += (_, _) => UpdateFuseProfile(profile, fuseResult, state);
                UpdateFuseProfile(profile, fuseResult, state);
            }

            AttachedForms.Add(form, state);
        }
    }

    private static void ApplyActionButtonStyle(
        Button button,
        Color background,
        Color border,
        Color hover,
        Color pressed)
    {
        button.BackColor = background;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = pressed;
    }

    private static T? GetPrivateField<T>(KabelCheckerForm form, string fieldName)
        where T : class
    {
        var field = typeof(KabelCheckerForm).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(form) as T;
    }

    private static void UpdateFuseProfile(
        ComboBox profile,
        Label fuseResult,
        FormAttachmentState state)
    {
        if (state.UpdatingFuseLabel)
            return;

        var selectedText = Convert.ToString(profile.SelectedItem) ?? string.Empty;
        var profileText = selectedText.StartsWith("Laatste helft", StringComparison.OrdinalIgnoreCase)
            ? "Laatste helft"
            : selectedText.StartsWith("Evenredig", StringComparison.OrdinalIgnoreCase)
                ? "Evenredig"
                : null;
        if (profileText is null)
            return;

        var baseText = fuseResult.Text.Trim();
        foreach (var suffix in new[] { " — Evenredig", " — Laatste helft" })
        {
            if (baseText.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                baseText = baseText[..^suffix.Length].TrimEnd();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(baseText))
            return;

        var desired = $"{baseText} — {profileText}";
        if (string.Equals(fuseResult.Text, desired, StringComparison.Ordinal))
            return;

        state.UpdatingFuseLabel = true;
        try
        {
            fuseResult.Text = desired;
        }
        finally
        {
            state.UpdatingFuseLabel = false;
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

    private sealed class FormAttachmentState
    {
        public bool UpdatingFuseLabel { get; set; }
    }
}
