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
    require("src/Roentgen.UI/Phase15SocietyScreens.cs",
            "FactionScreenSection", "SocietyUiText", "Ideological affinity", "Known criminal concern",
            "BuildFactionDetail", "BuildSettlement", "BuildReputation")
    require("src/Roentgen.UI/Phase20Models.cs",
            "FactionShowOnMap", "DialogueConversation", "CompanionScreenSection")
    require("src/Roentgen.App/Phase20ScreenFactory.cs",
            "FactionTabs", "CompanionTabs", "Factions(Phase15SocietyRuntime", "BuildFactionReputationPanel",
            "BuildFactionRelationsPanel", "BuildFactionCrimesPanel", "BuildFactionTradePanel",
            "Show on Map", "Prefer Ranged Weapons", "RECENT ORDERS")
    require("src/Roentgen.App/Phase10CombatController.cs",
            "CompanionInterfaceSnapshot", "BuildCompanionInterfaceSnapshot", "CurrentOrder", "RecentOrders")
    require("src/Roentgen.Narrative/NarrativeRuntimeState.cs", "SkillLabel")
    require("src/Roentgen.Narrative/DialogueEngine.cs", "choice.Skill?.ToString()")
    require("src/Roentgen.UI/Phase20UiRenderer.cs",
            "DrawDialogueConversation", "ACTIVE SPEAKER", "CONVERSATION HISTORY", "1-9 QUICK SELECT")
    require("src/Roentgen.App/RoentgenGame.cs",
            "factionSection", "companionSection", "dialogueHistory", "ShowSelectedSettlementOnMap",
            "HandleDialogueShortcutInput", "EnsureDialogueHistory", "AppendDialogueHistory",
            "CompanionCommandKind.PreferRanged", "CompanionCommandKind.PreferMelee")
    forbid("src/Roentgen.App/RoentgenGame.cs", "_ => CompanionCommandKind.Follow")

    require("docs/PLAYER_GUIDE.md", "Roentgen Focus", "Inventory")
    print("UI Phases 14-16 verification PASSED: faction/settlement views, conversation UX, and companion interface paths are present.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"UI Phases 14-16 verification FAILED: {exc}")
        raise SystemExit(1)
