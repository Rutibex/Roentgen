using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roentgen.Focus;

namespace Roentgen.UI;

public sealed class Phase20UiRenderer : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly SpriteBatch batch;
    private readonly Texture2D pixel;
    private readonly Texture2D? bodyFont;
    private readonly UiHitMap hitMap = new();
    private bool disposed;
    public WorldMapModel? WorldMap { get; set; }
    private Texture2D? mapTexture;
    private string? mapIdentity;

    private float scale = 1f;
    private bool highContrast;
    private ColorVisionMode colorVisionMode;
    private Phase20UserSettings currentSettings = new();
    private UiInteractionSnapshot interaction = new(Point.Zero, UiInputDevice.Keyboard);
    private double currentInterfaceTimeSeconds;
    private double screenTransitionStartSeconds;
    private Phase20ScreenKind transitionScreen = Phase20ScreenKind.None;

    public int LastVisibleEntryCount { get; private set; } = 1;
    public IReadOnlyList<UiHitRegion> HitRegions => hitMap.Regions;
    public Phase20ScreenKind RenderedScreenKind => hitMap.ScreenKind;

    public Phase20UiRenderer(GraphicsDevice device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        batch = new SpriteBatch(device);
        pixel = new Texture2D(device, 1, 1);
        pixel.SetData([Color.White]);
        string fontPath = Path.Combine(AppContext.BaseDirectory, "content", "ui", "body-font.png");
        if (File.Exists(fontPath)) { using var stream = File.OpenRead(fontPath); bodyFont = Texture2D.FromStream(device, stream); }
    }

    public UiHitRegion? HitTest(Point pointer) => hitMap.HitTest(pointer);
    public UiHitRegion? HitTest(Point pointer, UiControlKind kind) => hitMap.HitTest(pointer, kind);
    public int HitTestEntry(Point pointer) => hitMap.HitTest(pointer, UiControlKind.ListItem)?.Index ?? -1;
    public void InvalidateHitMap() => hitMap.Clear();

    public void DrawScreen(Phase20ScreenModel model, Phase20UserSettings settings, UiInteractionSnapshot interactionSnapshot, double interfaceTimeSeconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(settings);
        Phase20AccessibilitySettings validated = settings.Accessibility.Validate();
        currentSettings = settings.Validate();
        interaction = interactionSnapshot;
        currentInterfaceTimeSeconds = interfaceTimeSeconds;
        if (transitionScreen != model.Kind) { transitionScreen = model.Kind; screenTransitionStartSeconds = interfaceTimeSeconds; }
        scale = Math.Clamp(validated.InterfaceScale * (model.Kind == Phase20ScreenKind.Dialogue ? validated.SubtitleScale : 1f), 0.75f, 2f);
        highContrast = validated.HighContrast;
        colorVisionMode = validated.ColorVisionMode;
        hitMap.Begin(model.Kind);
        int width = device.Viewport.Width;
        int height = device.Viewport.Height;
        batch.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.AlphaBlend);
        if (model.DimWorld) Fill(new Rectangle(0, 0, width, height), new Color(0, 0, 0, model.Kind == Phase20ScreenKind.MainMenu ? 245 : 185));

        if (model.Kind == Phase20ScreenKind.MainMenu)
            DrawMainMenu(model, width, height, interfaceTimeSeconds);
        else if (model.Kind == Phase20ScreenKind.Dialogue && model.Presentation == Phase20ScreenPresentation.DialogueConversation)
            DrawDialogueConversation(model, width, height);
        else if (model.Kind == Phase20ScreenKind.Merchant && model.Presentation == Phase20ScreenPresentation.MerchantTrade)
            DrawMerchantTrade(model, width, height);
        else if (model.Kind == Phase20ScreenKind.Map && WorldMap is not null)
            DrawWorldMap(model, width, height);
        else
            DrawGeneric(model, width, height, model.Kind == Phase20ScreenKind.RoentgenFocus);

        DrawKeyboardFocusIndicator(model.FocusedControlId);
        DrawScreenTransition(width, height);
        DrawHoveredTooltip(width, height);
        if (interaction.DebugOverlay) DrawInterfaceDiagnostics(width, height, model);
        batch.End();
    }

    public void DrawFocus(Phase20FocusModel model, Phase20UserSettings settings, UiInteractionSnapshot interactionSnapshot, double interfaceTimeSeconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(settings);
        Phase20AccessibilitySettings accessibility = settings.Accessibility;
        Phase20AccessibilitySettings validated = accessibility.Validate();
        currentSettings = settings.Validate();
        interaction = interactionSnapshot;
        currentInterfaceTimeSeconds = interfaceTimeSeconds;
        scale = validated.InterfaceScale;
        highContrast = validated.HighContrast;
        colorVisionMode = validated.ColorVisionMode;
        hitMap.Begin(Phase20ScreenKind.RoentgenFocus);
        int width = device.Viewport.Width;
        int height = device.Viewport.Height;
        int intensity = Math.Clamp(accessibility.FocusOverlayIntensityPermille, 0, 1000);
        byte alpha = (byte)(120 + intensity * 100 / 1000);
        batch.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.AlphaBlend);
        Fill(new Rectangle(0, 0, width, height), new Color((byte)2, (byte)21, (byte)17, alpha));
        Color accent = Palette.FocusAccent(highContrast);
        DrawCornerBrackets(new Rectangle(18, 18, width - 36, height - 36), accent);
        Text("ROENTGEN FOCUS", 34, 30, 3, accent);
        Text($"MODE {model.Snapshot.Mode}   FROZEN TICK {model.Snapshot.FrozenTick}", 36, 60, 1, Palette.Muted(highContrast));
        int resourceWidth = Math.Max(1, width / 4);
        int resourceFill = resourceWidth * model.Snapshot.Resource.AvailableMilliunits / Math.Max(1, model.Snapshot.Resource.MaximumMilliunits);
        Fill(new Rectangle(width - resourceWidth - 36, 34, resourceWidth, 18), Palette.PanelDark(highContrast));
        Fill(new Rectangle(width - resourceWidth - 34, 36, Math.Max(0, resourceFill - 4), 14), accent);
        Text($"FOCUS {model.Snapshot.Resource.AvailableMilliunits / 1000.0:F1}/{model.Snapshot.Resource.MaximumMilliunits / 1000.0:F1}", width - resourceWidth - 34, 58, 1, Palette.Primary(highContrast));

        int leftWidth = Math.Clamp(width / 3, 280, 520);
        int rightWidth = Math.Clamp(width / 3, 300, 560);
        Rectangle targetPanel = new(28, 90, leftWidth, height - 150);
        Rectangle forecastPanel = new(width - rightWidth - 28, 90, rightWidth, (height - 170) / 2);
        Rectangle queuePanel = new(width - rightWidth - 28, forecastPanel.Bottom + 16, rightWidth, height - forecastPanel.Bottom - 76);
        Panel(targetPanel, $"TARGET: {model.TargetName}", accent);
        Panel(forecastPanel, $"DIAGNOSIS: {model.SelectedPartName}", accent);
        Panel(queuePanel, "COMMITTED SEQUENCE", accent);
        DrawEntries(model.TargetParts, model.SelectedPartIndex, targetPanel, true, accent, 0, "focus-parts");
        DrawLines(model.ForecastLines, forecastPanel, 0, Palette.Primary(highContrast), 0, "focus-forecast");
        DrawLines(model.QueueLines.Count == 0 ? ["NO ACTIONS QUEUED"] : model.QueueLines, queuePanel, 0, model.QueueLines.Count == 0 ? Palette.Muted(highContrast) : Palette.Primary(highContrast), 0, "focus-queue");
        TextWrapped(model.Status, 36, height - 56, Math.Max(20, (width - 72) / CellWidth(1)), 1, Palette.Warning(highContrast));
        DrawActionBar([
            new Phase20ActionPrompt(UiAction.FocusPreviousTarget, "Prev Target"),
            new Phase20ActionPrompt(UiAction.FocusNextTarget, "Next Target"),
            new Phase20ActionPrompt(UiAction.FocusPreviousPart, "Prev Part"),
            new Phase20ActionPrompt(UiAction.FocusNextPart, "Next Part"),
            new Phase20ActionPrompt(UiAction.FocusQueue, "Queue"),
            new Phase20ActionPrompt(UiAction.FocusRemove, "Remove"),
            new Phase20ActionPrompt(UiAction.FocusCommit, "Commit"),
            new Phase20ActionPrompt(UiAction.Cancel, "Cancel")
        ], new Rectangle(28, height - 34, width - 56, 28));
        DrawHoveredTooltip(width, height);
        batch.End();
    }

    private void DrawDialogueConversation(Phase20ScreenModel model, int width, int height)
    {
        Color accent = Palette.Accent(highContrast);
        int margin = Math.Clamp((int)(24 * scale), 16, 42);
        int gap = Math.Clamp((int)(12 * scale), 8, 22);
        int actionHeight = Math.Clamp((int)(34 * scale), 30, 56);
        Rectangle frame = new(margin, margin, Math.Max(320, width - margin * 2), Math.Max(300, height - margin * 2));
        Fill(frame, Palette.Panel(highContrast));
        Border(frame, accent, 2);
        DrawCornerBrackets(frame, accent);

        Rectangle actionArea = new(frame.X + 12, frame.Bottom - actionHeight - 10, frame.Width - 24, actionHeight);
        Rectangle statusArea = new(frame.X + 18, actionArea.Y - 28, frame.Width - 36, 22);
        Rectangle content = new(frame.X + 14, frame.Y + 14, frame.Width - 28, Math.Max(180, statusArea.Y - frame.Y - 24));

        bool wide = content.Width >= 920;
        Rectangle speakerArea;
        Rectangle conversationArea;
        if (wide)
        {
            int speakerWidth = Math.Clamp(content.Width * 23 / 100, 220, 340);
            speakerArea = new(content.X, content.Y, speakerWidth, content.Height);
            conversationArea = new(speakerArea.Right + gap, content.Y, Math.Max(320, content.Right - speakerArea.Right - gap), content.Height);
        }
        else
        {
            int speakerHeight = Math.Clamp((int)(92 * scale), 74, 126);
            speakerArea = new(content.X, content.Y, content.Width, speakerHeight);
            conversationArea = new(content.X, speakerArea.Bottom + gap, content.Width, Math.Max(100, content.Bottom - speakerArea.Bottom - gap));
        }

        Panel(speakerArea, "SPEAKER", accent);
        string initials = Initials(model.Title);
        if (wide)
        {
            Rectangle portrait = new(speakerArea.X + 22, speakerArea.Y + 52, speakerArea.Width - 44, Math.Clamp(speakerArea.Width - 44, 120, 230));
            Fill(portrait, Palette.Panel(highContrast));
            Border(portrait, accent, 2);
            TextCentered(initials, portrait.Center.X, portrait.Center.Y - Math.Clamp((int)(20 * scale), 14, 28), Math.Clamp((int)Math.Round(scale * 2), 2, 3), accent);
            TextCentered("ACTIVE SPEAKER", speakerArea.Center.X, portrait.Bottom + 18, 1, Palette.Warning(highContrast));
            TextWrapped(model.Title.ToUpperInvariant(), speakerArea.X + 18, portrait.Bottom + 44, Math.Max(10, (speakerArea.Width - 36) / CellWidth(1)), 1, Palette.Primary(highContrast));
        }
        else
        {
            Rectangle portrait = new(speakerArea.X + 18, speakerArea.Y + 40, 48, 40);
            Fill(portrait, Palette.Panel(highContrast));
            Border(portrait, accent);
            TextCentered(initials, portrait.Center.X, portrait.Y + 10, 1, accent);
            Text(model.Title.ToUpperInvariant(), portrait.Right + 16, speakerArea.Y + 48, Math.Max(1, (int)Math.Round(scale)), Palette.Primary(highContrast));
            TextRight("ACTIVE SPEAKER", speakerArea.Right - 18, speakerArea.Y + 50, 1, Palette.Warning(highContrast));
        }

        int currentHeight = Math.Clamp(conversationArea.Height * 27 / 100, 100, 190);
        int historyHeight = Math.Clamp(conversationArea.Height * 27 / 100, 100, 210);
        int minimumResponses = 120;
        if (currentHeight + historyHeight + gap * 2 + minimumResponses > conversationArea.Height)
        {
            currentHeight = Math.Max(80, (conversationArea.Height - gap * 2) / 4);
            historyHeight = currentHeight;
        }
        Rectangle currentArea = new(conversationArea.X, conversationArea.Y, conversationArea.Width, currentHeight);
        Rectangle historyArea = new(conversationArea.X, currentArea.Bottom + gap, conversationArea.Width, historyHeight);
        Rectangle responseArea = new(conversationArea.X, historyArea.Bottom + gap, conversationArea.Width, Math.Max(70, conversationArea.Bottom - historyArea.Bottom - gap));

        Panel(currentArea, "CURRENT", accent);
        TextWrapped(model.Subtitle, currentArea.X + 16, currentArea.Y + 42, Math.Max(16, (currentArea.Width - 32) / CellWidth(1)), 1, Palette.Primary(highContrast));

        Panel(historyArea, model.Detail?.Title ?? "CONVERSATION HISTORY", accent);
        DrawLines(model.Detail?.Lines ?? [], historyArea, -1, Palette.Muted(highContrast), model.DetailScrollOffset, "detail-dialogue-history");

        Panel(responseArea, "YOUR RESPONSE  |  1-9 QUICK SELECT", accent);
        DrawEntries(model.Entries, model.SelectedIndex, responseArea, true, accent, model.ScrollOffset, "dialogue-responses", false, "entry");

        if (!string.IsNullOrWhiteSpace(model.Status))
            Text(Trim(model.Status, Math.Max(16, statusArea.Width / CellWidth(1))), statusArea.X, statusArea.Y, 1, Palette.Warning(highContrast));
        DrawActionBar(model.ActionPrompts, actionArea);
    }

    private static string Initials(string name)
    {
        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return "?";
        if (words.Length == 1) return words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant();
        return string.Concat(words.Take(2).Select(static word => char.ToUpperInvariant(word[0])));
    }

    private void DrawMerchantTrade(Phase20ScreenModel model, int width, int height)
    {
        UiLayoutMetrics layout = UiResponsiveLayout.Compute(width, height, scale, false, true, 0);
        Color accent = Palette.Accent(highContrast);
        Fill(layout.Frame, Palette.Panel(highContrast));
        Border(layout.Frame, accent, 2);
        DrawCornerBrackets(layout.Frame, accent);
        Text(model.Title, layout.HeaderArea.X + 4, layout.HeaderArea.Y + 3, 2, accent);
        Text(Trim(model.Subtitle, Math.Max(20, layout.HeaderArea.Width / CellWidth(1))), layout.HeaderArea.X + 6, layout.HeaderArea.Y + 34, 1, Palette.Muted(highContrast));

        Rectangle content = layout.ContentArea;
        int detailHeight = Math.Clamp(content.Height * 34 / 100, 150, 260);
        Rectangle tradeArea = new(content.X, content.Y, content.Width, Math.Max(120, content.Height - detailHeight - layout.Gap));
        Rectangle detailArea = new(content.X, tradeArea.Bottom + layout.Gap, content.Width, Math.Max(90, content.Bottom - tradeArea.Bottom - layout.Gap));
        int leftWidth = Math.Max(180, (tradeArea.Width - layout.Gap) / 2);
        Rectangle merchantPane = new(tradeArea.X, tradeArea.Y, leftWidth, tradeArea.Height);
        Rectangle playerPane = new(merchantPane.Right + layout.Gap, tradeArea.Y, Math.Max(180, tradeArea.Right - merchantPane.Right - layout.Gap), tradeArea.Height);

        Panel(merchantPane, model.SecondaryPaneActive ? "MERCHANT STOCK" : "MERCHANT STOCK — ACTIVE", accent);
        Panel(playerPane, model.SecondaryPaneActive ? (model.SecondaryTitle ?? "YOUR INVENTORY") + " — ACTIVE" : (model.SecondaryTitle ?? "YOUR INVENTORY"), accent);
        if (!model.SecondaryPaneActive) Border(merchantPane, accent, 2); else Border(playerPane, accent, 2);
        DrawEntries(model.Entries, model.SelectedIndex, merchantPane, true, accent, model.ScrollOffset, "merchant-stock", false, "merchant-entry");
        IReadOnlyList<Phase20MenuEntry> playerEntries = model.SecondaryEntries ?? [];
        DrawEntries(playerEntries, model.SecondarySelectedIndex, playerPane, true, accent, model.SecondaryScrollOffset, "player-stock", false, "player-entry");

        Panel(detailArea, model.Detail?.Title ?? "TRADE DETAILS", accent);
        DrawLines(model.Detail?.Lines ?? ["Select an item to inspect price and trade modifiers."], detailArea, -1, Palette.Primary(highContrast), model.DetailScrollOffset, "detail");
        if (!string.IsNullOrWhiteSpace(model.Status))
            Text(Trim(model.Status, Math.Max(16, layout.StatusArea.Width / CellWidth(1))), layout.StatusArea.X, layout.StatusArea.Y, 1, Palette.Warning(highContrast));
        DrawActionBar(model.ActionPrompts, layout.ActionArea);
    }

    private void DrawWorldMap(Phase20ScreenModel model, int width, int height)
    {
        WorldMapModel map = WorldMap!;
        if (mapTexture is null || mapIdentity != map.Identity)
        {
            mapTexture?.Dispose();
            mapTexture = new Texture2D(device, map.SampleCount, map.SampleCount);
            mapTexture.SetData(map.Colors.Select(c => new Color(c)).ToArray());
            mapIdentity = map.Identity;
        }

        UiLayoutMetrics layout = UiResponsiveLayout.Compute(width, height, scale, model.Tabs.Count > 0, false, model.Tabs.Count);
        Color accent = Palette.Accent(highContrast);
        Fill(layout.Frame, Palette.Panel(highContrast));
        Border(layout.Frame, accent, 2);
        DrawCornerBrackets(layout.Frame, accent);
        Text("WASTELAND ATLAS", layout.HeaderArea.X + 4, layout.HeaderArea.Y + 3, 3, accent);
        Text($"{map.WidthMeters / 1000f:0.0} X {map.DepthMeters / 1000f:0.0} KM  |  ZOOM {map.Zoom:0.0}X  |  DRAG TO PAN", layout.HeaderArea.X + 6, layout.HeaderArea.Y + 38, 1, Palette.Muted(highContrast));
        DrawTabs(model.Tabs, model.SelectedTab, layout.TabArea, accent);

        Rectangle content = layout.ContentArea;
        if (model.SubTabs is { Count: > 0 })
        {
            int subHeight = Math.Max(28, (int)(31 * scale));
            Rectangle subArea = new(content.X, content.Y, content.Width, Math.Min(subHeight, content.Height));
            DrawSubTabs(model.SubTabs, model.SelectedSubTab, subArea, accent);
            int offset = Math.Min(content.Height, subHeight + layout.Gap);
            content = new Rectangle(content.X, content.Y + offset, content.Width, Math.Max(1, content.Height - offset));
        }

        int listWidth = Math.Clamp(content.Width * 34 / 100, 270, 520);
        Rectangle viewport = new(content.X, content.Y, Math.Max(180, content.Width - listWidth - layout.Gap), content.Height);
        Rectangle side = new(viewport.Right + layout.Gap, content.Y, Math.Max(180, content.Right - viewport.Right - layout.Gap), content.Height);
        int detailHeight = model.Detail is null ? 0 : Math.Clamp(side.Height * 38 / 100, 130, 250);
        Rectangle list = model.Detail is null ? side : new Rectangle(side.X, side.Y, side.Width, Math.Max(100, side.Height - detailHeight - layout.Gap));
        Rectangle detail = model.Detail is null ? Rectangle.Empty : new Rectangle(side.X, list.Bottom + layout.Gap, side.Width, Math.Max(90, side.Bottom - list.Bottom - layout.Gap));

        float zoom = Math.Clamp(map.Zoom, 1f, 6f);
        float span = 1f / zoom;
        float minX = Math.Clamp(map.PanX - span / 2f, 0f, 1f - span);
        float minZ = Math.Clamp(map.PanZ - span / 2f, 0f, 1f - span);
        int sourceX = Math.Clamp((int)MathF.Floor(minX * map.SampleCount), 0, Math.Max(0, map.SampleCount - 1));
        int sourceY = Math.Clamp((int)MathF.Floor(minZ * map.SampleCount), 0, Math.Max(0, map.SampleCount - 1));
        int sourceW = Math.Clamp((int)MathF.Ceiling(span * map.SampleCount), 1, map.SampleCount - sourceX);
        int sourceH = Math.Clamp((int)MathF.Ceiling(span * map.SampleCount), 1, map.SampleCount - sourceY);
        batch.Draw(mapTexture, viewport, new Rectangle(sourceX, sourceY, sourceW, sourceH), Color.White);
        Border(viewport, accent, 2);
        hitMap.Add(new UiHitRegion("map:viewport", UiControlKind.MapViewport, viewport, true, ZOrder: 2, Tooltip: "Mouse wheel zooms. Hold the left mouse button and drag to pan."));

        Vector2 Project(float x, float z) => new(viewport.X + (x - minX) / span * viewport.Width, viewport.Y + (z - minZ) / span * viewport.Height);
        bool InView(float x, float z) => x >= minX && x <= minX + span && z >= minZ && z <= minZ + span;
        foreach (MapRoad road in map.Roads)
        {
            for (int i = 1; i < road.Points.Count; i++)
            {
                (float ax, float az) = road.Points[i - 1];
                (float bx, float bz) = road.Points[i];
                if (!InView(ax, az) || !InView(bx, bz)) continue;
                Line(Project(ax, az), Project(bx, bz), new Color(195, 176, 122), 2);
            }
        }

        HashSet<string> visibleIds = model.Entries.Select(static entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        foreach (MapMarker marker in map.Markers)
        {
            if (!visibleIds.Contains(marker.Id) || !InView(marker.X, marker.Z)) continue;
            Vector2 point = Project(marker.X, marker.Z);
            bool tracked = marker.Id == map.TrackedId;
            bool selected = marker.Id == map.SelectedId;
            Color markerColor = tracked ? Palette.Warning(highContrast)
                : marker.QuestTarget ? new Color(225, 186, 112)
                : marker.Kind.Equals("Settlement", StringComparison.OrdinalIgnoreCase) ? new Color(130, 225, 188)
                : marker.Kind.Equals("Ruin", StringComparison.OrdinalIgnoreCase) ? new Color(225, 145, 112)
                : new Color(150, 192, 218);
            Rectangle markerBounds = new((int)point.X - 6, (int)point.Y - 6, 12, 12);
            Fill(markerBounds, markerColor);
            if (tracked) Border(new Rectangle(markerBounds.X - 5, markerBounds.Y - 5, markerBounds.Width + 10, markerBounds.Height + 10), markerColor, 2);
            if (selected) Border(new Rectangle(markerBounds.X - 9, markerBounds.Y - 9, markerBounds.Width + 18, markerBounds.Height + 18), Palette.Primary(highContrast), 1);
            int entryIndex = model.Entries.Select((entry, index) => (entry, index)).First(pair => pair.entry.Id == marker.Id).index;
            hitMap.Add(new UiHitRegion($"map-marker:{marker.Id}", UiControlKind.MapMarker, new Rectangle(markerBounds.X - 5, markerBounds.Y - 5, 22, 22), true, entryIndex, ZOrder: 35, Tooltip: $"{marker.Name} — {(marker.QuestTarget ? "quest objective" : marker.Kind)}"));
        }

        if (InView(map.PlayerX, map.PlayerZ))
        {
            Vector2 player = Project(map.PlayerX, map.PlayerZ);
            Vector2 forward = new(MathF.Sin(map.HeadingRadians), -MathF.Cos(map.HeadingRadians));
            Vector2 right = new(-forward.Y, forward.X);
            Line(player + forward * 10, player - forward * 6 + right * 5, Color.White, 2);
            Line(player + forward * 10, player - forward * 6 - right * 5, Color.White, 2);
            Line(player - forward * 6 + right * 5, player - forward * 6 - right * 5, Color.White, 2);
        }

        Panel(list, "LOCATIONS", accent);
        DrawEntries(model.Entries, model.SelectedIndex, list, true, accent, model.ScrollOffset, "entries");
        if (model.Detail is not null)
        {
            Panel(detail, model.Detail.Title, accent);
            DrawLines(model.Detail.Lines, detail, model.Detail.SelectedLine, Palette.Primary(highContrast), model.DetailScrollOffset, "detail");
        }
        Text("WHITE: YOU   AMBER: QUEST   HIGHLIGHT: WAYPOINT", layout.StatusArea.X, layout.StatusArea.Y, 1, Palette.Primary(highContrast));
        if (!string.IsNullOrWhiteSpace(model.Status)) TextRight(Trim(model.Status, Math.Max(12, layout.StatusArea.Width / CellWidth(1) / 2)), layout.StatusArea.Right, layout.StatusArea.Y, 1, Palette.Warning(highContrast));
        DrawActionBar(model.ActionPrompts, layout.ActionArea);
    }

    private void Line(Vector2 a, Vector2 b, Color color, int thickness)
    {
        Vector2 delta = b - a;
        batch.Draw(pixel, a, null, color, MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(delta.Length(), thickness), SpriteEffects.None, 0);
    }

    private void DrawMainMenu(Phase20ScreenModel model, int width, int height, double time)
    {
        Color accent = Palette.Accent(highContrast);
        int titleScale = width < 900 ? 3 : 4;
        int titleWidth = Measure(model.Title, titleScale);
        int titleX = Math.Max(42, width / 2 - titleWidth / 2);
        int titleY = Math.Max(50, height / 8);
        Text(model.Title, titleX + 3, titleY + 3, titleScale, new Color(0, 0, 0, 170));
        Text(model.Title, titleX, titleY, titleScale, accent);
        int pulse = currentSettings.Accessibility.ReduceMotion ? 104 : 90 + (int)(Math.Sin(time * 1.8) * 22);
        Fill(new Rectangle(width / 2 - Math.Min(340, width / 3), titleY + titleScale * 20 + 12, Math.Min(680, width * 2 / 3), 2), new Color(accent.R, accent.G, accent.B, pulse));
        TextCentered(model.Subtitle, width / 2, titleY + titleScale * 20 + 26, 1, Palette.Muted(highContrast));
        int availableWidth = Math.Max(1, width - 48);
        int menuWidth = Math.Min(availableWidth, Math.Clamp(width / 3, Math.Min(330, availableWidth), Math.Min(560, availableWidth)));
        int menuTop = Math.Min(Math.Max(12, height - 80), titleY + titleScale * 20 + 70);
        int availableHeight = Math.Max(1, height - menuTop - 66);
        int desiredMenuHeight = Math.Max(Math.Min(180, availableHeight), model.Entries.Count * RowHeight() + 50);
        int menuHeight = Math.Min(availableHeight, desiredMenuHeight);
        Rectangle menu = new(Math.Max(0, width / 2 - menuWidth / 2), menuTop, menuWidth, menuHeight);
        Fill(menu, Palette.Panel(highContrast));
        Border(menu, accent, 2);
        DrawEntries(model.Entries, model.SelectedIndex, menu, false, accent, model.ScrollOffset, "entries");
        TextCentered(model.Status, width / 2, menu.Bottom + 16, 1, Palette.Warning(highContrast));
        DrawActionBar(model.ActionPrompts, new Rectangle(28, height - 48, width - 56, 32));
    }

    private void DrawGeneric(Phase20ScreenModel model, int width, int height, bool focusTheme)
    {
        Color accent = focusTheme ? Palette.FocusAccent(highContrast) : Palette.Accent(highContrast);
        UiLayoutMetrics layout = UiResponsiveLayout.Compute(width, height, scale, model.Tabs.Count > 0, model.Detail is not null, model.Tabs.Count);
        Fill(layout.Frame, Palette.Panel(highContrast));
        Border(layout.Frame, accent, 2);
        DrawCornerBrackets(layout.Frame, accent);
        Text(model.Title, layout.HeaderArea.X + 4, layout.HeaderArea.Y + 3, 2, accent);
        Text(model.Subtitle, layout.HeaderArea.X + 6, layout.HeaderArea.Y + 34, 1, Palette.Muted(highContrast));
        int headerRuleY = Math.Min(layout.HeaderArea.Bottom - 2, layout.HeaderArea.Y + Math.Max(54, (int)(58 * scale)));
        if (headerRuleY > layout.HeaderArea.Y) Fill(new Rectangle(layout.HeaderArea.X + 4, headerRuleY, Math.Max(1, layout.HeaderArea.Width - 8), 1), Palette.Border(highContrast));
        DrawMeters(model.Meters, layout.HeaderArea, accent);
        DrawTabs(model.Tabs, model.SelectedTab, layout.TabArea, accent);

        Rectangle contentArea = layout.ContentArea;
        if (model.SubTabs is { Count: > 0 })
        {
            int subRowHeight = Math.Max(28, (int)(31 * scale));
            int subRows = EstimateTabRows(model.SubTabs, contentArea.Width, subRowHeight);
            int subHeight = Math.Min(contentArea.Height, subRows * subRowHeight + Math.Max(0, subRows - 1) * 4);
            Rectangle subArea = new(contentArea.X, contentArea.Y, contentArea.Width, Math.Max(1, subHeight));
            DrawSubTabs(model.SubTabs, model.SelectedSubTab, subArea, accent);
            int offset = Math.Min(contentArea.Height, subHeight + layout.Gap);
            contentArea = new Rectangle(contentArea.X, contentArea.Y + offset, contentArea.Width, Math.Max(1, contentArea.Height - offset));
        }

        (Rectangle entriesArea, Rectangle detailArea) = ReflowContent(contentArea, layout.DetailPlacement, model.Detail is not null, layout.Gap);
        Fill(entriesArea, Palette.PanelDark(highContrast));
        Border(entriesArea, Palette.Border(highContrast));
        DrawEntryCollection(model, entriesArea, accent);

        if (model.Detail is not null && layout.DetailPlacement != UiDetailPlacement.None && detailArea.Width > 0 && detailArea.Height > 0)
        {
            Panel(detailArea, model.Detail.Title, accent);
            DrawLines(model.Detail.Lines, detailArea, model.Detail.SelectedLine, Palette.Primary(highContrast), model.DetailScrollOffset, "detail");
            if (!string.IsNullOrWhiteSpace(model.Detail.Status))
                TextWrapped(model.Detail.Status, detailArea.X + 14, detailArea.Bottom - 34, Math.Max(10, (detailArea.Width - 28) / CellWidth(1)), 1, StatusColor(model.Detail.Status));
        }

        TextWrapped(model.Status, layout.StatusArea.X, layout.StatusArea.Y, Math.Max(20, layout.StatusArea.Width / CellWidth(1)), 1, StatusColor(model.Status));
        DrawActionBar(model.ActionPrompts, layout.ActionArea);
        if ((model.ActionPrompts is null || model.ActionPrompts.Count == 0) && !string.IsNullOrWhiteSpace(model.Footer))
            TextRight(model.Footer.ToUpperInvariant(), layout.ActionArea.Right, layout.ActionArea.Y, 1, Palette.Muted(highContrast));
    }

    private static (Rectangle Entries, Rectangle Detail) ReflowContent(Rectangle content, UiDetailPlacement placement, bool hasDetail, int gap)
    {
        if (!hasDetail || placement == UiDetailPlacement.None || content.Width <= 1 || content.Height <= 1)
            return (content, Rectangle.Empty);

        int boundedGap = Math.Clamp(gap, 0, Math.Max(0, Math.Min(content.Width, content.Height) / 4));
        if (placement == UiDetailPlacement.Right && content.Width >= 360)
        {
            int available = Math.Max(2, content.Width - boundedGap);
            int detailWidth = Math.Clamp((int)(available * 0.42f), Math.Min(160, available / 2), Math.Max(1, available - 160));
            int entryWidth = Math.Max(1, available - detailWidth);
            Rectangle entries = new(content.X, content.Y, entryWidth, content.Height);
            Rectangle detail = new(entries.Right + boundedGap, content.Y, Math.Max(1, content.Right - entries.Right - boundedGap), content.Height);
            return (entries, detail);
        }

        // Narrow/tall windows always retain both panes by stacking them.
        int availableHeight = Math.Max(2, content.Height - boundedGap);
        int entriesHeight = Math.Clamp((int)(availableHeight * 0.52f), Math.Min(60, availableHeight / 2), Math.Max(1, availableHeight - Math.Min(60, availableHeight / 2)));
        Rectangle belowEntries = new(content.X, content.Y, content.Width, entriesHeight);
        int detailY = Math.Min(content.Bottom, belowEntries.Bottom + boundedGap);
        Rectangle belowDetail = new(content.X, detailY, content.Width, Math.Max(1, content.Bottom - detailY));
        return (belowEntries, belowDetail);
    }

    private void DrawEntryCollection(Phase20ScreenModel model, Rectangle panel, Color accent)
    {
        switch (model.Presentation)
        {
            case Phase20ScreenPresentation.CardGridTwoColumns:
                DrawCardGrid(model.Entries, model.SelectedIndex, panel, accent, model.ScrollOffset, 2);
                break;
            case Phase20ScreenPresentation.CardGridThreeColumns:
                DrawCardGrid(model.Entries, model.SelectedIndex, panel, accent, model.ScrollOffset, 3);
                break;
            default:
                DrawEntries(model.Entries, model.SelectedIndex, panel, false, accent, model.ScrollOffset, "entries", model.TextEntryActive);
                break;
        }
    }

    private int EstimateTabRows(IReadOnlyList<string> tabs, int width, int rowHeight)
    {
        if (tabs.Count == 0 || width <= 0) return 0;
        int rows = 1, x = 0;
        foreach (string raw in tabs)
        {
            int tabWidth = Math.Min(width, Measure(raw.ToUpperInvariant(), 1) + 22);
            if (x > 0 && x + tabWidth > width) { rows++; x = 0; }
            x += tabWidth + 5;
        }
        return Math.Clamp(rows, 1, 6);
    }

    private void DrawSubTabs(IReadOnlyList<string> tabs, int selectedTab, Rectangle area, Color accent)
    {
        if (tabs.Count == 0 || area.Height <= 0) return;
        int x = area.X;
        int y = area.Y;
        int rowHeight = Math.Max(26, (int)(28 * scale));
        for (int index = 0; index < tabs.Count; index++)
        {
            string tab = tabs[index].ToUpperInvariant();
            int width = Math.Min(area.Width, Measure(tab, 1) + 22);
            if (x + width > area.Right && x > area.X) { x = area.X; y += rowHeight + 4; }
            if (y + rowHeight > area.Bottom) break;
            if (width <= 1) break;
            Rectangle bounds = new(x, y, Math.Min(width, area.Right - x), rowHeight);
            bool hovered = bounds.Contains(interaction.Pointer);
            bool selected = index == selectedTab;
            Fill(bounds, selected || hovered ? Palette.Selection(highContrast) : Palette.PanelDark(highContrast));
            Border(bounds, selected ? accent : Palette.Border(highContrast));
            Text(Trim(tab, Math.Max(4, (bounds.Width - 16) / CellWidth(1))), bounds.X + 8, bounds.Y + 8, 1, selected ? Palette.Primary(highContrast) : Palette.Muted(highContrast));
            hitMap.Add(new UiHitRegion($"subtab:{index}", UiControlKind.TabButton, bounds, true, index, ZOrder: 22, Tooltip: tabs[index]));
            x += width + 5;
        }
    }

    private void DrawMeters(IReadOnlyList<Phase20ProgressMeter>? meters, Rectangle header, Color accent)
    {
        if (meters is null || meters.Count == 0) return;
        int meterWidth = Math.Clamp(header.Width / 5, 150, 280);
        int x = header.Right - meterWidth;
        int y = header.Y + 4;
        foreach (Phase20ProgressMeter meter in meters.Take(2))
        {
            float normalized = meter.Maximum <= 0 ? 0f : Math.Clamp(meter.Value / meter.Maximum, 0f, 1f);
            TextRight(meter.ValueLabel ?? $"{meter.Value:0}/{meter.Maximum:0}", header.Right, y, 1, Palette.Primary(highContrast));
            Text(meter.Label.ToUpperInvariant(), x, y, 1, Palette.Muted(highContrast));
            Rectangle track = new(x, y + 17, meterWidth, 7);
            Fill(track, Palette.PanelDark(highContrast));
            Border(track, Palette.Border(highContrast));
            Fill(new Rectangle(track.X + 1, track.Y + 1, Math.Max(0, (int)((track.Width - 2) * normalized)), Math.Max(1, track.Height - 2)), accent);
            y += 30;
        }
    }

    private void DrawTabs(IReadOnlyList<string> tabs, int selectedTab, Rectangle area, Color accent)
    {
        if (tabs.Count == 0 || area.Height <= 0) return;
        int x = area.X;
        int y = area.Y;
        int rowHeight = Math.Max(24, (int)(25 * scale));
        for (int index = 0; index < tabs.Count; index++)
        {
            string tab = tabs[index].TrimEnd(' ', '*').ToUpperInvariant();
            int tabWidth = Measure(tab, 1) + 24;
            if (x + tabWidth > area.Right && x > area.X) { x = area.X; y += rowHeight + 4; }
            if (y + rowHeight > area.Bottom) break;
            Rectangle bounds = new(x, y, Math.Min(tabWidth, area.Right - x), rowHeight);
            bool hovered = bounds.Contains(interaction.Pointer);
            bool selected = index == selectedTab;
            Fill(bounds, selected || hovered ? Palette.Selection(highContrast) : Palette.PanelDark(highContrast));
            Border(bounds, selected ? accent : Palette.Border(highContrast));
            Text(Trim(tab, Math.Max(4, (bounds.Width - 20) / CellWidth(1))), bounds.X + 10, bounds.Y + 8, 1, selected ? Palette.Primary(highContrast) : Palette.Muted(highContrast));
            hitMap.Add(new UiHitRegion($"tab:{index}", UiControlKind.TabButton, bounds, true, index, ZOrder: 20, Tooltip: tabs[index]));
            x += tabWidth + 6;
        }
    }

    private void DrawEntries(IReadOnlyList<Phase20MenuEntry> entries, int selectedIndex, Rectangle panel, bool compact, Color accent, int scrollOffset, string scrollId, bool textEntryActive = false, string entryIdPrefix = "entry")
    {
        int top = panel.Y + (compact ? 34 : 20);
        int bottomPadding = compact ? 14 : 20;
        int rowHeight = compact ? Math.Max(22, (int)(24 * scale)) : RowHeight();
        int visible = Math.Max(1, (panel.Bottom - bottomPadding - top) / rowHeight);
        LastVisibleEntryCount = visible;
        int selected = entries.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, entries.Count - 1);
        int maximumFirst = Math.Max(0, entries.Count - visible);
        int first = Math.Clamp(scrollOffset, 0, maximumFirst);
        Rectangle scrollPaneBounds = new(panel.X + 4, top - 4, panel.Width - 8, Math.Max(1, panel.Bottom - bottomPadding - top + 8));
        hitMap.Add(new UiHitRegion(scrollId, UiControlKind.ScrollPane, scrollPaneBounds, true, ZOrder: 1));
        for (int visibleIndex = 0; visibleIndex < Math.Min(visible, entries.Count - first); visibleIndex++)
        {
            int index = first + visibleIndex;
            Phase20MenuEntry entry = entries[index];
            Rectangle row = new(panel.X + 10, top + visibleIndex * rowHeight, panel.Width - 24, rowHeight - 4);
            bool hovered = row.Contains(interaction.Pointer);
            bool selectedRow = index == selected;
            if (selectedRow || hovered) Fill(row, Palette.Selection(highContrast));
            if (selectedRow) Border(row, accent);
            string prefix = selectedRow ? "> " : "  ";
            Color textColor = !entry.Enabled ? Palette.Disabled(highContrast) : entry.Dangerous ? Palette.Danger(highContrast) : selectedRow ? Palette.Primary(highContrast) : Palette.Muted(highContrast);
            int textScale = compact ? 1 : Math.Max(1, (int)Math.Round(scale));
            int controlWidth = entry.Presentation is Phase20EntryPresentation.Standard or Phase20EntryPresentation.Button ? Math.Min(row.Width / 2, 240) : Math.Clamp(row.Width / 3, 160, 310);
            int labelMax = Math.Max(8, (row.Width - controlWidth - 28) / CellWidth(textScale));
            Text(Trim(prefix + entry.Label, labelMax), row.X + 10, row.Y + 8, textScale, textColor);
            DrawEntryControl(entry, index, row, controlWidth, accent, selectedRow, textEntryActive && selectedRow);
            if (!string.IsNullOrWhiteSpace(entry.Badge)) TextRight($"[{entry.Badge.ToUpperInvariant()}]", row.Right - 10, row.Y + rowHeight - 13, 1, Palette.Warning(highContrast));
            hitMap.Add(new UiHitRegion($"{entryIdPrefix}:{index}", UiControlKind.ListItem, row, entry.Enabled, index, ZOrder: 10, Tooltip: entry.Description));
        }
        if (entries.Count == 0) Text("NO ENTRIES", panel.X + 20, panel.Y + 24, 1, Palette.Disabled(highContrast));
        if (entries.Count > visible) DrawScrollbar(new Rectangle(panel.Right - 10, top, 5, visible * rowHeight), first, maximumFirst, scrollId + ":bar");
    }

    private void DrawEntryControl(Phase20MenuEntry entry, int index, Rectangle row, int controlWidth, Color accent, bool selected, bool textEntryActive)
    {
        Rectangle control = new(row.Right - controlWidth - 10, row.Y + 4, controlWidth, Math.Max(18, row.Height - 8));
        Color valueColor = selected ? accent : Palette.Primary(highContrast);
        switch (entry.Presentation)
        {
            case Phase20EntryPresentation.Button:
                Fill(control, selected ? Palette.Selection(highContrast) : Palette.Panel(highContrast));
                Border(control, selected ? accent : Palette.Border(highContrast));
                TextCentered((entry.Value ?? "ACTIVATE").ToUpperInvariant(), control.Center.X, control.Y + 7, 1, valueColor);
                hitMap.Add(new UiHitRegion($"button:{index}", UiControlKind.Button, control, entry.Enabled, index, UiAction.Confirm, 25, entry.Description));
                break;
            case Phase20EntryPresentation.Toggle:
                Fill(control, Palette.Panel(highContrast));
                Border(control, Palette.Border(highContrast));
                TextCentered((entry.Value ?? "OFF").ToUpperInvariant(), control.Center.X, control.Y + 7, 1, valueColor);
                hitMap.Add(new UiHitRegion($"toggle:{index}", UiControlKind.Toggle, control, entry.Enabled, index, UiAction.Confirm, 25, entry.Description));
                break;
            case Phase20EntryPresentation.Slider:
                {
                    Rectangle track = new(control.X + 8, control.Center.Y - 3, Math.Max(20, control.Width - 72), 7);
                    Fill(track, Palette.Panel(highContrast));
                    Border(track, Palette.Border(highContrast));
                    int fill = (int)((track.Width - 2) * Math.Clamp(entry.NormalizedValue, 0f, 1f));
                    Fill(new Rectangle(track.X + 1, track.Y + 1, Math.Max(0, fill), Math.Max(1, track.Height - 2)), accent);
                    int thumbX = track.X + Math.Clamp(fill, 0, Math.Max(0, track.Width - 3));
                    Fill(new Rectangle(thumbX, track.Y - 4, 3, track.Height + 8), Palette.Primary(highContrast));
                    TextRight(entry.Value ?? string.Empty, control.Right, control.Y + 7, 1, valueColor);
                    hitMap.Add(new UiHitRegion($"slider:{index}", UiControlKind.Slider, track, entry.Enabled, index, ZOrder: 25, Tooltip: entry.Description));
                    break;
                }
            case Phase20EntryPresentation.Stepper:
                {
                    int button = Math.Min(34, control.Height);
                    Rectangle minus = new(control.X, control.Y, button, control.Height);
                    Rectangle plus = new(control.Right - button, control.Y, button, control.Height);
                    Fill(control, Palette.Panel(highContrast)); Border(control, Palette.Border(highContrast));
                    Border(minus, Palette.Border(highContrast)); Border(plus, Palette.Border(highContrast));
                    TextCentered("-", minus.Center.X, minus.Y + 7, 1, valueColor);
                    TextCentered(entry.Value ?? string.Empty, control.Center.X, control.Y + 7, 1, valueColor);
                    TextCentered("+", plus.Center.X, plus.Y + 7, 1, valueColor);
                    hitMap.Add(new UiHitRegion($"stepper-minus:{index}", UiControlKind.Stepper, minus, entry.Enabled, index, UiAction.NavigateLeft, 26, "Decrease"));
                    hitMap.Add(new UiHitRegion($"stepper-plus:{index}", UiControlKind.Stepper, plus, entry.Enabled, index, UiAction.NavigateRight, 26, "Increase"));
                    break;
                }
            case Phase20EntryPresentation.Dropdown:
                Fill(control, Palette.Panel(highContrast)); Border(control, Palette.Border(highContrast));
                Text("<", control.X + 8, control.Y + 7, 1, valueColor);
                TextCentered(entry.Value ?? string.Empty, control.Center.X, control.Y + 7, 1, valueColor);
                TextRight(">", control.Right - 8, control.Y + 7, 1, valueColor);
                hitMap.Add(new UiHitRegion($"dropdown:{index}", UiControlKind.Dropdown, control, entry.Enabled, index, UiAction.NavigateRight, 25, entry.Description));
                break;
            case Phase20EntryPresentation.TextBox:
                Fill(control, Palette.Panel(highContrast)); Border(control, textEntryActive ? accent : Palette.Border(highContrast));
                string text = entry.Value ?? string.Empty;
                if (textEntryActive && (Environment.TickCount64 / 450) % 2 == 0) text += "_";
                Text(Trim(text, Math.Max(4, (control.Width - 16) / CellWidth(1))), control.X + 8, control.Y + 7, 1, valueColor);
                hitMap.Add(new UiHitRegion($"textbox:{index}", UiControlKind.TextBox, control, entry.Enabled, index, UiAction.Confirm, 25, "Click to edit"));
                break;
            case Phase20EntryPresentation.Progress:
                {
                    // Reserve a compact numeric readout so advancement screens can show both
                    // the at-a-glance meter and the exact RPG value without making the player
                    // open the detail pane.
                    int valueReserve = string.IsNullOrWhiteSpace(entry.Value) ? 0 : Math.Clamp((int)(50 * scale), 38, 72);
                    int trackWidth = Math.Max(20, control.Width - 12 - valueReserve);
                    Rectangle track = new(control.X + 6, control.Center.Y - 4, trackWidth, 9);
                    Fill(track, Palette.Panel(highContrast)); Border(track, Palette.Border(highContrast));
                    int fillWidth = (int)((track.Width - 2) * Math.Clamp(entry.NormalizedValue, 0f, 1f));
                    if (fillWidth > 0) Fill(new Rectangle(track.X + 1, track.Y + 1, fillWidth, Math.Max(1, track.Height - 2)), accent);
                    if (!string.IsNullOrWhiteSpace(entry.Value))
                        TextRight(Trim(entry.Value, 8), control.Right, control.Y + 7, 1, valueColor);
                    break;
                }
            default:
                if (!string.IsNullOrWhiteSpace(entry.Value)) TextRight(Trim(entry.Value, Math.Max(8, control.Width / CellWidth(1))), row.Right - 10, row.Y + 8, 1, valueColor);
                break;
        }
    }

    private void DrawCardGrid(IReadOnlyList<Phase20MenuEntry> entries, int selectedIndex, Rectangle panel, Color accent, int scrollOffset, int columns)
    {
        columns = Math.Max(1, columns);
        int gap = Math.Max(6, (int)(8 * scale));
        int top = panel.Y + 12;
        int cardHeight = Math.Clamp((int)(94 * scale), 76, 150);
        int cardWidth = Math.Max(110, (panel.Width - 20 - gap * (columns - 1)) / columns);
        int rows = Math.Max(1, (panel.Height - 24 + gap) / (cardHeight + gap));
        int visible = rows * columns;
        LastVisibleEntryCount = visible;
        int maximumFirst = Math.Max(0, entries.Count - visible);
        int first = Math.Clamp(scrollOffset, 0, maximumFirst);
        if (columns > 1) first = Math.Min(maximumFirst, first / columns * columns);
        hitMap.Add(new UiHitRegion("entries", UiControlKind.ScrollPane, new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, panel.Height - 8), true, ZOrder: 1));
        for (int visibleIndex = 0; visibleIndex < Math.Min(visible, entries.Count - first); visibleIndex++)
        {
            int index = first + visibleIndex;
            int rowIndex = visibleIndex / columns;
            int column = visibleIndex % columns;
            Rectangle card = new(panel.X + 10 + column * (cardWidth + gap), top + rowIndex * (cardHeight + gap), cardWidth, cardHeight);
            Phase20MenuEntry entry = entries[index];
            bool selected = index == Math.Clamp(selectedIndex, 0, Math.Max(0, entries.Count - 1));
            bool hovered = card.Contains(interaction.Pointer);
            Fill(card, selected || hovered ? Palette.Selection(highContrast) : Palette.Panel(highContrast));
            Border(card, selected ? accent : Palette.Border(highContrast), selected ? 2 : 1);
            Color textColor = entry.Enabled ? (selected ? Palette.Primary(highContrast) : Palette.Muted(highContrast)) : Palette.Disabled(highContrast);
            Text(Trim(entry.Label.ToUpperInvariant(), Math.Max(8, (card.Width - 20) / CellWidth(1))), card.X + 10, card.Y + 10, 1, textColor);
            if (!string.IsNullOrWhiteSpace(entry.Value)) Text(Trim(entry.Value, Math.Max(8, (card.Width - 20) / CellWidth(1))), card.X + 10, card.Y + 31, 1, accent);
            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                IReadOnlyList<string> pieces = Wrap(entry.Description, Math.Max(12, (card.Width - 20) / CellWidth(1)));
                int y = card.Y + 52;
                foreach (string piece in pieces.Take(2)) { Text(piece, card.X + 10, y, 1, Palette.Muted(highContrast)); y += 18; }
            }
            if (!string.IsNullOrWhiteSpace(entry.Badge)) TextRight($"[{entry.Badge.ToUpperInvariant()}]", card.Right - 8, card.Bottom - 16, 1, Palette.Warning(highContrast));
            hitMap.Add(new UiHitRegion($"entry:{index}", UiControlKind.Card, card, entry.Enabled, index, ZOrder: 12, Tooltip: entry.Description));
        }
        if (entries.Count == 0) Text("NO ENTRIES", panel.X + 20, panel.Y + 24, 1, Palette.Disabled(highContrast));
        if (entries.Count > visible) DrawScrollbar(new Rectangle(panel.Right - 8, panel.Y + 8, 4, panel.Height - 16), first, maximumFirst, "entries:bar");
    }

    private void DrawLines(IReadOnlyList<string> lines, Rectangle panel, int selectedLine, Color color, int scrollOffset, string scrollId)
    {
        int maxChars = Math.Max(10, (panel.Width - 36) / CellWidth(1));
        var rendered = new List<(string Text, int SourceIndex)>();
        for (int index = 0; index < lines.Count; index++) foreach (string piece in Wrap(lines[index], maxChars)) rendered.Add((piece, index));
        int top = panel.Y + 40;
        int maxLines = Math.Max(1, (panel.Height - 54) / 22);
        int maximumFirst = Math.Max(0, rendered.Count - maxLines);
        int first = Math.Clamp(scrollOffset, 0, maximumFirst);
        Rectangle scrollBounds = new(panel.X + 6, top - 4, panel.Width - 12, Math.Max(1, panel.Height - 48));
        hitMap.Add(new UiHitRegion(scrollId, UiControlKind.ScrollPane, scrollBounds, true, ZOrder: 1));
        int y = top;
        for (int index = first; index < Math.Min(rendered.Count, first + maxLines); index++)
        {
            (string text, int source) = rendered[index];
            bool selected = source == selectedLine;
            if (selected) Fill(new Rectangle(panel.X + 8, y - 4, panel.Width - 20, 16), Palette.Selection(highContrast));
            Text((selected ? "> " : "  ") + text, panel.X + 12, y, 1, selected ? Palette.Primary(highContrast) : color);
            y += 22;
        }
        if (rendered.Count > maxLines) DrawScrollbar(new Rectangle(panel.Right - 9, top, 4, maxLines * 22), first, maximumFirst, scrollId + ":bar");
    }

    private void DrawScrollbar(Rectangle track, int offset, int maximumOffset, string id)
    {
        Fill(track, Palette.Panel(highContrast));
        if (maximumOffset <= 0) return;
        int thumbHeight = Math.Clamp(track.Height / 4, 16, Math.Max(16, track.Height));
        int travel = Math.Max(0, track.Height - thumbHeight);
        int y = track.Y + travel * offset / maximumOffset;
        Rectangle thumb = new(track.X, y, track.Width, thumbHeight);
        Fill(thumb, Palette.Border(highContrast));
        hitMap.Add(new UiHitRegion(id, UiControlKind.Scrollbar, track, true, ZOrder: 5));
    }

    private void DrawActionBar(IReadOnlyList<Phase20ActionPrompt>? prompts, Rectangle area)
    {
        if (prompts is null || prompts.Count == 0 || area.Width <= 0) return;
        if (area.Height > 2) Fill(new Rectangle(area.X, area.Y, area.Width, 1), Palette.Border(highContrast));
        string[] labels = prompts.Select(prompt => prompt.ShowBinding ? UiBindingFormatter.Prompt(currentSettings, prompt, interaction.ActiveDevice) : prompt.Label.ToUpperInvariant()).ToArray();
        int x = area.Right;
        for (int index = labels.Length - 1; index >= 0; index--)
        {
            Phase20ActionPrompt prompt = prompts[index];
            string label = labels[index];
            int width = Measure(label, 1) + 20;
            x -= width;
            Rectangle bounds = new(Math.Max(area.X, x), area.Y, Math.Min(width, area.Right - Math.Max(area.X, x)), Math.Max(24, area.Height));
            bool hovered = bounds.Contains(interaction.Pointer);
            if (hovered) Fill(bounds, Palette.Selection(highContrast));
            Color color = !prompt.Enabled ? Palette.Disabled(highContrast) : prompt.Dangerous ? Palette.Danger(highContrast) : hovered ? Palette.Primary(highContrast) : Palette.Muted(highContrast);
            Text(Trim(label, Math.Max(5, (bounds.Width - 12) / CellWidth(1))), bounds.X + 8, bounds.Y + 7, 1, color);
            hitMap.Add(new UiHitRegion($"action:{prompt.Action}:{index}", UiControlKind.ActionButton, bounds, prompt.Enabled, index, prompt.Action, 30, prompt.Tooltip ?? prompt.Label));
            x -= 8;
            if (x <= area.X) break;
        }
    }

    private void DrawScreenTransition(int width, int height)
    {
        if (currentSettings.Accessibility.ReduceMotion) return;
        double age = Math.Max(0d, currentInterfaceTimeSeconds - screenTransitionStartSeconds);
        const double duration = 0.14d;
        if (age >= duration) return;
        float t = Math.Clamp((float)(age / duration), 0f, 1f);
        byte alpha = (byte)Math.Clamp((int)MathF.Round((1f - t) * 82f), 0, 82);
        Fill(new Rectangle(0, 0, width, height), new Color(0, 8, 7, (int)alpha));
        int scanY = Math.Clamp((int)MathF.Round(t * height), 0, Math.Max(0, height - 2));
        Color transitionAccent = Palette.Accent(highContrast);
        Fill(new Rectangle(0, scanY, width, 2), new Color(transitionAccent.R, transitionAccent.G, transitionAccent.B, (byte)Math.Clamp((int)((1f - t) * 120), 0, 120)));
    }

    private void DrawHoveredTooltip(int width, int height)
    {
        UiHitRegion? hovered = hitMap.HitTest(interaction.Pointer);
        if (hovered is null || string.IsNullOrWhiteSpace(hovered.Tooltip)) return;
        string tooltip = hovered.Tooltip!;
        int maxChars = Math.Min(56, Math.Max(18, tooltip.Length));
        IReadOnlyList<string> lines = Wrap(tooltip, maxChars);
        int tooltipWidth = Math.Min(width - 24, Math.Max(180, lines.Max(static line => line.Length) * CellWidth(1) + 24));
        int tooltipHeight = lines.Count * 22 + 18;
        int x = Math.Clamp(interaction.Pointer.X + 14, 12, Math.Max(12, width - tooltipWidth - 12));
        int y = Math.Clamp(interaction.Pointer.Y + 18, 12, Math.Max(12, height - tooltipHeight - 12));
        Rectangle bounds = new(x, y, tooltipWidth, tooltipHeight);
        Fill(bounds, Palette.Panel(highContrast));
        Border(bounds, Palette.Border(highContrast));
        int lineY = y + 10;
        foreach (string line in lines)
        {
            Text(line, x + 12, lineY, 1, Palette.Primary(highContrast));
            lineY += 22;
        }
    }

    private void DrawKeyboardFocusIndicator(string? focusedControlId)
    {
        if (interaction.ActiveDevice == UiInputDevice.Mouse || string.IsNullOrWhiteSpace(focusedControlId)) return;
        UiHitRegion? focused = hitMap.Regions.LastOrDefault(region => string.Equals(region.Id, focusedControlId, StringComparison.Ordinal));
        if (focused is null || !focused.Enabled) return;
        Rectangle bounds = focused.Bounds;
        if (bounds.Width < 2 || bounds.Height < 2) return;
        Color accent = Palette.Accent(highContrast);
        int inset = 2;
        Rectangle ring = new(bounds.X - inset, bounds.Y - inset, bounds.Width + inset * 2, bounds.Height + inset * 2);
        Border(ring, accent, 2);
        int bracket = Math.Clamp(Math.Min(ring.Width, ring.Height) / 4, 5, 14);
        Fill(new Rectangle(ring.X, ring.Y, bracket, 2), Palette.Primary(highContrast));
        Fill(new Rectangle(ring.X, ring.Y, 2, bracket), Palette.Primary(highContrast));
        Fill(new Rectangle(ring.Right - bracket, ring.Y, bracket, 2), Palette.Primary(highContrast));
        Fill(new Rectangle(ring.Right - 2, ring.Y, 2, bracket), Palette.Primary(highContrast));
    }

    private Color StatusColor(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Palette.Muted(highContrast);
        string text = status.ToLowerInvariant();
        if (text.Contains("error", StringComparison.Ordinal) || text.Contains("failed", StringComparison.Ordinal) || text.Contains("cannot", StringComparison.Ordinal) || text.Contains("unavailable", StringComparison.Ordinal))
            return Palette.Danger(highContrast);
        if (text.Contains("warning", StringComparison.Ordinal) || text.Contains("locked", StringComparison.Ordinal) || text.Contains("missing", StringComparison.Ordinal) || text.Contains("remain", StringComparison.Ordinal))
            return Palette.Warning(highContrast);
        if (text.Contains("saved", StringComparison.Ordinal) || text.Contains("equipped", StringComparison.Ordinal) || text.Contains("crafted", StringComparison.Ordinal) || text.Contains("completed", StringComparison.Ordinal) || text.Contains("applied", StringComparison.Ordinal))
            return Palette.Primary(highContrast);
        return Palette.Muted(highContrast);
    }

    private void DrawInterfaceDiagnostics(int width, int height, Phase20ScreenModel model)
    {
        foreach (UiHitRegion region in hitMap.Regions)
        {
            Color color = region.Bounds.Contains(interaction.Pointer) ? Palette.Warning(highContrast) : new Color(90, 150, 135, 155);
            Border(region.Bounds, color);
        }
        string line = $"UI {model.Kind} | DEVICE {interaction.ActiveDevice} | FOCUS {model.FocusedControlId ?? "-"} | SCROLL {model.ScrollOffset}/{model.DetailScrollOffset}";
        Rectangle strip = new(8, height - 18, Math.Min(width - 16, Measure(line, 1) + 16), 16);
        Fill(strip, new Color(0, 0, 0, 220));
        Text(Trim(line, Math.Max(10, (strip.Width - 10) / CellWidth(1))), strip.X + 5, strip.Y + 4, 1, Palette.Warning(highContrast));
    }
    private void Panel(Rectangle rectangle, string title, Color accent)
    {
        Fill(rectangle, Palette.PanelDark(highContrast));
        Border(rectangle, Palette.Border(highContrast));
        Fill(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 28), new Color(accent.R / 5, accent.G / 5, accent.B / 5, 245));
        Text(Trim(title.ToUpperInvariant(), Math.Max(8, (rectangle.Width - 24) / CellWidth(1))), rectangle.X + 12, rectangle.Y + 10, 1, accent);
    }

    private void DrawCornerBrackets(Rectangle rectangle, Color color)
    {
        int size = Math.Clamp(rectangle.Width / 28, 16, 38);
        int thickness = 2;
        Fill(new Rectangle(rectangle.X, rectangle.Y, size, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Y, thickness, size), color);
        Fill(new Rectangle(rectangle.Right - size, rectangle.Y, size, thickness), color);
        Fill(new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, size), color);
        Fill(new Rectangle(rectangle.X, rectangle.Bottom - thickness, size, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Bottom - size, thickness, size), color);
        Fill(new Rectangle(rectangle.Right - size, rectangle.Bottom - thickness, size, thickness), color);
        Fill(new Rectangle(rectangle.Right - thickness, rectangle.Bottom - size, thickness, size), color);
    }

    private void DrawScanlines(int width, int height, double time)
    {
        byte alpha = highContrast ? (byte)5 : (byte)12;
        int offset = (int)(time * 18) % 4;
        for (int y = offset; y < height; y += 4) Fill(new Rectangle(0, y, width, 1), new Color((byte)0, (byte)0, (byte)0, alpha));
    }

    private int RowHeight() => Math.Clamp((int)(38 * scale), 28, 68);
    private static string Trim(string text, int maximumCharacters) => text.Length <= maximumCharacters ? text : text[..Math.Max(1, maximumCharacters - 3)] + "...";

    private static IReadOnlyList<string> Wrap(string text, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(text)) return [string.Empty];
        var lines = new List<string>();
        string remaining = text;
        while (remaining.Length > maximumCharacters)
        {
            int split = remaining.LastIndexOf(' ', maximumCharacters);
            if (split < maximumCharacters / 3) split = maximumCharacters;
            lines.Add(remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart();
        }
        lines.Add(remaining);
        return lines;
    }

    private void TextWrapped(string text, int x, int y, int maximumCharacters, int textScale, Color color)
    {
        foreach (string line in Wrap(text, maximumCharacters))
        {
            Text(line, x, y, textScale, color);
            y += 22 * textScale;
        }
    }

    private void TextCentered(string text, int centerX, int y, int textScale, Color color) => Text(text, centerX - Measure(text, textScale) / 2, y, textScale, color);
    private void TextRight(string text, int rightX, int y, int textScale, Color color) => Text(text, rightX - Measure(text, textScale), y, textScale, color);
    private static int Measure(string text, int textScale) => text.Length * CellWidth(textScale);
    private static int CellWidth(int textScale) => 9 * textScale;

    private void Text(string text, int x, int y, int textScale, Color color)
    {
        if(bodyFont is not null)
        {
            int dx=x;
            foreach(char original in text)
            {
                char c=original is '—' or '–' ? '-' : original=='•'?'|':original;
                if(c=='\n'){dx=x;y+=18*textScale;continue;}
                int code=c>=32&&c<=127?c-32:'?'-32;
                batch.Draw(bodyFont,new Rectangle(dx,y,12*textScale,20*textScale),new Rectangle(code%16*24,code/16*40,24,40),ApplyColorVision(color));
                dx+=CellWidth(textScale);
            }
            return;
        }
        int cursor = x;
        foreach (char raw in text)
        {
            char character = char.ToUpperInvariant(raw);
            if (character == '\n')
            {
                y += 8 * textScale;
                cursor = x;
                continue;
            }
            string[] glyph = Glyph(character);
            for (int row = 0; row < 7; row++)
                for (int column = 0; column < 5; column++)
                    if (glyph[row][column] == '#') Fill(new Rectangle(cursor + column * textScale, y + row * textScale, textScale, textScale), color);
            cursor += CellWidth(textScale);
        }
    }

    private void Fill(Rectangle rectangle, Color color)
    {
        if (rectangle.Width > 0 && rectangle.Height > 0) batch.Draw(pixel, rectangle, ApplyColorVision(color));
    }

    private Color ApplyColorVision(Color color)
    {
        if (highContrast || colorVisionMode == ColorVisionMode.Standard) return color;
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        (float nr, float ng, float nb) = colorVisionMode switch
        {
            ColorVisionMode.Deuteranopia => (0.625f * r + 0.375f * g, 0.700f * r + 0.300f * g, 0.300f * g + 0.700f * b),
            ColorVisionMode.Protanopia => (0.567f * r + 0.433f * g, 0.558f * r + 0.442f * g, 0.242f * g + 0.758f * b),
            ColorVisionMode.Tritanopia => (0.950f * r + 0.050f * g, 0.433f * g + 0.567f * b, 0.475f * g + 0.525f * b),
            ColorVisionMode.Monochrome => ((r + g + b) / 3f, (r + g + b) / 3f, (r + g + b) / 3f),
            _ => (r, g, b)
        };
        return new Color(Math.Clamp(nr, 0f, 1f), Math.Clamp(ng, 0f, 1f), Math.Clamp(nb, 0f, 1f), color.A / 255f);
    }

    private void Border(Rectangle rectangle, Color color, int thickness = 1)
    {
        Fill(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        Fill(new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    private static string[] Glyph(char character) => Font.TryGetValue(character, out string[]? glyph) ? glyph : Font['?'];

    private static readonly Dictionary<char, string[]> Font = BuildFont();

    private static Dictionary<char, string[]> BuildFont()
    {
        var result = new Dictionary<char, string[]>();
        void Add(char character, params string[] rows) => result[character] = rows;
        Add(' ', ".....", ".....", ".....", ".....", ".....", ".....", ".....");
        Add('?', ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..");
        Add('-', ".....", ".....", ".....", "#####", ".....", ".....", ".....");
        Add('_', ".....", ".....", ".....", ".....", ".....", ".....", "#####");
        Add('.', ".....", ".....", ".....", ".....", ".....", ".##..", ".##..");
        Add(',', ".....", ".....", ".....", ".....", ".##..", ".##..", ".#...");
        Add(':', ".....", ".##..", ".##..", ".....", ".##..", ".##..", ".....");
        Add(';', ".....", ".##..", ".##..", ".....", ".##..", ".##..", ".#...");
        Add('[', ".###.", ".#...", ".#...", ".#...", ".#...", ".#...", ".###.");
        Add(']', ".###.", "...#.", "...#.", "...#.", "...#.", "...#.", ".###.");
        Add('(', "..##.", ".#...", "#....", "#....", "#....", ".#...", "..##.");
        Add(')', ".##..", "...#.", "....#", "....#", "....#", "...#.", ".##..");
        Add('/', "....#", "...#.", "...#.", "..#..", ".#...", ".#...", "#....");
        Add('\\', "#....", ".#...", ".#...", "..#..", "...#.", "...#.", "....#");
        Add('+', ".....", "..#..", "..#..", "#####", "..#..", "..#..", ".....");
        Add('=', ".....", "#####", ".....", "#####", ".....", ".....", ".....");
        Add('%', "##..#", "##.#.", "...#.", "..#..", ".#...", ".#.##", "#..##");
        Add('!', "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#..");
        Add('\'', "..#..", "..#..", ".#...", ".....", ".....", ".....", ".....");
        Add('"', ".#.#.", ".#.#.", "#.#.#", ".....", ".....", ".....", ".....");
        Add('>', "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#....");
        Add('<', "....#", "...#.", "..#..", ".#...", "..#..", "...#.", "....#");
        Add('|', "..#..", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
        Add('*', ".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", ".....");

        string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string[][] rows =
        [
            [".###.","#...#","#...#","#####","#...#","#...#","#...#"], ["####.","#...#","####.","#...#","#...#","#...#","####."],
            [".####","#....","#....","#....","#....","#....",".####"], ["####.","#...#","#...#","#...#","#...#","#...#","####."],
            ["#####","#....","#....","####.","#....","#....","#####"], ["#####","#....","#....","####.","#....","#....","#...."],
            [".####","#....","#....","#.###","#...#","#...#",".###."], ["#...#","#...#","#...#","#####","#...#","#...#","#...#"],
            ["#####","..#..","..#..","..#..","..#..","..#..","#####"], ["..###","...#.","...#.","...#.","...#.","#..#.",".##.."],
            ["#...#","#..#.","#.#..","##...","#.#..","#..#.","#...#"], ["#....","#....","#....","#....","#....","#....","#####"],
            ["#...#","##.##","#.#.#","#.#.#","#...#","#...#","#...#"], ["#...#","##..#","##..#","#.#.#","#..##","#..##","#...#"],
            [".###.","#...#","#...#","#...#","#...#","#...#",".###."], ["####.","#...#","#...#","####.","#....","#....","#...."],
            [".###.","#...#","#...#","#...#","#.#.#","#..#.",".##.#"], ["####.","#...#","#...#","####.","#.#..","#..#.","#...#"],
            [".####","#....","#....",".###.","....#","....#","####."], ["#####","..#..","..#..","..#..","..#..","..#..","..#.."],
            ["#...#","#...#","#...#","#...#","#...#","#...#",".###."], ["#...#","#...#","#...#","#...#","#...#",".#.#.","..#.."],
            ["#...#","#...#","#...#","#.#.#","#.#.#","##.##","#...#"], ["#...#","#...#",".#.#.","..#..",".#.#.","#...#","#...#"],
            ["#...#","#...#",".#.#.","..#..","..#..","..#..","..#.."], ["#####","....#","...#.","..#..",".#...","#....","#####"]
        ];
        for (int index = 0; index < letters.Length; index++) result[letters[index]] = rows[index];

        string digits = "0123456789";
        string[][] digitRows =
        [
            [".###.","#...#","#..##","#.#.#","##..#","#...#",".###."], ["..#..",".##..","..#..","..#..","..#..","..#..",".###."],
            [".###.","#...#","....#","...#.","..#..",".#...","#####"], ["####.","....#","....#",".###.","....#","....#","####."],
            ["...#.","..##.",".#.#.","#..#.","#####","...#.","...#."], ["#####","#....","#....","####.","....#","....#","####."],
            [".###.","#....","#....","####.","#...#","#...#",".###."], ["#####","....#","...#.","..#..",".#...",".#...",".#..."],
            [".###.","#...#","#...#",".###.","#...#","#...#",".###."], [".###.","#...#","#...#",".####","....#","....#",".###."]
        ];
        for (int index = 0; index < digits.Length; index++) result[digits[index]] = digitRows[index];
        return result;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        batch.Dispose();
        pixel.Dispose();
        bodyFont?.Dispose();
        mapTexture?.Dispose();
    }

    private static class Palette
    {
        public static Color Accent(bool highContrast) => highContrast ? Color.Cyan : new Color(125, 214, 173);
        public static Color FocusAccent(bool highContrast) => highContrast ? Color.White : new Color(130, 245, 195);
        public static Color Primary(bool highContrast) => highContrast ? Color.White : new Color(216, 239, 226);
        public static Color Muted(bool highContrast) => highContrast ? new Color(205, 205, 205) : new Color(155, 192, 177);
        public static Color Disabled(bool highContrast) => highContrast ? new Color(140, 140, 140) : new Color(92, 119, 110);
        public static Color Warning(bool highContrast) => highContrast ? Color.Yellow : new Color(232, 196, 116);
        public static Color Danger(bool highContrast) => highContrast ? Color.Red : new Color(235, 112, 96);
        public static Color Panel(bool highContrast) => highContrast ? new Color(0, 0, 0, 248) : new Color(8, 22, 20, 246);
        public static Color PanelDark(bool highContrast) => highContrast ? new Color(0, 0, 0, 250) : new Color(5, 15, 14, 235);
        public static Color Selection(bool highContrast) => highContrast ? new Color(40, 40, 40, 255) : new Color(39, 78, 65, 230);
        public static Color Border(bool highContrast) => highContrast ? Color.White : new Color(68, 128, 106);
    }
}
