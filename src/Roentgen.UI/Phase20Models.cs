using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Input;
using Roentgen.Content;
using Roentgen.Focus;

namespace Roentgen.UI;

public enum Phase20ScreenKind
{
    None,
    MainMenu,
    NewCampaign,
    CharacterAttributes,
    CharacterSkills,
    CharacterBackground,
    CharacterTraits,
    CharacterReview,
    LoadGame,
    SaveGame,
    PauseMenu,
    Inventory,
    Equipment,
    Character,
    Skills,
    Perks,
    Map,
    Journal,
    Dialogue,
    Factions,
    Crafting,
    Merchant,
    Companion,
    Settings,
    Accessibility,
    InputBindings,
    BindingConflict,
    Tutorials,
    Credits,
    Ending,
    Epilogue,
    Confirmation,
    Error,
    RoentgenFocus,
    ContainerLoot,
    ItemInspection
}

public enum UiAction
{
    MoveForward,
    MoveBackward,
    StrafeLeft,
    StrafeRight,
    Jump,
    Interact,
    Sprint,
    Crouch,
    PrimaryAttack,
    SecondaryAttack,
    Reload,
    EquipSidearm,
    EquipRifle,
    EquipMelee,
    ThrowGrenade,
    NavigateUp,
    NavigateDown,
    NavigateLeft,
    NavigateRight,
    Confirm,
    Cancel,
    NextTab,
    PreviousTab,
    Pause,
    OpenInventory,
    OpenEquipment,
    OpenCharacter,
    OpenSkills,
    OpenPerks,
    OpenMap,
    OpenJournal,
    OpenFactions,
    OpenCrafting,
    OpenCompanion,
    OpenDialogue,
    OpenMerchant,
    OpenFocus,
    QuickSave,
    QuickLoad,
    ToggleDiagnostics,
    Delete,
    PageUp,
    PageDown,
    FocusQueue,
    FocusRemove,
    FocusCommit,
    FocusNextTarget,
    FocusPreviousTarget,
    FocusNextPart,
    FocusPreviousPart,
    WizardBack,
    WizardNext,
    WizardFinish,
    WizardReset,
    WizardRecommended,
    InventoryUse,
    InventoryEquip,
    InventoryRepair,
    InventoryDrop,
    InventorySplit,
    InventoryInspect,
    InventorySort,
    EquipmentChange,
    EquipmentEquip,
    EquipmentUnequip,
    EquipmentRepair,
    EquipmentInspect,
    EquipmentBack,
    SkillIncrease,
    PerkAcquire,
    CraftingCraftOne,
    CraftingCraftFive,
    CraftingCraftAll,
    CraftingToggleFavorite,
    CraftingToggleUnavailable,
    ContainerTake,
    ContainerTakeAll,
    MerchantBuyOne,
    MerchantBuyFive,
    MerchantBuyAll,
    MerchantSellOne,
    MerchantSellFive,
    MerchantSellAll,
    MerchantSwitchPane,
    MerchantInspect,
    MapTrack,
    MapClearWaypoint,
    MapZoomIn,
    MapZoomOut,
    MapCenterPlayer,
    JournalTrack,
    JournalUntrack,
    JournalShowOnMap,
    FactionShowOnMap,
    SettingsReset
}

public enum DifficultyPreset
{
    Story,
    Standard,
    Survival,
    Custom
}

public enum ColorVisionMode
{
    Standard,
    Deuteranopia,
    Protanopia,
    Tritanopia,
    Monochrome
}

public enum ToggleBehavior
{
    Hold,
    Toggle
}

public enum Phase20EntryPresentation
{
    Standard,
    Button,
    Toggle,
    Slider,
    Stepper,
    Dropdown,
    TextBox,
    Progress
}

public enum Phase20ScreenPresentation
{
    Standard,
    CardGridTwoColumns,
    CardGridThreeColumns,
    MerchantTrade,
    DialogueConversation
}

public enum CompanionScreenSection
{
    Overview = 0,
    Commands = 1,
    Equipment = 2,
    Behavior = 3
}

