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
            raise RuntimeError(f"{path} is missing required UI-foundation marker: {needle}")


def main() -> int:
    require(
        "src/Roentgen.UI/UiFoundation.cs",
        "public sealed class UiHitMap",
        "public sealed record UiScreenState",
        "public sealed class UiScreenStateStore",
        "public sealed record UiBindingConflict",
        "public static class UiBindingFormatter",
        "public static class UiFocusNavigator",
    )
    require(
        "src/Roentgen.UI/UiResponsiveLayout.cs",
        "UiDetailPlacement.Right",
        "UiDetailPlacement.Below",
    )
    require(
        "src/Roentgen.UI/Phase20InputRouter.cs",
        "PointerDoubleClicked",
        "UiInputDevice ActiveDevice",
        "FirstPhysicalInput",
        "NewlyPressedMouseButton",
    )
    require(
        "src/Roentgen.UI/Phase20UiRenderer.cs",
        "UiHitMap hitMap",
        "UiControlKind.TabButton",
        "UiControlKind.ActionButton",
        "DrawActionBar",
        "DrawHoveredTooltip",
        "UiResponsiveLayout.Compute",
    )
    require(
        "src/Roentgen.App/RoentgenGame.cs",
        "UiScreenStateStore uiState",
        "HandleBindingCaptureInput",
        "ActivateBindingConflict",
        "ActivateTabIndex",
        "PointerDoubleClicked",
        "ApplyUiFoundation",
    )
    require(
        "src/Roentgen.App/Phase8InputAdapter.cs",
        "Phase20InputRouter.IsMouseButtonDown",
    )
    require(
        "src/Roentgen.App/Phase10InputAdapter.cs",
        "Phase20InputRouter.IsMouseButtonDown",
    )
    require(
        "src/Roentgen.UI/Phase8HudRenderer.cs",
        "settings.Accessibility.PersistentReticle",
        "UiBindingFormatter.BindingLabel",
    )
    require(
        "tests/Roentgen.Phase20.Tests/Phase20ContractTests.cs",
        "Ui_screen_state_remembers_selection_and_independent_scroll",
        "Responsive_layout_preserves_detail_panel_in_narrow_windows",
        "Binding_formatter_tracks_active_device",
    )
    require("UI_FOUNDATION_PHASES_0_4.md", "Phase 0", "Phase 1", "Phase 2", "Phase 3", "Phase 4")
    print("UI Foundation Phases 0-4 verification PASSED: controls, hit maps, responsive layout, state, mouse interaction, semantic prompts, and direct remapping are present.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"UI Foundation verification FAILED: {exc}")
        raise SystemExit(1)
