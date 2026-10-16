using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Roentgen.UI;

/// <summary>Last physical input family used by the player. UI presentation follows this automatically.</summary>
public enum UiInputDevice { Keyboard, Mouse, Gamepad }

public enum UiMouseButton { None, Left, Right, Middle, XButton1, XButton2 }

/// <summary>Semantic widget categories used by the shared hit-test/focus layer.</summary>
public enum UiControlKind
{
    Button, TabButton, ListItem, Card, ScrollPane, Scrollbar, Slider, Toggle,
    Stepper, Dropdown, TextBox, ProgressBar, ActionButton, Modal, MapViewport, MapMarker
}

/// <summary>
/// Common base for the Phase 20+ control vocabulary. Individual screens may remain model-driven while
/// migrating; the control layer gives them one definition for bounds, focusability and accessible text.
/// </summary>
public abstract record UiControl(string Id, Rectangle Bounds)
{
    public bool Enabled { get; init; } = true;
    public bool Visible { get; init; } = true;
    public bool Focusable { get; init; } = true;
    public string? Tooltip { get; init; }
    public string? AccessibilityLabel { get; init; }
}

public sealed record UiButton(string ControlId, Rectangle ControlBounds, string Label) : UiControl(ControlId, ControlBounds);
public sealed record UiTabButton(string ControlId, Rectangle ControlBounds, string Label, int TabIndex) : UiControl(ControlId, ControlBounds);
public sealed record UiListView(string ControlId, Rectangle ControlBounds, int ItemCount) : UiControl(ControlId, ControlBounds);
public sealed record UiCardGrid(string ControlId, Rectangle ControlBounds, int ItemCount, int Columns) : UiControl(ControlId, ControlBounds);
public sealed record UiScrollPane(string ControlId, Rectangle ControlBounds, int Offset, int MaximumOffset) : UiControl(ControlId, ControlBounds);
public sealed record UiScrollbar(string ControlId, Rectangle ControlBounds, int Offset, int MaximumOffset) : UiControl(ControlId, ControlBounds);
public sealed record UiSlider(string ControlId, Rectangle ControlBounds, float Value, float Minimum, float Maximum) : UiControl(ControlId, ControlBounds);
public sealed record UiToggle(string ControlId, Rectangle ControlBounds, bool Value) : UiControl(ControlId, ControlBounds);
public sealed record UiStepper(string ControlId, Rectangle ControlBounds, decimal Value, decimal Step) : UiControl(ControlId, ControlBounds);
public sealed record UiDropdown(string ControlId, Rectangle ControlBounds, string Value) : UiControl(ControlId, ControlBounds);
public sealed record UiTextBox(string ControlId, Rectangle ControlBounds, string Value) : UiControl(ControlId, ControlBounds);
public sealed record UiProgressBar(string ControlId, Rectangle ControlBounds, float Value, float Maximum) : UiControl(ControlId, ControlBounds);
public sealed record UiActionBar(string ControlId, Rectangle ControlBounds) : UiControl(ControlId, ControlBounds);
public sealed record UiModalDialog(string ControlId, Rectangle ControlBounds) : UiControl(ControlId, ControlBounds);

/// <summary>A concrete interactive region produced by the renderer and consumed by the input router.</summary>
public sealed record UiHitRegion(
    string Id,
    UiControlKind Kind,
    Rectangle Bounds,
    bool Enabled = true,
    int Index = -1,
    UiAction? Action = null,
    int ZOrder = 0,
    string? Tooltip = null)
{
    public bool Contains(Point point) => Bounds.Contains(point);
}

/// <summary>Per-frame hit map. Highest Z-order, then most recently registered region wins.</summary>
public sealed class UiHitMap
{
    private readonly List<UiHitRegion> regions = [];
    public Phase20ScreenKind ScreenKind { get; private set; } = Phase20ScreenKind.None;
    public IReadOnlyList<UiHitRegion> Regions => regions;

    public void Begin(Phase20ScreenKind screenKind)
    {
        ScreenKind = screenKind;
        regions.Clear();
    }

    public void Clear()
    {
        ScreenKind = Phase20ScreenKind.None;
        regions.Clear();
    }

    public void Add(UiHitRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region.Bounds.Width > 0 && region.Bounds.Height > 0) regions.Add(region);
    }

    public UiHitRegion? HitTest(Point point, UiControlKind? kind = null)
    {
        UiHitRegion? best = null;
        int bestIndex = -1;
        for (int index = 0; index < regions.Count; index++)
        {
            UiHitRegion candidate = regions[index];
            if (kind.HasValue && candidate.Kind != kind.Value) continue;
            if (!candidate.Contains(point)) continue;
            if (best is null || candidate.ZOrder > best.ZOrder || (candidate.ZOrder == best.ZOrder && index > bestIndex))
            {
                best = candidate;
                bestIndex = index;
            }
        }
        return best;
    }
}

public sealed record UiInteractionSnapshot(
    Point Pointer,
    UiInputDevice ActiveDevice,
    string? FocusedControlId = null,
    bool DebugOverlay = false);

/// <summary>Remembered state for a screen. Switching tabs no longer throws away selection and scroll position.</summary>
public sealed record UiScreenState
{
    public int SelectedIndex { get; init; }
    public int ScrollOffset { get; init; }
    public int DetailScrollOffset { get; init; }
    public int SelectedTab { get; init; }
    public string? FocusedControlId { get; init; }