public enum Phase20SettingsSection
{
    Gameplay = 0,
    Controls = 1,
    Video = 2,
    Audio = 3,
    Accessibility = 4
}

public sealed record Phase20ProgressMeter(string Label, float Value, float Maximum, string? ValueLabel = null);

public sealed record Phase20VideoSettings
{
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public bool Fullscreen { get; init; }
    public bool VSync { get; init; } = true;

    public Phase20VideoSettings Validate() => this with
    {
        Width = Math.Clamp(Width, 800, 7680),
        Height = Math.Clamp(Height, 600, 4320)
    };
}

public sealed record InputBinding(Keys PrimaryKey, Keys SecondaryKey, Buttons GamepadButton, UiMouseButton MouseButton = UiMouseButton.None)
{
    public static InputBinding Keyboard(Keys key, Buttons button = Buttons.None) => new(key, Keys.None, button);
    public static InputBinding Mouse(UiMouseButton button, Buttons gamepadButton = Buttons.None) => new(Keys.None, Keys.None, gamepadButton, button);

    public string KeyboardLabel
    {
        get
        {
            if (PrimaryKey == Keys.None && SecondaryKey == Keys.None) return string.Empty;
            if (PrimaryKey == Keys.None) return SecondaryKey.ToString();
            return SecondaryKey == Keys.None ? PrimaryKey.ToString() : $"{PrimaryKey}/{SecondaryKey}";
        }
    }

    public string MouseLabel => UiBindingFormatter.MouseButtonLabel(MouseButton);
    public string GamepadLabel => GamepadButton == Buttons.None ? string.Empty : GamepadButton.ToString();

    public bool Contains(UiPhysicalInput input) => input.Kind switch
    {
        UiBindingInputKind.Keyboard => input.Key != Keys.None && (PrimaryKey == input.Key || SecondaryKey == input.Key),
        UiBindingInputKind.Mouse => input.MouseButton != UiMouseButton.None && MouseButton == input.MouseButton,
        UiBindingInputKind.Gamepad => input.GamepadButton != Buttons.None && GamepadButton == input.GamepadButton,
        _ => false
    };

    public UiPhysicalInput Preferred(UiBindingInputKind kind) => kind switch
    {
        UiBindingInputKind.Keyboard when PrimaryKey != Keys.None => UiPhysicalInput.Keyboard(PrimaryKey),
        UiBindingInputKind.Keyboard when SecondaryKey != Keys.None => UiPhysicalInput.Keyboard(SecondaryKey),
        UiBindingInputKind.Mouse when MouseButton != UiMouseButton.None => UiPhysicalInput.Mouse(MouseButton),
        UiBindingInputKind.Gamepad when GamepadButton != Buttons.None => UiPhysicalInput.Gamepad(GamepadButton),
        _ => default
    };

    public InputBinding WithPhysical(UiPhysicalInput input) => input.Kind switch
    {
        UiBindingInputKind.Keyboard => this with { PrimaryKey = input.Key },
        UiBindingInputKind.Mouse => this with { MouseButton = input.MouseButton },
        UiBindingInputKind.Gamepad => this with { GamepadButton = input.GamepadButton },
        _ => this
    };

    public InputBinding RemovePhysical(UiPhysicalInput input) => input.Kind switch
    {
        UiBindingInputKind.Keyboard when PrimaryKey == input.Key => this with { PrimaryKey = SecondaryKey, SecondaryKey = Keys.None },
        UiBindingInputKind.Keyboard when SecondaryKey == input.Key => this with { SecondaryKey = Keys.None },
        UiBindingInputKind.Mouse when MouseButton == input.MouseButton => this with { MouseButton = UiMouseButton.None },
        UiBindingInputKind.Gamepad when GamepadButton == input.GamepadButton => this with { GamepadButton = Buttons.None },
        _ => this
    };

    public InputBinding ReplacePhysical(UiPhysicalInput oldInput, UiPhysicalInput newInput)
    {
        InputBinding removed = RemovePhysical(oldInput);
        if (newInput.Kind != oldInput.Kind) return removed;
        return removed.WithPhysical(newInput);
    }
}

