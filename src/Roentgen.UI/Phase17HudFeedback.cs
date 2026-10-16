using Microsoft.Xna.Framework;
using Roentgen.Contracts;

namespace Roentgen.UI;

public enum HudToastKind
{
    Information,
    Loot,
    Quest,
    Reputation,
    Progression,
    Location,
    Warning,
    Error
}

/// <summary>
/// Presentation-only HUD notification. Lifetime is expressed in interface frames so pauses do not
/// freeze or duplicate transient feedback and no simulation authority leaks into the UI layer.
/// </summary>
public sealed record HudToast(
    string Id,
    string Title,
    string Body,
    HudToastKind Kind,
    int CreatedInterfaceFrame,
    int LifetimeFrames = 270)
{
    public int AgeFrames(int interfaceFrame) => Math.Max(0, interfaceFrame - CreatedInterfaceFrame);
    public bool IsExpired(int interfaceFrame) => AgeFrames(interfaceFrame) >= Math.Max(1, LifetimeFrames);
}

public sealed record HudWorldFeedbackModel(IReadOnlyList<HudToast> Toasts)
{
    public static HudWorldFeedbackModel Empty { get; } = new(Array.Empty<HudToast>());
}

public sealed record InteractionPromptHudModel(
    string TargetName,
    string ActionLabel,
    string BindingLabel,
    string? SecondaryLine,
    bool Enabled,
    int DistanceMillimeters,
    int HoldDurationTicks);

public static class HudInteractionFormatter
{
    public static InteractionPromptHudModel? Build(InteractionTargetDescriptor? target, Phase20UserSettings settings, UiInputDevice inputDevice)
    {
        if (target is null || target.Options.Count == 0) return null;
        InteractionOption option = target.Options
            .OrderByDescending(static value => value.Enabled)
            .ThenByDescending(static value => value.Priority)
            .First();

        string action = Humanize(string.IsNullOrWhiteSpace(option.PromptKey) ? option.Verb.ToString() : option.PromptKey);
        string binding = UiBindingFormatter.BindingLabel(settings, UiAction.Interact, inputDevice).ToUpperInvariant();
        string? secondary = option.Enabled
            ? option.HoldDurationTicks > 0 ? $"Hold {binding} to {action.ToLowerInvariant()}" : null
            : FailureText(option.FailureReason, target.DistanceMillimeters, target.Occluded);
        return new InteractionPromptHudModel(
            Humanize(target.DisplayName),
            action,
            binding,
            secondary,
            option.Enabled,
            target.DistanceMillimeters,
            option.HoldDurationTicks);
    }

    public static string FailureText(InteractionFailureReason reason, int distanceMillimeters, bool occluded) => reason switch
    {
        InteractionFailureReason.OutOfRange => $"Too far away • {distanceMillimeters / 1000.0:F1} m",
        InteractionFailureReason.Occluded => "Line of sight blocked",
        InteractionFailureReason.Locked => "Locked",
        InteractionFailureReason.Jammed => "Jammed",
        InteractionFailureReason.Barricaded => "Barricaded",
        InteractionFailureReason.Blocked => "Blocked",
        InteractionFailureReason.Unloaded => "Requires power or ammunition",
        InteractionFailureReason.Empty => "Empty",
        InteractionFailureReason.InvalidState => "Cannot interact right now",
        _ when occluded => "Line of sight blocked",
        _ => "Unavailable"
    };

    public static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string text = value.Trim().Replace('_', ' ').Replace('-', ' ');
        while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ", StringComparison.Ordinal);
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(static part =>
            part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }
}

public sealed record HudLayoutMetrics(
    Rectangle SafeArea,
    Rectangle HealthArea,
    Rectangle WeaponArea,
    Rectangle QuestArea,
    Rectangle ToastArea,
    Rectangle InteractionArea,
    float EffectiveScale);

/// <summary>Safe-area aware HUD placement for 16:9, ultrawide, narrow windows, and scaled interfaces.</summary>
public static class HudResponsiveLayout
{
    public static HudLayoutMetrics Compute(int width, int height, float interfaceScale)
    {
        float scale = Math.Clamp(interfaceScale, 0.75f, 2f);
        int inset = Math.Clamp((int)MathF.Round(Math.Min(width, height) * 0.025f * MathF.Min(scale, 1.35f)), 10, 52);
        Rectangle safe = new(
            Math.Min(inset, Math.Max(0, width / 4)),
            Math.Min(inset, Math.Max(0, height / 4)),
            Math.Max(1, width - inset * 2),
            Math.Max(1, height - inset * 2));

        int bottomHeight = Math.Clamp((int)MathF.Round(74 * scale), 56, Math.Max(56, safe.Height / 3));
        int healthWidth = Math.Clamp((int)MathF.Round(250 * scale), 210, Math.Max(210, safe.Width / 2));
        int weaponWidth = Math.Clamp((int)MathF.Round(430 * scale), 280, Math.Max(280, safe.Width / 2));
        Rectangle health = new(safe.X, Math.Max(safe.Y, safe.Bottom - bottomHeight), Math.Min(healthWidth, safe.Width), bottomHeight);
        Rectangle weapon = new(Math.Max(safe.X, safe.Right - Math.Min(weaponWidth, safe.Width)), health.Y, Math.Min(weaponWidth, safe.Width), bottomHeight);

        int sideWidth = Math.Clamp((int)(safe.Width * 0.34f), 280, Math.Min(560, Math.Max(280, safe.Width)));
        Rectangle quest = new(Math.Max(safe.X, safe.Right - sideWidth), safe.Y, Math.Min(sideWidth, safe.Width), Math.Clamp((int)(86 * scale), 72, 132));
        int toastTop = quest.Bottom + Math.Clamp((int)(10 * scale), 8, 18);
        Rectangle toasts = new(quest.X, toastTop, quest.Width, Math.Max(1, health.Y - toastTop - 12));

        int interactionWidth = Math.Clamp((int)(620 * scale), 300, Math.Max(300, safe.Width));
        int interactionHeight = Math.Clamp((int)(72 * scale), 54, 116);
        Rectangle interaction = new(
            safe.Center.X - Math.Min(interactionWidth, safe.Width) / 2,
            Math.Max(safe.Y, health.Y - interactionHeight - Math.Clamp((int)(18 * scale), 10, 28)),
            Math.Min(interactionWidth, safe.Width),
            interactionHeight);
        return new HudLayoutMetrics(safe, health, weapon, quest, toasts, interaction, scale);
    }
}
