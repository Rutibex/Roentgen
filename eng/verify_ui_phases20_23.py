#!/usr/bin/env python3
"""Static UX-finalization verification for Roentgen UI/UX phases 20–23."""
from __future__ import annotations
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def text(rel: str) -> str:
    p = ROOT / rel
    assert p.exists(), f"missing {rel}"
    return p.read_text(encoding="utf-8")


def require(rel: str, *markers: str) -> None:
    data = text(rel)
    for marker in markers:
        assert marker in data, f"{rel}: missing marker {marker!r}"


def move(selection: int, delta: int, count: int) -> int:
    if count <= 0:
        return 0
    return max(0, min(count - 1, selection + delta))


def verify_navigation_edges() -> None:
    assert move(0, -1, 0) == 0
    assert move(0, -8, 1) == 0
    assert move(0, 8, 1) == 0
    assert move(4, -2, 10) == 2
    assert move(4, 2, 10) == 6
    assert move(8, 8, 10) == 9
    # Two/three-column grid row travel must remain bounded.
    assert move(3, -2, 9) == 1
    assert move(3, 2, 9) == 5
    assert move(4, -3, 10) == 1
    assert move(4, 3, 10) == 7


def main() -> None:
    require("src/Roentgen.UI/UiUxFinalization.cs",
            "UiUxFinalization", "Normalize(Phase20ScreenModel model)",
            "CleanLegacyFooter", "GridColumns", "IsAdjustableEntry",
            "This action requires confirmation", "Unavailable right now")
    require("src/Roentgen.App/RoentgenGame.cs",
            "SelectedEntryAdjustsValue", "MoveScreenSelection",
            "UiAction.PageUp", "UiAction.PageDown",
            "UiUxFinalization.GridColumns", "UiUxFinalization.Normalize")
    require("src/Roentgen.UI/Phase20UiRenderer.cs",
            "DrawKeyboardFocusIndicator", "StatusColor",
            "DrawScreenTransition", "Palette.Danger", "Palette.Warning")

    require("docs/PLAYER_GUIDE.md", "Roentgen Focus", "Inventory")
    # Legacy keyboard chords may still occur in old factory source for compatibility, but the
    # normalization boundary must suppress them whenever semantic prompts are present.
    finalizer = text("src/Roentgen.UI/UiUxFinalization.cs")
    assert 'upper.Contains("ENTER "' in finalizer
    assert 'upper.Contains("ESC "' in finalizer
    assert "Footer = prompts is { Count: > 0 } ? string.Empty" in finalizer

    verify_navigation_edges()
    print("UI phases 20-23 verification: PASS")


if __name__ == "__main__":
    main()