public sealed record Phase20AccessibilitySettings
{
    public float InterfaceScale { get; init; } = 1f;
    public float SubtitleScale { get; init; } = 1f;
    public bool HighContrast { get; init; }
    public ColorVisionMode ColorVisionMode { get; init; } = ColorVisionMode.Standard;
    public bool DisableCameraShake { get; init; } = true;
    public bool DisableHeadBob { get; init; }
    public bool DisableFlashes { get; init; }
    public bool DisableMotionBlur { get; init; } = true;
    public bool ReduceMotion { get; init; }
    public bool PersistentReticle { get; init; } = true;
    public bool PauseDuringDialogue { get; init; } = true;
    public bool PauseInMenus { get; init; } = true;
    public bool CinematicFocusCamera { get; init; } = true;
    public int FocusOverlayIntensityPermille { get; init; } = 850;
    public int FocusFlashIntensityPermille { get; init; } = 350;
    public ToggleBehavior AimBehavior { get; init; } = ToggleBehavior.Hold;
    public ToggleBehavior CrouchBehavior { get; init; } = ToggleBehavior.Toggle;
    public ToggleBehavior SprintBehavior { get; init; } = ToggleBehavior.Hold;

    public Phase20AccessibilitySettings Validate() => this with
    {
        InterfaceScale = Math.Clamp(InterfaceScale, 0.75f, 2f),
        SubtitleScale = Math.Clamp(SubtitleScale, 0.75f, 2f),
        FocusOverlayIntensityPermille = Math.Clamp(FocusOverlayIntensityPermille, 0, 1000),
        FocusFlashIntensityPermille = Math.Clamp(FocusFlashIntensityPermille, 0, 1000)
    };

    public FocusAccessibilitySettings ToFocusSettings() => new FocusAccessibilitySettings(
        CinematicCuts: CinematicFocusCamera,
        CameraShake: !DisableCameraShake,
        ExecutionZoom: !ReduceMotion,
        DiagnosticIntensityPermille: FocusOverlayIntensityPermille,
        PhosphorPersistencePermille: ReduceMotion ? 300 : 650,
        FlashIntensityPermille: DisableFlashes ? 0 : FocusFlashIntensityPermille,
        SlowMotionPermille: ReduceMotion ? 500 : 250,
        HighContrast: HighContrast,
        ToggleActivation: true).Clamped();
}

public sealed record Phase20GameplaySettings
{
    public DifficultyPreset Difficulty { get; init; } = DifficultyPreset.Standard;
    public float FieldOfViewDegrees { get; init; } = 75f;
    public float MouseSensitivity { get; init; } = 0.12f;
    public float GamepadLookSensitivity { get; init; } = 2.2f;
    public bool InvertVerticalLook { get; init; }
    public bool TutorialsEnabled { get; init; } = true;
    public bool AutosavesEnabled { get; init; } = true;
    public int AutosaveSlots { get; init; } = 5;
    public int AutosaveIntervalMinutes { get; init; } = 10;

    public Phase20GameplaySettings Validate() => this with
    {
        FieldOfViewDegrees = Math.Clamp(FieldOfViewDegrees, 55f, 110f),
        MouseSensitivity = Math.Clamp(MouseSensitivity, 0.02f, 1f),
        GamepadLookSensitivity = Math.Clamp(GamepadLookSensitivity, 0.2f, 8f),
        AutosaveSlots = Math.Clamp(AutosaveSlots, 1, 20),
        AutosaveIntervalMinutes = Math.Clamp(AutosaveIntervalMinutes, 1, 60)
    };
}


public sealed record Phase20AudioInterfaceSettings
{
    public int MasterPermille { get; init; } = 1000;
    public int MusicPermille { get; init; } = 800;
    public int InterfacePermille { get; init; } = 900;
    public int WeaponsPermille { get; init; } = 1000;
    public int CreaturesPermille { get; init; } = 1000;
    public int FoleyPermille { get; init; } = 900;
    public int AmbiencePermille { get; init; } = 800;
    public int FocusPermille { get; init; } = 900;

