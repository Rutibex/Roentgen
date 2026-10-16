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
    require("src/Roentgen.UI/Phase13ItemScreens.cs",
            "EquipmentViewMode", "SkillCategory", "PerkFilter", "CraftingCategory",
            "EquipmentComparisonModel", "CraftingIngredientUiModel", "CraftingRecipeUiModel")
    require("src/Roentgen.UI/Phase20Models.cs",
            "EquipmentChange", "EquipmentEquip", "EquipmentUnequip", "EquipmentRepair", "EquipmentInspect",
            "SkillIncrease", "PerkAcquire", "CraftingCraftOne", "CraftingCraftFive", "CraftingCraftAll",
            "CraftingToggleFavorite", "CraftingToggleUnavailable")
    require("src/Roentgen.App/Phase13ItemRuntime.cs",
            "BuildEquipmentSlots", "BuildEquipmentCandidates", "EquipInventoryItemToSlot", "BuildEquipmentComparison",
            "BuildCraftingRecipeUi", "GetMaximumCraftCount", "CraftKnownRecipeMultiple", "StationIdFor")
    require("src/Roentgen.App/Phase20ScreenFactory.cs",
            "EquipmentTabs", "SkillTabs", "PerkTabs", "CraftingTabs",
            "Equipment(Phase13ItemRuntime", "Skills(Phase12ProgressionRuntime", "Perks(Phase12ProgressionRuntime",
            "Crafting(Phase13ItemRuntime", "CardGridTwoColumns", "Hide Unavailable")
    require("src/Roentgen.App/RoentgenGame.cs",
            "equipmentView", "equipmentSelectedSlot", "skillCategory", "perkFilter", "craftingCategory",
            "HandleEquipmentAction", "HandleCraftingAction", "ReturnEquipmentToSlots", "craftingFavorites")
    require("src/Roentgen.UI/Phase20UiRenderer.cs",
            "Phase20EntryPresentation.Progress", "valueReserve", "entry.NormalizedValue")

    require("docs/PLAYER_GUIDE.md", "Roentgen Focus", "Inventory")
    print("UI Phases 8-10 verification PASSED: equipment comparison, categorized skills/perks, and recipe-browser crafting paths are present.")
    return 0

if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"UI Phases 8-10 verification FAILED: {exc}")
        raise SystemExit(1)
