namespace Roentgen.UI;

/// <summary>
/// Final player-facing consistency pass for Phase 20-23 UI models.  This deliberately operates on
/// presentation models only: simulation identifiers remain available to authoritative systems but
/// never become the default way the interface explains itself to the player.
/// </summary>
public static class UiUxFinalization
{
    public static Phase20ScreenModel Normalize(Phase20ScreenModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Phase20MenuEntry[] entries = model.Entries.Select(NormalizeEntry).ToArray();
        Phase20MenuEntry[]? secondary = model.SecondaryEntries?.Select(NormalizeEntry).ToArray();
        int selected = entries.Length == 0 ? 0 : Math.Clamp(model.SelectedIndex, 0, entries.Length - 1);
        int secondarySelected = secondary is null || secondary.Length == 0 ? 0 : Math.Clamp(model.SecondarySelectedIndex, 0, secondary.Length - 1);
        IReadOnlyList<Phase20ActionPrompt>? prompts = NormalizePrompts(model.ActionPrompts);
        Phase20Panel? detail = model.Detail is null ? null : model.Detail with
        {
            Title = CleanText(model.Detail.Title),
            Lines = model.Detail.Lines.Select(CleanText).Where(static line => !string.IsNullOrWhiteSpace(line)).ToArray(),
            Status = string.IsNullOrWhiteSpace(model.Detail.Status) ? null : CleanText(model.Detail.Status!)
        };

        return model with
        {
            Title = CleanText(model.Title),
            Subtitle = CleanText(model.Subtitle),
            Entries = entries,
            SelectedIndex = selected,
            SecondaryEntries = secondary,
            SecondarySelectedIndex = secondarySelected,
            Detail = detail,
            Status = CleanText(model.Status),
            // Once semantic action prompts exist, legacy keyboard-chord footers are intentionally
            // suppressed.  The action bar is binding-aware for keyboard, mouse and controller.
            Footer = prompts is { Count: > 0 } ? string.Empty : CleanLegacyFooter(model.Footer),
            ActionPrompts = prompts
        };
    }

    public static int GridColumns(Phase20ScreenPresentation presentation) => presentation switch
    {
        Phase20ScreenPresentation.CardGridTwoColumns => 2,
        Phase20ScreenPresentation.CardGridThreeColumns => 3,
        _ => 1
    };

    public static bool IsAdjustableEntry(Phase20MenuEntry entry) => entry.Presentation is
        Phase20EntryPresentation.Toggle or Phase20EntryPresentation.Slider or
        Phase20EntryPresentation.Stepper or Phase20EntryPresentation.Dropdown;

    private static Phase20MenuEntry NormalizeEntry(Phase20MenuEntry entry)
    {
        string label = CleanText(entry.Label);
        string? value = string.IsNullOrWhiteSpace(entry.Value) ? null : CleanText(entry.Value!);
        string? description = string.IsNullOrWhiteSpace(entry.Description) ? null : CleanText(entry.Description!);
        string? badge = string.IsNullOrWhiteSpace(entry.Badge) ? null : CleanText(entry.Badge!);
        if (!entry.Enabled && string.IsNullOrWhiteSpace(description)) description = "Unavailable right now.";
        if (entry.Dangerous && string.IsNullOrWhiteSpace(description)) description = "This action requires confirmation.";
        return entry with { Label = label, Value = value, Description = description, Badge = badge };
    }

    private static IReadOnlyList<Phase20ActionPrompt>? NormalizePrompts(IReadOnlyList<Phase20ActionPrompt>? prompts)
    {
        if (prompts is null || prompts.Count == 0) return prompts;
        var seen = new HashSet<UiAction>();
        var normalized = new List<Phase20ActionPrompt>(prompts.Count);
        foreach (Phase20ActionPrompt prompt in prompts)
        {
            if (!seen.Add(prompt.Action)) continue;
            normalized.Add(prompt with
            {
                Label = CleanText(prompt.Label),
                Tooltip = string.IsNullOrWhiteSpace(prompt.Tooltip) ? null : CleanText(prompt.Tooltip!)
            });
        }

        // Keep navigation/destructive actions predictable across every screen.  Cancel is always
        // the right-most semantic escape action and destructive operations sit immediately before it.
        Phase20ActionPrompt? cancel = normalized.FirstOrDefault(static value => value.Action == UiAction.Cancel);
        normalized.RemoveAll(static value => value.Action == UiAction.Cancel);
        var dangerous = normalized.Where(static value => value.Dangerous).ToArray();
        normalized.RemoveAll(static value => value.Dangerous);
        normalized.AddRange(dangerous);
        if (cancel is not null) normalized.Add(cancel);
        return normalized;
    }

    private static string CleanLegacyFooter(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string upper = value.ToUpperInvariant();
        if (upper.Contains("ENTER ", StringComparison.Ordinal) || upper.Contains("ESC ", StringComparison.Ordinal) ||
            upper.Contains("TAB ", StringComparison.Ordinal) || upper.Contains("DELETE ", StringComparison.Ordinal))
            return string.Empty;
        return CleanText(value);
    }

    public static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string text = value.Trim();
        while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ", StringComparison.Ordinal);
        // Raw enum/definition keys are humanized when they accidentally reach presentation models.
        if (!text.Contains(' ') && (text.Contains('_') || text.Contains('-')) && text.Length < 64)
            text = HudInteractionFormatter.Humanize(text);
        return text;
    }
}