    public Phase20AudioInterfaceSettings Validate() => this with
    {
        MasterPermille = Math.Clamp(MasterPermille, 0, 1000),
        MusicPermille = Math.Clamp(MusicPermille, 0, 1000),
        InterfacePermille = Math.Clamp(InterfacePermille, 0, 1000),
        WeaponsPermille = Math.Clamp(WeaponsPermille, 0, 1000),
        CreaturesPermille = Math.Clamp(CreaturesPermille, 0, 1000),
        FoleyPermille = Math.Clamp(FoleyPermille, 0, 1000),
        AmbiencePermille = Math.Clamp(AmbiencePermille, 0, 1000),
        FocusPermille = Math.Clamp(FocusPermille, 0, 1000)
    };

    public float Master => MasterPermille / 1000f;
    public float Music => MusicPermille / 1000f;
    public float Interface => InterfacePermille / 1000f;
    public float Weapons => WeaponsPermille / 1000f;
    public float Creatures => CreaturesPermille / 1000f;
    public float Foley => FoleyPermille / 1000f;
    public float Ambience => AmbiencePermille / 1000f;
    public float Focus => FocusPermille / 1000f;
}

public sealed record Phase20UserSettings
{
    public const int CurrentSchemaVersion = 20;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Phase20GameplaySettings Gameplay { get; init; } = new();
    public Phase20AccessibilitySettings Accessibility { get; init; } = new();
    public Phase20VideoSettings Video { get; init; } = new();
    public Phase20AudioInterfaceSettings Audio { get; init; } = new();
    public IReadOnlyDictionary<UiAction, InputBinding> Bindings { get; init; } = InputBindingDefaults.Create();

    public Phase20UserSettings Validate()
    {
        IReadOnlyDictionary<UiAction, InputBinding> defaults = InputBindingDefaults.Create();
        var mergedBindings = Bindings.Count == 0
            ? defaults.ToDictionary(static value => value.Key, static value => value.Value)
            : Bindings.ToDictionary(static value => value.Key, static value => value.Value);
        foreach ((UiAction action, InputBinding binding) in defaults)
            if (!mergedBindings.ContainsKey(action)) mergedBindings[action] = binding;
        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            Gameplay = Gameplay.Validate(),
            Accessibility = Accessibility.Validate(),
            Video = Video.Validate(),
            Audio = Audio.Validate(),
            Bindings = mergedBindings
        };
    }
}

