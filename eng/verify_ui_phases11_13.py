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

def forbid(path: str, *needles: str) -> None:
    file = ROOT / path
    if not file.is_file():
        raise RuntimeError(f"Missing {path}")
    text = file.read_text(encoding="utf-8")
    for needle in needles:
        if needle in text:
            raise RuntimeError(f"{path} retains obsolete marker: {needle}")

def main() -> int:
    require("src/Roentgen.UI/Phase13ItemScreens.cs", "MerchantPane", "MerchantTradePreviewUiModel")
    require("src/Roentgen.UI/Phase20Models.cs",
            "MerchantTrade", "SecondaryEntries", "MerchantBuyOne", "MerchantBuyFive", "MerchantBuyAll",
            "MerchantSellOne", "MerchantSellFive", "MerchantSellAll", "MapTrack", "MapZoomIn",
            "JournalTrack", "JournalUntrack", "JournalShowOnMap")
    require("src/Roentgen.App/Phase13ItemRuntime.cs",
            "BuildMerchantTradePreview", "EffectiveCommerce", "TradeQuantity", "BuildEquipmentComparisonForCandidate")
    require("src/Roentgen.App/Phase20ScreenFactory.cs",
            "Merchant(\n", "YOUR INVENTORY", "EQUIPMENT COMPARISON", "MapTabs", "JournalTabs", "CURRENT STEP", "KNOWN PEOPLE AND PLACES")
    require("src/Roentgen.UI/UiFoundation.cs", "MapViewport", "MapMarker")
    require("src/Roentgen.UI/WorldMapModel.cs", "MapFilter", "QuestTarget", "TrackedId", "Zoom", "PanX", "PanZ", "SelectedId")
    require("src/Roentgen.UI/Phase20UiRenderer.cs",
            "DrawMerchantTrade", "map:viewport", "map-marker:", "DRAG TO PAN", "Mouse wheel zooms")
    require("src/Roentgen.UI/Phase16NarrativeScreens.cs",
            "JournalFilter", "QuestReferenceRow", "CurrentObjective", "RouteHints", "ConsequenceHints")
    require("src/Roentgen.UI/Phase8HudRenderer.cs", "QuestTrackerHudModel")
    require("src/Roentgen.App/RoentgenGame.cs",
            "merchantPane", "HandleMerchantScreenInput", "HandleMerchantAction", "mapFilter", "HandleMapPointerInput",
            "TryGetQuestMapTarget", "JournalDisplayName", "trackedQuestId", "HandleJournalAction", "BuildTrackedQuestHud")
    forbid("src/Roentgen.App/RoentgenGame.cs", "merchantBuying", "trackedSettlementId")

    require("docs/PLAYER_GUIDE.md", "Roentgen Focus", "Inventory")
    print("UI Phases 11-13 verification PASSED: two-pane trading, interactive atlas navigation, and quest journal/tracking paths are present.")
    return 0

if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"UI Phases 11-13 verification FAILED: {exc}")
        raise SystemExit(1)
