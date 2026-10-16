using Xunit;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Persistence;
using Roentgen.UI;

namespace Roentgen.Phase20.Tests;

public sealed class Phase20ContractTests
{
    [Fact]
    public void Settings_validation_clamps_accessibility_and_gameplay_ranges()
    {
        Phase20UserSettings value = new Phase20UserSettings
        {
            Gameplay = new Phase20GameplaySettings { FieldOfViewDegrees = 500, AutosaveSlots = 0 },
            Accessibility = new Phase20AccessibilitySettings { InterfaceScale = 5, FocusOverlayIntensityPermille = -100 }
        }.Validate();
        Assert.Equal(110f, value.Gameplay.FieldOfViewDegrees);
        Assert.Equal(1, value.Gameplay.AutosaveSlots);
        Assert.Equal(2f, value.Accessibility.InterfaceScale);
        Assert.Equal(0, value.Accessibility.FocusOverlayIntensityPermille);
    }

    [Fact]
    public void Acceptance_layout_matrix_has_no_hard_overflow()
    {
        var model = new Phase20ScreenModel(Phase20ScreenKind.Inventory, "Inventory", "Generated items",
            Enumerable.Range(0, 80).Select(i => new Phase20MenuEntry(i.ToString(), $"Item {i}")).ToArray(), 0,
            ["Inventory", "Equipment", "Character"], 0, new Phase20Panel("Detail", ["Line"]), "ENTER SELECT • ESC CLOSE", string.Empty);
        foreach (InterfaceViewportProfile profile in Phase20LayoutPlanner.AcceptanceMatrix)
            Assert.True(Phase20LayoutPlanner.Analyze(model, profile).IsValid, profile.Name);
    }

    [Fact]
    public void Pseudo_localization_expands_and_preserves_lookup()
    {
        var service = new Phase20LocalizationService("en-US", Phase20LocalizationService.CreatePseudoLocalization());
        string value = service.Get("menu.newCampaign");
        Assert.StartsWith("⟦", value);
        Assert.EndsWith("⟧", value);
        Assert.Empty(service.MissingKeys);
    }

    [Fact]
    public void Save_serializer_round_trips_all_phase20_sections()
    {
        StableId id = StableIdFactory.Create("phase20-test", "player");
        var metadata = new SaveGameMetadata("manual-001", SaveSlotKind.Manual, "Manual Save", DateTimeOffset.UnixEpoch, 42, 99, 120, "Prospect", "Tester", 3, "0.20.0", 20, false);
        var player = new PlayerSaveState(id, FixedVector3.Zero, FixedVector3.Zero, 0, 0, PlayerStance.Standing, true, StableId.Empty, 7);
        var save = new Phase20SaveGame(metadata, player, [1], [2], [3], [4], [5], [], new Dictionary<string, string> { ["build"] = "20" });
        Phase20SaveGame restored = Phase20SaveGameSerializer.Read(Phase20SaveGameSerializer.Write(save));
        Assert.Equal(save.Metadata, restored.Metadata);
        Assert.Equal(save.Player, restored.Player);
        Assert.Equal(save.CampaignPayload, restored.CampaignPayload);
        Assert.Equal("20", restored.GeneratorVersions["build"]);
    }
    [Fact]
    public void Ui_screen_state_remembers_selection_and_independent_scroll()
    {
        var store = new UiScreenStateStore();
        store.Set(Phase20ScreenKind.Inventory, new UiScreenState { SelectedIndex = 17, ScrollOffset = 12, DetailScrollOffset = 8 });
        UiScreenState restored = store.Get(Phase20ScreenKind.Inventory, 30);
        Assert.Equal(17, restored.SelectedIndex);
        Assert.Equal(12, restored.ScrollOffset);
        Assert.Equal(8, restored.DetailScrollOffset);
        Assert.Equal(14, (restored with { SelectedIndex = 20 }).EnsureSelectionVisible(7).ScrollOffset);
    }

    [Fact]
    public void Hit_map_exposes_disabled_rows_for_inspection_without_marking_them_enabled()
    {
        var map = new UiHitMap();
        map.Begin(Phase20ScreenKind.Perks);
        map.Add(new UiHitRegion("locked", UiControlKind.ListItem, new Rectangle(10, 10, 100, 24), false, 3));
        UiHitRegion? hit = map.HitTest(new Point(20, 20));
        Assert.NotNull(hit);
        Assert.False(hit!.Enabled);
        Assert.Equal(3, hit.Index);
    }

    [Fact]
    public void Default_combat_bindings_are_real_mouse_bindings_and_can_be_replaced()
    {
        InputBinding attack = InputBindingDefaults.Create()[UiAction.PrimaryAttack];
        Assert.Equal(UiMouseButton.Left, attack.MouseButton);
        InputBinding remapped = attack.WithPhysical(UiPhysicalInput.Mouse(UiMouseButton.XButton1));
        Assert.Equal(UiMouseButton.XButton1, remapped.MouseButton);
        Assert.True(remapped.Contains(UiPhysicalInput.Mouse(UiMouseButton.XButton1)));
    }

    [Fact]
    public void Responsive_layout_preserves_detail_panel_in_narrow_windows()
    {
        UiLayoutMetrics layout = UiResponsiveLayout.Compute(900, 900, 1f, hasTabs: true, hasDetail: true);
        Assert.Equal(UiDetailPlacement.Below, layout.DetailPlacement);
        Assert.True(layout.EntriesArea.Height > 0);
        Assert.True(layout.DetailArea.Height > 0);
        Assert.True(layout.DetailArea.Top >= layout.EntriesArea.Bottom);
    }

    [Fact]
    public void Binding_formatter_tracks_active_device()
    {
        Phase20UserSettings settings = new();
        Assert.Equal("E", UiBindingFormatter.BindingLabel(settings, UiAction.Interact, UiInputDevice.Keyboard));
        Assert.Equal(Buttons.X.ToString(), UiBindingFormatter.BindingLabel(settings, UiAction.Interact, UiInputDevice.Gamepad));
        Assert.Equal("MOUSE 1", UiBindingFormatter.BindingLabel(settings, UiAction.PrimaryAttack, UiInputDevice.Mouse));
    }

}