public static class InputBindingDefaults
{
    public static IReadOnlyDictionary<UiAction, InputBinding> Create() => new Dictionary<UiAction, InputBinding>
    {
        [UiAction.MoveForward] = InputBinding.Keyboard(Keys.W),
        [UiAction.MoveBackward] = InputBinding.Keyboard(Keys.S),
        [UiAction.StrafeLeft] = InputBinding.Keyboard(Keys.A),
        [UiAction.StrafeRight] = InputBinding.Keyboard(Keys.D),
        [UiAction.Jump] = InputBinding.Keyboard(Keys.Space, Buttons.A),
        [UiAction.Interact] = InputBinding.Keyboard(Keys.E, Buttons.X),
        [UiAction.Sprint] = InputBinding.Keyboard(Keys.LeftShift, Buttons.LeftStick),
        [UiAction.Crouch] = new(Keys.LeftControl, Keys.C, Buttons.B),
        [UiAction.PrimaryAttack] = InputBinding.Mouse(UiMouseButton.Left, Buttons.RightTrigger),
        [UiAction.SecondaryAttack] = InputBinding.Mouse(UiMouseButton.Right, Buttons.LeftTrigger),
        [UiAction.Reload] = InputBinding.Keyboard(Keys.R, Buttons.Y),
        [UiAction.EquipSidearm] = InputBinding.Keyboard(Keys.D1, Buttons.DPadLeft),
        [UiAction.EquipRifle] = InputBinding.Keyboard(Keys.D2, Buttons.DPadUp),
        [UiAction.EquipMelee] = InputBinding.Keyboard(Keys.D3, Buttons.DPadRight),
        [UiAction.ThrowGrenade] = InputBinding.Keyboard(Keys.G, Buttons.RightShoulder),
        [UiAction.NavigateUp] = new(Keys.Up, Keys.W, Buttons.DPadUp),
        [UiAction.NavigateDown] = new(Keys.Down, Keys.S, Buttons.DPadDown),
        [UiAction.NavigateLeft] = new(Keys.Left, Keys.A, Buttons.DPadLeft),
        [UiAction.NavigateRight] = new(Keys.Right, Keys.D, Buttons.DPadRight),
        [UiAction.Confirm] = new(Keys.Enter, Keys.Space, Buttons.A),
        [UiAction.Cancel] = new(Keys.Escape, Keys.Back, Buttons.B),
        [UiAction.NextTab] = new(Keys.Tab, Keys.PageDown, Buttons.RightShoulder),
        [UiAction.PreviousTab] = new(Keys.PageUp, Keys.None, Buttons.LeftShoulder),
        [UiAction.Pause] = new(Keys.Escape, Keys.None, Buttons.Start),
        [UiAction.OpenInventory] = InputBinding.Keyboard(Keys.I, Buttons.Y),
        [UiAction.OpenEquipment] = InputBinding.Keyboard(Keys.O),
        [UiAction.OpenCharacter] = InputBinding.Keyboard(Keys.P),
        [UiAction.OpenSkills] = InputBinding.Keyboard(Keys.K),
        [UiAction.OpenPerks] = InputBinding.Keyboard(Keys.L),
        [UiAction.OpenMap] = InputBinding.Keyboard(Keys.M, Buttons.Back),
        [UiAction.OpenJournal] = InputBinding.Keyboard(Keys.J),
        [UiAction.OpenFactions] = InputBinding.Keyboard(Keys.F),
        [UiAction.OpenCrafting] = InputBinding.Keyboard(Keys.B),
        [UiAction.OpenCompanion] = InputBinding.Keyboard(Keys.H),
        [UiAction.OpenDialogue] = InputBinding.Keyboard(Keys.Q),
        [UiAction.OpenMerchant] = InputBinding.Keyboard(Keys.T),
        [UiAction.OpenFocus] = InputBinding.Keyboard(Keys.V, Buttons.LeftShoulder),
        [UiAction.QuickSave] = InputBinding.Keyboard(Keys.F5),
        [UiAction.QuickLoad] = InputBinding.Keyboard(Keys.F9),
        [UiAction.ToggleDiagnostics] = InputBinding.Keyboard(Keys.F3),
        [UiAction.Delete] = InputBinding.Keyboard(Keys.Delete, Buttons.X),
        [UiAction.PageUp] = InputBinding.Keyboard(Keys.PageUp, Buttons.LeftTrigger),
        [UiAction.PageDown] = InputBinding.Keyboard(Keys.PageDown, Buttons.RightTrigger),
        [UiAction.FocusQueue] = new(Keys.Enter, Keys.Space, Buttons.A),
        [UiAction.FocusRemove] = InputBinding.Keyboard(Keys.Back, Buttons.X),
        [UiAction.FocusCommit] = InputBinding.Keyboard(Keys.R, Buttons.Y),
        [UiAction.FocusNextTarget] = InputBinding.Keyboard(Keys.E, Buttons.RightShoulder),
        [UiAction.FocusPreviousTarget] = InputBinding.Keyboard(Keys.Q, Buttons.LeftShoulder),
        [UiAction.FocusNextPart] = InputBinding.Keyboard(Keys.Down, Buttons.DPadDown),
        [UiAction.FocusPreviousPart] = InputBinding.Keyboard(Keys.Up, Buttons.DPadUp)
    };
}