    public UiScreenState Clamp(int entryCount)
    {
        if (entryCount <= 0) return this with { SelectedIndex = 0, ScrollOffset = 0 };
        return this with
        {
            SelectedIndex = Math.Clamp(SelectedIndex, 0, entryCount - 1),
            ScrollOffset = Math.Clamp(ScrollOffset, 0, entryCount - 1),
            DetailScrollOffset = Math.Max(0, DetailScrollOffset)
        };
    }

    public UiScreenState EnsureSelectionVisible(int visibleCount)
    {
        if (visibleCount <= 0) return this;
        int offset = ScrollOffset;
        if (SelectedIndex < offset) offset = SelectedIndex;
        else if (SelectedIndex >= offset + visibleCount) offset = SelectedIndex - visibleCount + 1;
        return this with { ScrollOffset = Math.Max(0, offset) };
    }
}

public sealed class UiScreenStateStore
{
    private readonly Dictionary<Phase20ScreenKind, UiScreenState> states = [];

    public UiScreenState Get(Phase20ScreenKind kind, int entryCount = int.MaxValue)
    {
        UiScreenState state = states.TryGetValue(kind, out UiScreenState? value) ? value : new UiScreenState();
        return entryCount == int.MaxValue ? state : state.Clamp(entryCount);
    }

    public void Set(Phase20ScreenKind kind, UiScreenState state) => states[kind] = state ?? new UiScreenState();
    public void Reset(Phase20ScreenKind kind) => states.Remove(kind);
    public void ResetAll() => states.Clear();
}

public enum UiBindingInputKind { Keyboard, Mouse, Gamepad }

public readonly record struct UiPhysicalInput(UiBindingInputKind Kind, Keys Key = Keys.None, UiMouseButton MouseButton = UiMouseButton.None, Buttons GamepadButton = Buttons.None)
{
    public static UiPhysicalInput Keyboard(Keys key) => new(UiBindingInputKind.Keyboard, Key: key);
    public static UiPhysicalInput Mouse(UiMouseButton button) => new(UiBindingInputKind.Mouse, MouseButton: button);
    public static UiPhysicalInput Gamepad(Buttons button) => new(UiBindingInputKind.Gamepad, GamepadButton: button);
    public override string ToString() => Kind switch
    {
        UiBindingInputKind.Keyboard => Key.ToString(),
        UiBindingInputKind.Mouse => UiBindingFormatter.MouseButtonLabel(MouseButton),
        UiBindingInputKind.Gamepad => GamepadButton.ToString(),
        _ => string.Empty
    };
}

public sealed record UiBindingCapture(UiAction Action, InputBinding OriginalBinding);
public sealed record UiBindingConflict(UiAction TargetAction, UiPhysicalInput Incoming, UiPhysicalInput Outgoing, IReadOnlyList<UiAction> ConflictingActions, InputBinding ProposedBinding);
public sealed record Phase20ActionPrompt(UiAction Action, string Label, bool Enabled = true, bool Dangerous = false, bool ShowBinding = true, string? Tooltip = null);

public static class UiBindingFormatter
{
    public static string BindingLabel(Phase20UserSettings settings, UiAction action, UiInputDevice device)
    {
        if (!settings.Bindings.TryGetValue(action, out InputBinding? binding)) return action.ToString().ToUpperInvariant();
        return device switch
        {
            UiInputDevice.Gamepad when binding.GamepadButton != Buttons.None => binding.GamepadLabel,
            UiInputDevice.Mouse when binding.MouseButton != UiMouseButton.None => binding.MouseLabel,
            _ when binding.PrimaryKey != Keys.None || binding.SecondaryKey != Keys.None => binding.KeyboardLabel,
            _ when binding.MouseButton != UiMouseButton.None => binding.MouseLabel,
            _ when binding.GamepadButton != Buttons.None => binding.GamepadLabel,
            _ => "UNBOUND"
        };
    }

    public static string Prompt(Phase20UserSettings settings, Phase20ActionPrompt prompt, UiInputDevice device)
        => $"{BindingLabel(settings, prompt.Action, device).ToUpperInvariant()} {prompt.Label.ToUpperInvariant()}";

    public static string MouseButtonLabel(UiMouseButton button) => button switch
    {
        UiMouseButton.Left => "MOUSE 1",
        UiMouseButton.Right => "MOUSE 2",
        UiMouseButton.Middle => "MOUSE 3",
        UiMouseButton.XButton1 => "MOUSE 4",
        UiMouseButton.XButton2 => "MOUSE 5",
        _ => string.Empty
    };
}

/// <summary>Spatial focus helper for keyboard/controller navigation between arbitrary controls.</summary>
public static class UiFocusNavigator
{
    public static UiHitRegion? FindNearest(IReadOnlyList<UiHitRegion> regions, string? currentId, Vector2 direction)
    {
        if (regions.Count == 0 || direction.LengthSquared() < 0.001f) return null;
        UiHitRegion? current = currentId is null ? null : regions.FirstOrDefault(value => value.Id == currentId);
        if (current is null) return regions.FirstOrDefault(value => value.Enabled);
        Vector2 origin = new(current.Bounds.Center.X, current.Bounds.Center.Y);
        direction.Normalize();
        UiHitRegion? best = null;
        float bestScore = float.MaxValue;
        foreach (UiHitRegion candidate in regions)
        {
            if (!candidate.Enabled || candidate.Id == current.Id) continue;
            Vector2 target = new(candidate.Bounds.Center.X, candidate.Bounds.Center.Y);
            Vector2 delta = target - origin;
            float distance = delta.Length();
            if (distance < 1f) continue;
            float alignment = Vector2.Dot(delta / distance, direction);
            if (alignment <= 0.15f) continue;
            float score = distance * (2.1f - alignment);
            if (score < bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }
}
