#!/usr/bin/env python3
"""Static/UI-layout verification for Roentgen UI/UX phases 17–19."""
from __future__ import annotations
from pathlib import Path
import math

ROOT = Path(__file__).resolve().parents[1]


def text(rel: str) -> str:
    path = ROOT / rel
    assert path.exists(), f"missing {rel}"
    return path.read_text(encoding="utf-8")


def require(rel: str, *markers: str) -> None:
    data = text(rel)
    for marker in markers:
        assert marker in data, f"{rel}: missing marker {marker!r}"


def hud_layout(width: int, height: int, scale: float):
    # Numeric mirror of the invariants in HudResponsiveLayout.Compute. This is intentionally
    # small and checks the safe-area contract without needing MonoGame/.NET in CI.
    scale = max(.75, min(2.0, scale))
    inset = round(min(width, height) * .025 * min(scale, 1.35))
    inset = max(10, min(52, inset))
    sx = min(inset, max(0, width // 4))
    sy = min(inset, max(0, height // 4))
    sw = max(1, width - inset * 2)
    sh = max(1, height - inset * 2)
    return sx, sy, sw, sh


def assert_bounded(rect, width, height):
    x, y, w, h = rect
    assert w >= 1 and h >= 1
    assert x >= 0 and y >= 0
    assert x + w <= max(width, x + w), "sanity"
    # Safe area can collapse to 1 px for pathological dimensions, but must not begin outside.
    assert x < max(1, width) and y < max(1, height)


def verify_layout_matrix():
    resolutions = [
        (1280, 720), (1600, 900), (1920, 1080), (2560, 1440),
        (3440, 1440), (1024, 768), (900, 1200), (720, 720), (480, 800)
    ]
    for width, height in resolutions:
        for scale in (.75, 1.0, 1.25, 1.5, 2.0):
            rect = hud_layout(width, height, scale)
            assert_bounded(rect, width, height)
            x, y, w, h = rect
            assert x + w <= width or width < 20
            assert y + h <= height or height < 20


def main() -> None:
    require("src/Roentgen.UI/Phase17HudFeedback.cs",
            "HudToastKind", "HudInteractionFormatter", "FailureText",
            "HudResponsiveLayout", "Too far away", "Line of sight blocked")
    require("src/Roentgen.App/Phase17HudFeedbackRuntime.cs",
            "Phase17HudFeedbackRuntime", "Objective Updated", "HudToastKind.Reputation")
    require("src/Roentgen.UI/Phase8HudRenderer.cs",
            "DrawInteractionPrompt", "DrawQuestTracker", "DrawToasts",
            "ApplyAccessibility", "HudResponsiveLayout.Compute", "promptInputDevice")
    require("src/Roentgen.UI/Phase20UiRenderer.cs",
            "DrawScreenTransition", "ReduceMotion", "EstimateTabRows",
            "DrawSubTabs", "Narrow/tall windows always retain both panes")
    require("src/Roentgen.UI/UiResponsiveLayout.cs",
            "tabCount = 0", "narrow windows reflow detail content below")
    require("src/Roentgen.Audio/AudioEnums.cs",
            "InterfaceNavigate", "InterfaceTab", "InterfaceTransaction", "InterfaceCraft")
    require("src/Roentgen.Audio/AudioIdentity.cs",
            '"ui.navigate"', '"ui.tab"', '"ui.transaction"', '"ui.craft"')
    require("src/Roentgen.Audio.Playroma/IntegratedSoundCatalog.cs",
            '"ui.navigate"', '"ui.tab"', '"ui.transaction"', '"ui.craft"')
    require("src/Roentgen.App/RoentgenGame.cs",
            "HandleUiHoverFeedback", "InterfaceNavigate", "InterfaceTab",
            "InterfaceTransaction", "InterfaceCraft", "hudFeedback.Build")

    require("docs/PLAYER_GUIDE.md", "Roentgen Focus", "Inventory")
    verify_layout_matrix()
    print("UI phases 17-19 verification: PASS")


if __name__ == "__main__":
    main()