public sealed class Phase20SettingsStore
{
    private readonly string path;
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public Phase20SettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = Path.GetFullPath(path);
    }

    public Phase20UserSettings Load()
    {
        try
        {
            if (!File.Exists(path)) return new Phase20UserSettings().Validate();
            string json = File.ReadAllText(path);
            Phase20UserSettings loaded = JsonSerializer.Deserialize<Phase20UserSettings>(json, Options) ?? new Phase20UserSettings();
            // Pre-foundation Phase 20 settings did not serialize mouse bindings because the mouse was hardcoded
            // in the combat adapter. Migrate those profiles once so existing players retain fire/aim controls.
            if (!json.Contains("\"MouseButton\"", StringComparison.OrdinalIgnoreCase))
            {
                var bindings = loaded.Bindings.ToDictionary(static value => value.Key, static value => value.Value);
                IReadOnlyDictionary<UiAction, InputBinding> defaults = InputBindingDefaults.Create();
                if (bindings.TryGetValue(UiAction.PrimaryAttack, out InputBinding? primary))
                    bindings[UiAction.PrimaryAttack] = primary with { MouseButton = UiMouseButton.Left };
                else
                    bindings[UiAction.PrimaryAttack] = defaults[UiAction.PrimaryAttack];
                if (bindings.TryGetValue(UiAction.SecondaryAttack, out InputBinding? secondary))
                    bindings[UiAction.SecondaryAttack] = secondary with { MouseButton = UiMouseButton.Right };
                else
                    bindings[UiAction.SecondaryAttack] = defaults[UiAction.SecondaryAttack];
                loaded = loaded with { Bindings = bindings };
            }
            return loaded.Validate();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Phase20UserSettings().Validate();
        }
    }

    public void Save(Phase20UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Validate(), Options));
        File.Move(temporary, path, overwrite: true);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed record Phase20MenuEntry(
    string Id,
    string Label,
    string? Value = null,
    string? Description = null,
    bool Enabled = true,
    bool Dangerous = false,
    string? Badge = null,
    Phase20EntryPresentation Presentation = Phase20EntryPresentation.Standard,
    float NormalizedValue = 0f);

public sealed record Phase20Panel(
    string Title,
    IReadOnlyList<string> Lines,
    int SelectedLine = -1,
    string? Status = null);

public sealed record Phase20ScreenModel(
    Phase20ScreenKind Kind,
    string Title,
    string Subtitle,
    IReadOnlyList<Phase20MenuEntry> Entries,
    int SelectedIndex,
    IReadOnlyList<string> Tabs,
    int SelectedTab,
    Phase20Panel? Detail,
    string Footer,
    string Status,
    bool DimWorld = true,
    bool Modal = false,
    int ScrollOffset = 0,
    int DetailScrollOffset = 0,
    string? FocusedControlId = null,
    IReadOnlyList<Phase20ActionPrompt>? ActionPrompts = null,
    Phase20ScreenPresentation Presentation = Phase20ScreenPresentation.Standard,
    IReadOnlyList<string>? SubTabs = null,
    int SelectedSubTab = 0,
    IReadOnlyList<Phase20ProgressMeter>? Meters = null,
    bool TextEntryActive = false,
    IReadOnlyList<Phase20MenuEntry>? SecondaryEntries = null,
    int SecondarySelectedIndex = 0,
    int SecondaryScrollOffset = 0,
    string? SecondaryTitle = null,
    bool SecondaryPaneActive = false);

public sealed record Phase20FocusModel(
    FocusPresentationSnapshot Snapshot,
    string TargetName,
    string SelectedPartName,
    IReadOnlyList<Phase20MenuEntry> TargetParts,
    int SelectedPartIndex,
    IReadOnlyList<string> ForecastLines,
    IReadOnlyList<string> QueueLines,
    string Footer,
    string Status);

public sealed record TutorialCard(
    string Id,
    string Title,
    string Body,
    string Trigger,
    bool CanReplay = true);

public static class Phase20TutorialCatalog
{
    private static readonly Lazy<IReadOnlyList<TutorialCard>> Cards = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<TutorialCard> All => Cards.Value;

    private static IReadOnlyList<TutorialCard> Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        TutorialCard[] result = content.Pack.Tutorials
            .OrderByDescending(static value => value.Priority)
            .ThenBy(static value => value.Id, StringComparer.Ordinal)
            .Select(static value => new TutorialCard(value.Id, value.Title, value.Body, value.TriggerFact, value.Replayable))
            .ToArray();
        if (result.Length != ContentBudgets.Tutorials)
            throw new InvalidOperationException($"Phase 21 requires {ContentBudgets.Tutorials} tutorials; found {result.Length}.");
        return result;
    }
}
