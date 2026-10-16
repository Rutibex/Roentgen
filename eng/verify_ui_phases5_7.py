#!/usr/bin/env python3
from __future__ import annotations
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def require(path: str, *needles: str) -> None:
    file = ROOT / path
    if not file.is_file():
        raise RuntimeError(f"Missing {path}")
    text = file.read_text(encoding="utf-8")
    for needle in needles:
        if needle not in text:
            raise RuntimeError(f"{path} missing marker: {needle}")

def main() -> int:
    require("src/Roentgen.App/Phase20CharacterCreation.cs",
            "CharacterName", "ResetAttributes", "ApplyRecommendedAttributes",
            "if (specializedSkills.Count >= 3) return false", "if (traitIds.Count >= 2) return false")
    require("src/Roentgen.App/Phase20ScreenFactory.cs",
            "BuildCampaignIdentity", "CardGridTwoColumns", "WizardNext", "WizardFinish",
            "InventoryTabs", "InventorySortMode", "ContainerLoot", "ItemInspection",
            "SettingsTabs", "Phase20SettingsSection.Video", 'new("motion-blur"')
    require("src/Roentgen.App/RoentgenGame.cs",
            "TryOpenCreationStage", "HandleCharacterNameInput", "ActivateSubTabIndex",
            "ApplySliderFromPointer", "ResetCurrentSettingsSection", "ApplyVideoSettings",
            "HandleInventoryAction", "HandleContainerAction", "dropItemAfterConfirmation")
    require("src/Roentgen.App/Phase13ItemRuntime.cs",
            "BuildInspection(StableId itemId, bool includeDiagnostics = false)",
            "EquipInventoryItem", "SplitInventoryStackHalf", "DropInventoryItem", "CanRepairInventoryItem")
    require("src/Roentgen.Simulation/PlayerSimulationRuntime.cs",
            "TransferOpenContainerItem", "TransferAllOpenContainerItems", "CloseOpenContainer", "BuildPresentationSnapshot")
    require("src/Roentgen.UI/Phase20UiRenderer.cs",
            "Phase20EntryPresentation.Button", "UiControlKind.Slider", "UiControlKind.TextBox", "DrawCardGrid")

    require("docs/PLAYER_GUIDE.md", "Roentgen Focus", "Inventory")
    print("UI Phases 5-7 verification PASSED: character wizard, settings/accessibility controls, inventory, inspection, and container loot paths are present.")
    return 0

if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"UI Phases 5-7 verification FAILED: {exc}")
        raise SystemExit(1)
