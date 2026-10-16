using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roentgen.AI;
using Roentgen.Audio;
using Roentgen.Audio.MonoGame;
using Roentgen.Audio.Playroma;
using Roentgen.Campaign;
using Roentgen.Combat;
using Roentgen.Content;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Diagnostics;
using Roentgen.Entities;
using Roentgen.Factions;
using Roentgen.Music;
using Roentgen.Music.Playroma;
using Roentgen.Items;
using Roentgen.Narrative;
using Roentgen.Persistence;
using Roentgen.Physics;
using Roentgen.Rendering;
using Roentgen.Rpg;
using Roentgen.Simulation;
using Roentgen.UI;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.App;

// Phase 10 Real-time Combat remains authoritative beneath the Phase 20 interface.
// Phase 14 Navigation and Full AI remains the movement and actor-simulation foundation.
// Phase 16 — Quest and Dialogue Engine remains authoritative for facts, quests, dialogue, and recovery.
// Phase 17 — Campaign Director remains authoritative for milestones, convergence, endings, and epilogues.
// Legacy Phase13OverlayKind.Dialogue and Phase13OverlayKind.Journal presentation paths are retired;
// Phase 20 screens now present the same authoritative runtime state.

/// <summary>
/// Phase 21 product host. MonoGame lifecycle code stays here while deterministic simulation,
/// generation, combat, narrative, audio, music, persistence, and player-interface state remain
/// in their owning libraries. The bundled GeneralUser GS SoundFont remains the preferred
/// Playroma Music renderer by product decision.
/// </summary>
public sealed class RoentgenGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly IRoentgenLogger logger;
    private readonly ModuleCatalog modules;
    private readonly RenderingOptions renderingOptions = new();
    private readonly Phase8InputAdapter gameplayInput = new();
    private readonly Phase10InputAdapter combatInput = new();
    private readonly Phase20InputRouter interfaceInput = new();
    private readonly Phase20CharacterCreation characterCreation = new();
    private readonly Phase21ContentRuntime launchContent = Phase21ContentRuntime.CreateDefault();

    private FirstPersonCameraRig camera = new();
    private RendererFoundation? renderer;
    private Phase9WorldScene? scene;
    private PlayerSimulationRuntime? player;
    private Phase10CombatController? combat;
    private Phase12ProgressionRuntime? progression;
    private Phase13ItemRuntime? items;
    private Phase14WorldAgentRuntime? agents;
    private Phase15SocietyRuntime? society;
    private Phase16NarrativeRuntime? narrative;
    private Phase17CampaignRuntime? campaign;
    private Phase20FocusRuntime? focus;

    private IAudioRuntime? audioRuntime;
    private PlayromaMonoGameHost? audioHost;
    private Phase18AudioBridge? audioBridge;
    private AudioSettings audioSettings = new();
    private IMusicRuntime? musicRuntime;
    private Phase19MusicBridge? musicBridge;
    private MusicSettings musicSettings = new();

    private Phase8HudRenderer? hud;
    private readonly Phase17HudFeedbackRuntime hudFeedback = new();
    private FirstPersonWeaponRenderer? viewWeapon;
    private bool aiming;
    private Phase20UiRenderer? phase20Ui;
    private PlayerPresentationSnapshot? snapshot;
    private Phase20SettingsStore? settingsStore;
    private SaveSlotManager? saveSlots;
    private ILocalizationService localization = new Phase20LocalizationService();
    private Phase20UserSettings userSettings = new();

    private CampaignSeed campaignSeed = new(0x524F454E5447454EUL);
    private CampaignSeed pendingCampaignSeed = new(0x524F454E5447454EUL);
    private Phase20ScreenKind screen = Phase20ScreenKind.MainMenu;
    private readonly Stack<Phase20ScreenKind> screenHistory = new();
    private readonly UiScreenStateStore uiState = new();
    private int screenSelection;
    private int interfaceFrame;
    private bool showDebug;
    private bool showUiDebug;
    private bool mouseCaptured;
    private UiInputDevice uiInputDevice = UiInputDevice.Keyboard;
    private Phase20InputFrame? latestUiInput;
    private UiBindingCapture? bindingCapture;
    private UiBindingConflict? bindingConflict;
    private string? lastUiHoverId;
    private bool sessionReady;
    private WorldMapModel? worldMap;
    private string screenStatus = string.Empty;
    private bool exitAfterConfirmation;
    private bool returnToMenuAfterConfirmation;
    private string? deleteSaveAfterConfirmation;
    private StableId trackedMapMarkerId = StableId.Empty;
    private StableId trackedQuestId = StableId.Empty;
    private bool endingPresented;
    private MerchantPane merchantPane = MerchantPane.MerchantStock;
    private int merchantStockSelection;
    private int merchantPlayerSelection;
    private int merchantStockScroll;
    private int merchantPlayerScroll;
    private MapFilter mapFilter = MapFilter.All;
    private float mapZoom = 1f;
    private float mapPanX = 0.5f;
    private float mapPanZ = 0.5f;
    private bool mapDragging;
    private Point mapDragLast;
    private Rectangle mapDragBounds;
    private JournalFilter journalFilter = JournalFilter.Active;
    private FactionScreenSection factionSection = FactionScreenSection.Overview;
    private CompanionScreenSection companionSection = CompanionScreenSection.Overview;
    private readonly List<string> dialogueHistory = [];
    private StableId dialogueHistorySessionId = StableId.Empty;
    private Phase20SettingsSection settingsSection = Phase20SettingsSection.Gameplay;
    private InventoryCategory inventoryCategory = InventoryCategory.All;
    private InventorySortMode inventorySort = InventorySortMode.Name;
    private EquipmentViewMode equipmentView = EquipmentViewMode.Slots;
    private EquipmentSlot equipmentSelectedSlot = EquipmentSlot.Sidearm;
    private SkillCategory skillCategory = SkillCategory.All;
    private PerkFilter perkFilter = PerkFilter.All;
    private CraftingCategory craftingCategory = CraftingCategory.All;
    private bool craftingHideUnavailable;
    private readonly HashSet<string> craftingFavorites = new(StringComparer.Ordinal);
    private bool characterNameEditing;
    private string characterNameBeforeEdit = string.Empty;
    private StableId inspectingItemId = StableId.Empty;
    private StableId dropItemAfterConfirmation = StableId.Empty;
    private static readonly (int Width, int Height)[] ResolutionPresets = [(1024, 768), (1280, 720), (1600, 900), (1920, 1080), (2560, 1440), (3440, 1440), (3840, 2160)];
    private double playedSeconds;
    private double autosaveSeconds;
    private double titleAccumulator;

    public RoentgenGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
            PreferMultiSampling = false,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        Content.RootDirectory = "Content";
        Window.Title = "Roentgen — Phase 20 Full Interface and Accessibility";
        Window.AllowUserResizing = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / BuildIdentity.SimulationTicksPerSecond);
        IsMouseVisible = true;
        logger = new ConsoleRoentgenLogger();
        modules = AppBootstrap.CreateModuleCatalog();
    }

    protected override void Initialize()
    {
        graphics.ApplyChanges();
        logger.Write(LogLevel.Information, nameof(RoentgenGame), $"Starting {BuildIdentity.ProductName} {BuildIdentity.DevelopmentPhase} with {modules.Modules.Count} modules.");
        base.Initialize();
    }

    protected override void LoadContent()
    {
        string userRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roentgen");
        settingsStore = new Phase20SettingsStore(Path.Combine(userRoot, "phase20.settings.json"));
        userSettings = settingsStore.Load();
        ApplyVideoSettings();
        saveSlots = new SaveSlotManager(Path.Combine(userRoot, "Saves"));
        localization = new Phase20LocalizationService();
        ApplyCameraSettings();

        renderer = new RendererFoundation(GraphicsDevice, Content, renderingOptions, logger);
        hud = new Phase8HudRenderer(GraphicsDevice);
        viewWeapon = new FirstPersonWeaponRenderer(GraphicsDevice,renderer.ThreadGuard);
        phase20Ui = new Phase20UiRenderer(GraphicsDevice);

        // Keep a ready regression session behind the main menu so the menu can be exercised even
        // on a clean installation. A selected campaign or loaded save replaces it atomically.
        CreateCampaignSession(campaignSeed, characterCreation);
        OpenScreen(Phase20ScreenKind.MainMenu);
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        Phase20InputFrame ui = interfaceInput.Poll(userSettings);
        latestUiInput = ui;
        uiInputDevice = ui.ActiveDevice;
        interfaceFrame++;

        if (!sessionReady || player is null || combat is null || scene is null || agents is null || items is null || society is null || narrative is null || campaign is null)
        {
            base.Update(gameTime);
            return;
        }

        ulong tick = snapshot?.Tick ?? player.Tick;
        if (focus?.Active == true)
        {
            HandleFocusInput(ui, tick);
            bool advance = focus.ShouldAdvanceSimulation(interfaceFrame);
            if (focus.IsExecuting && advance) focus.UpdateExecution();
            if (advance) AdvanceSimulation(default, default, interfaceOpen: true);
            else UpdateAudio(interfaceOpen: true, focusActive: true);
            if (focus.Mode is Roentgen.Focus.FocusTemporalMode.Completed or Roentgen.Focus.FocusTemporalMode.Interrupted)
            {
                focus.FinishCompleted();
                CloseScreenToGameplay();
            }
            IsMouseVisible = true;
            base.Update(gameTime);
            return;
        }

        if (screen != Phase20ScreenKind.None)
        {
            HandleScreenInput(ui, tick);
            if (screen == Phase20ScreenKind.Dialogue && !userSettings.Accessibility.PauseDuringDialogue)
                AdvanceSimulation(default, default, interfaceOpen: true);
            else UpdateAudio(interfaceOpen: true, focusActive: false);
            IsMouseVisible = true;
            base.Update(gameTime);
            return;
        }

        HandleGameplayShortcuts(ui, tick);
        if (screen != Phase20ScreenKind.None || focus?.Active == true)
        {
            UpdateAudio(interfaceOpen: true, focusActive: focus?.Active == true);
            IsMouseVisible = true;
            base.Update(gameTime);
            return;
        }

        PlayerIntentFrame movement = gameplayInput.Poll(Window, mouseCaptured && player.Player.ControlMode == PlayerControlMode.Exploration, userSettings);
        CombatInputFrame attack = combatInput.Poll(userSettings);
        aiming=attack.Buttons.HasFlag(CombatInputButtons.BlockHeld);
        AdvanceSimulation(movement, attack, interfaceOpen: false);
        focus?.Recover(combat.LivingHostiles == 0);
        playedSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        autosaveSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        if (userSettings.Gameplay.AutosavesEnabled && autosaveSeconds >= userSettings.Gameplay.AutosaveIntervalMinutes * 60.0)
        {
            autosaveSeconds = 0;
            TryWriteSave($"auto-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", SaveSlotKind.Auto, "Automatic Save");
            saveSlots?.RotateAutosaves(userSettings.Gameplay.AutosaveSlots);
        }
        IsMouseVisible = !mouseCaptured || player.Player.ControlMode is PlayerControlMode.Container or PlayerControlMode.Paused;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (renderer is null || scene is null || player is null || combat is null || hud is null || snapshot is null || phase20Ui is null)
        {
            GraphicsDevice.Clear(Color.Black);
            return;
        }

        CameraState cameraState = camera.Build(snapshot.Player, 1f, GraphicsDevice.Viewport.AspectRatio);
        renderer.Draw(scene.Build((long)snapshot.Tick, cameraState, showDebug, player, showDebug ? combat.BuildDebugLines() : null));

        if (screen == Phase20ScreenKind.None && focus?.Active != true)
        {
            var presentation=combat.Presentation;
            string kind=items?.ActiveWeaponId==items?.RifleId?"rifle":items?.ActiveWeaponId==items?.MeleeId?"melee":"sidearm";
            var lastShot=presentation.RecentFeedback.LastOrDefault(f=>f.ActorId==player.Player.Id&&f.Kind==CombatFeedbackKind.ShotFired);
            var lastReload=presentation.RecentFeedback.LastOrDefault(f=>f.ActorId==player.Player.Id&&f.Kind==CombatFeedbackKind.ReloadStarted);
            float age=lastShot is null?1000:snapshot.Tick-lastShot.Tick;
            float reloadAge=lastReload is null?1000:snapshot.Tick-lastReload.Tick;
            float reload=reloadAge<75?MathF.Sin(reloadAge/75f*MathF.PI):0;
            float sway=userSettings.Accessibility.DisableHeadBob?0:MathF.Sin(snapshot.Tick*.13f)*.003f;
            viewWeapon?.Draw(kind,Math.Clamp(1-age/12f,0,1),reload,sway,aiming,age<2&&!userSettings.Accessibility.DisableFlashes);
            QuestTrackerHudModel? trackedHud = BuildTrackedQuestHud();
            hud.Draw(snapshot, gameplayInput.ActiveDevice, showDebug, userSettings, presentation, null, trackedHud, hudFeedback.Build(interfaceFrame), interfaceFrame, uiInputDevice);
        }

        if (screen == Phase20ScreenKind.Map) phase20Ui.WorldMap = BuildWorldMap();
        double interfaceSeconds = gameTime.TotalGameTime.TotalSeconds;
        if (focus?.Active == true)
            phase20Ui.DrawFocus(focus.BuildModel(), userSettings, new UiInteractionSnapshot(latestUiInput?.Pointer ?? Point.Zero, uiInputDevice, null, showUiDebug), interfaceSeconds);
        else if (screen != Phase20ScreenKind.None)
        {
            UiScreenState state = uiState.Get(screen);
            Point pointer = latestUiInput?.Pointer ?? Point.Zero;
            phase20Ui.DrawScreen(BuildCurrentScreenModel(), userSettings, new UiInteractionSnapshot(pointer, uiInputDevice, state.FocusedControlId, showUiDebug), interfaceSeconds);
        }

        titleAccumulator += gameTime.ElapsedGameTime.TotalSeconds;
        if (titleAccumulator >= 0.25)
        {
            titleAccumulator = 0;
            RendererTelemetry draw = renderer.Telemetry.Latest;
            AudioTelemetrySnapshot audio = audioHost?.CaptureTelemetry() ?? audioRuntime?.Telemetry ?? SilentAudioRuntime.CreateTelemetry();
            MusicTelemetrySnapshot music = musicRuntime?.Telemetry ?? SilentMusicRuntime.CreateTelemetry();
            string state = screen != Phase20ScreenKind.None ? screen.ToString() : focus?.Active == true ? "RoentgenFocus" : "Playing";
            string musicCue = string.IsNullOrWhiteSpace(music.CurrentCue) ? "pending" : music.CurrentCue;
            string audioFault = string.IsNullOrWhiteSpace(audio.FaultMessage) ? string.Empty : " !AUDIO";
            string musicFault = string.IsNullOrWhiteSpace(music.FaultMessage) ? string.Empty : " !MUSIC";
            Window.Title = $"Roentgen — Phase 21 | content {launchContent.Pack.QuestKernels.Count}Q/{launchContent.Fingerprint[..8]} | {state} | tick {snapshot.Tick} | hostiles {combat.LivingHostiles} | MUSIC {music.State}:{music.Situation}/{musicCue}{musicFault} | AUDIO {audio.RuntimeState} voices {audio.StartedVoices} underruns {audio.Underruns}{audioFault} | {draw.DrawCalls} draws";
        }
        base.Draw(gameTime);
    }

    private void CreateCampaignSession(CampaignSeed seed, Phase20CharacterCreation creation)
    {
        if (renderer is null) throw new InvalidOperationException("Renderer must exist before a campaign session is created.");
        DisposeCampaignSession();
        campaignSeed = seed;
        scene = new Phase9WorldScene(renderer, seed);
        worldMap = null;
        var collision = new PlayerCollisionWorld(scene.TerrainCollision, scene.StructureCollision, scene.DoorCollision, scene.CharacterCollision);
        StableId playerId = StableIdFactory.Create("player", $"phase20-{seed.Value:X16}");
        var playerState = new PlayerState(playerId, scene.SpawnPosition);
        player = new PlayerSimulationRuntime(playerState, collision, scene.DoorCollision, scene.InteractionWorld);
        progression = new Phase12ProgressionRuntime(playerId, creation);
        var itemSkills = new RpgItemSkillCapabilityProvider(progression.Capabilities);
        items = Phase13ItemRuntime.CreateIntegrated(playerId, seed, progression.StartingEquipment, 75_000, itemSkills);
        society = new Phase15SocietyRuntime(Phase15Content.CreateRegressionWorld(seed, playerId), seed);
        society.UpdateFromSimulationTick(0, items.World);
        narrative = new Phase16NarrativeRuntime(Phase16Content.CreateRegressionWorld(seed, playerId), playerId, new Phase16DomainEffectRouter(society.World, items.World, HandleOptionalNarrativeEffect));
        agents = new Phase14WorldAgentRuntime(scene, collision, player);
        combat = new Phase10CombatController(scene, player, collision, items, agents, society.World, progression.CombatCapabilities, progression.GetMaximumHealth(playerId));
        GeneratedCampaignPlan plan = new CampaignGenerator().Generate(new CampaignGenerationRequest(
            seed,
            playerId,
            society.World,
            Phase16Content.CreateRegressionBindingContext(),
            Phase21NarrativeContent.CreateLaunchKernels(),
            CompanionIds: combat.CompanionId.IsEmpty ? [] : [combat.CompanionId]));
        campaign = new Phase17CampaignRuntime(new CampaignWorldState(plan), narrative.World, society.World, playerId);
        _ = campaign.Initialize(0, society.World.Day);
        scene.RefreshCharacterCollision();
        player.SyncActorTargets(scene.BuildActorInteractionTargets());
        snapshot = player.Step(default);
        combat.Update(snapshot.Tick, player.Player, default);
        focus = new Phase20FocusRuntime(combat, progression, playerId, seed, userSettings.Accessibility);
        InitializeAudio(playerId);
        musicBridge = new Phase19MusicBridge(combat.CompanionId);
        sessionReady = true;
        playedSeconds = 0;
        autosaveSeconds = 0;
        endingPresented = false;
        hudFeedback.Reset();
        lastUiHoverId = null;
        trackedMapMarkerId = StableId.Empty;
        trackedQuestId = StableId.Empty;
        merchantPane = MerchantPane.MerchantStock;
        merchantStockSelection = merchantPlayerSelection = 0;
        merchantStockScroll = merchantPlayerScroll = 0;
        mapFilter = MapFilter.All;
        mapZoom = 1f;
        mapPanX = mapPanZ = 0.5f;
        journalFilter = JournalFilter.Active;
        worldMap = null;
        screenHistory.Clear();
    }

    private void DisposeCampaignSession()
    {
        audioHost?.Dispose();
        if (audioHost is null) audioRuntime?.Dispose();
        musicRuntime?.Dispose();
        audioHost = null;
        audioRuntime = null;
        audioBridge = null;
        musicRuntime = null;
        musicBridge = null;
        scene?.Dispose();
        scene = null;
        player = null;
        progression = null;
        items = null;
        agents = null;
        combat = null;
        society = null;
        narrative = null;
        campaign = null;
        focus = null;
        snapshot = null;
        hudFeedback.Reset();
        lastUiHoverId = null;
        sessionReady = false;
    }

    private void AdvanceSimulation(PlayerIntentFrame movement, CombatInputFrame attack, bool interfaceOpen)
    {
        if (player is null || scene is null || agents is null || society is null || narrative is null || campaign is null || combat is null || items is null) return;
        scene.RefreshCharacterCollision();
        player.SyncActorTargets(scene.BuildActorInteractionTargets());
        snapshot = player.Step(movement);
        UpdatePhysicalQuestFacts(snapshot.Tick);
        HandleDirectActorInteraction(snapshot.Tick);
        if (snapshot.OpenContainer is not null && screen == Phase20ScreenKind.None && focus?.Active != true)
        {
            uiState.Reset(Phase20ScreenKind.ContainerLoot);
            OpenScreen(Phase20ScreenKind.ContainerLoot);
        }
        scene.Update(player.Player.FeetPosition, runAmbientTasks: false);
        agents.Update(player.Player.FeetPosition, snapshot.Tick);
        scene.RefreshCharacterCollision();
        society.UpdateFromSimulationTick(snapshot.Tick, items.World);
        narrative.Update(snapshot.Tick, society.World.Day, BuildNarrativeQuery(snapshot.Tick));
        _ = campaign.Update(snapshot.Tick, society.World.Day);
        combat.Update(snapshot.Tick, player.Player, attack);
        QuestTrackerHudModel? trackedHud = BuildTrackedQuestHud();
        HudFeedbackObservation feedbackObservation = hudFeedback.Observe(interfaceFrame, snapshot, trackedHud);
        if (feedbackObservation.QuestAdvanced) EmitUiAudio(AudioEventKind.ObjectiveAdvanced);
        else if (feedbackObservation.NotificationAdded) EmitUiAudio(AudioEventKind.JournalUpdate);
        if (campaign.Campaign.Completed && !endingPresented && screen == Phase20ScreenKind.None && focus?.Active != true)
        {
            endingPresented = true;
            OpenScreen(Phase20ScreenKind.Ending);
        }
        UpdateAudio(interfaceOpen, focus?.Active == true);
    }

    private void HandleGameplayShortcuts(Phase20InputFrame input, ulong tick)
    {
        if (input.IsPressed(UiAction.ToggleDiagnostics)) showDebug = !showDebug;
        if (input.IsPressed(UiAction.Pause)) { OpenScreen(Phase20ScreenKind.PauseMenu); return; }
        if (input.IsPressed(UiAction.QuickSave)) { TryWriteSave("quicksave", SaveSlotKind.Quick, "Quick Save"); return; }
        if (input.IsPressed(UiAction.QuickLoad)) { TryLoadSlot("quicksave"); return; }
        if (input.IsPressed(UiAction.OpenFocus) && focus is not null)
        {
            if (focus.Enter(tick))
            {
                screen = Phase20ScreenKind.None;
                mouseCaptured = false;
            }
            else screenStatus = focus.Status;
            return;
        }

        Phase20ScreenKind requested = Phase20ScreenKind.None;
        if (input.IsPressed(UiAction.OpenInventory)) requested = Phase20ScreenKind.Inventory;
        else if (input.IsPressed(UiAction.OpenEquipment)) requested = Phase20ScreenKind.Equipment;
        else if (input.IsPressed(UiAction.OpenCharacter)) requested = Phase20ScreenKind.Character;
        else if (input.IsPressed(UiAction.OpenSkills)) requested = Phase20ScreenKind.Skills;
        else if (input.IsPressed(UiAction.OpenPerks)) requested = Phase20ScreenKind.Perks;
        else if (input.IsPressed(UiAction.OpenMap)) requested = Phase20ScreenKind.Map;
        else if (input.IsPressed(UiAction.OpenJournal)) requested = Phase20ScreenKind.Journal;
        else if (input.IsPressed(UiAction.OpenFactions)) requested = Phase20ScreenKind.Factions;
        else if (input.IsPressed(UiAction.OpenCrafting)) requested = Phase20ScreenKind.Crafting;
        else if (input.IsPressed(UiAction.OpenCompanion)) requested = Phase20ScreenKind.Companion;
        else if (input.IsPressed(UiAction.OpenDialogue)) requested = Phase20ScreenKind.Dialogue;
        else if (input.IsPressed(UiAction.OpenMerchant)) requested = Phase20ScreenKind.Merchant;
        if (requested != Phase20ScreenKind.None)
        {
            if (requested is Phase20ScreenKind.Dialogue or Phase20ScreenKind.Merchant)
            {
                StableId actor=requested==Phase20ScreenKind.Dialogue?Phase16Content.ProspectCaretakerId:Phase16Content.ProspectDeputyId;
                if(!CanReachActor(actor)){screenStatus="Approach the caretaker to talk, or the clinic technician to trade.";return;}
            }
            if(requested==Phase20ScreenKind.Crafting&&!NearWorkshop()){screenStatus="Use crafting near a clinic or workshop.";return;}
            if (requested == Phase20ScreenKind.Dialogue) EnsureConversation(tick);
            OpenScreen(requested);
        }
    }

    private void HandleFocusInput(Phase20InputFrame input, ulong tick)
    {
        if (focus is null) return;

        if (input.PointerPressed && phase20Ui is not null && phase20Ui.RenderedScreenKind == Phase20ScreenKind.RoentgenFocus)
        {
            UiHitRegion? hit = phase20Ui.HitTest(input.Pointer);
            if (hit?.Kind == UiControlKind.ActionButton && hit.Action.HasValue && hit.Enabled)
            {
                HandleFocusAction(hit.Action.Value, tick);
                return;
            }
            if (hit?.Kind == UiControlKind.ListItem && hit.Index >= 0 && focus.IsPlanning)
            {
                Phase20FocusModel model = focus.BuildModel();
                int current = Math.Max(0, model.SelectedPartIndex);
                int target = Math.Clamp(hit.Index, 0, Math.Max(0, model.TargetParts.Count - 1));
                int delta = target - current;
                for (int step = 0; step < Math.Abs(delta); step++) focus.SelectNextPart(Math.Sign(delta));
                return;
            }
        }

        if (input.IsPressed(UiAction.Cancel)) { HandleFocusAction(UiAction.Cancel, tick); return; }
        if (!focus.IsPlanning) return;
        if (input.IsPressed(UiAction.FocusNextTarget) || input.IsPressed(UiAction.NavigateRight)) HandleFocusAction(UiAction.FocusNextTarget, tick);
        if (input.IsPressed(UiAction.FocusPreviousTarget) || input.IsPressed(UiAction.NavigateLeft)) HandleFocusAction(UiAction.FocusPreviousTarget, tick);
        if (input.IsRepeated(UiAction.FocusNextPart) || input.IsRepeated(UiAction.NavigateDown)) HandleFocusAction(UiAction.FocusNextPart, tick);
        if (input.IsRepeated(UiAction.FocusPreviousPart) || input.IsRepeated(UiAction.NavigateUp)) HandleFocusAction(UiAction.FocusPreviousPart, tick);
        if (input.IsPressed(UiAction.FocusQueue) || input.IsPressed(UiAction.Confirm)) HandleFocusAction(UiAction.FocusQueue, tick);
        if (input.IsPressed(UiAction.FocusRemove) || input.IsPressed(UiAction.Delete)) HandleFocusAction(UiAction.FocusRemove, tick);
        if (input.IsPressed(UiAction.FocusCommit)) HandleFocusAction(UiAction.FocusCommit, tick);
    }

    private void HandleFocusAction(UiAction action, ulong tick)
    {
        if (focus is null) return;
        if (action == UiAction.Cancel) { focus.Cancel(); CloseScreenToGameplay(); return; }
        if (!focus.IsPlanning) return;
        switch (action)
        {
            case UiAction.FocusNextTarget: focus.SelectNextTarget(1); break;
            case UiAction.FocusPreviousTarget: focus.SelectNextTarget(-1); break;
            case UiAction.FocusNextPart: focus.SelectNextPart(1); break;
            case UiAction.FocusPreviousPart: focus.SelectNextPart(-1); break;
            case UiAction.FocusQueue: _ = focus.QueueSelectedAction(); break;
            case UiAction.FocusRemove: focus.RemoveLastAction(); break;
            case UiAction.FocusCommit: _ = focus.Commit(tick); break;
        }
    }

    private void HandleScreenInput(Phase20InputFrame input, ulong tick)
    {
        if (input.IsPressed(UiAction.ToggleDiagnostics))
        {
            showUiDebug = !showUiDebug;
            screenStatus = showUiDebug ? "UI diagnostics enabled." : "UI diagnostics disabled.";
        }

        if (screen == Phase20ScreenKind.InputBindings && bindingCapture is not null)
        {
            HandleBindingCaptureInput(input);
            return;
        }

        if (screen == Phase20ScreenKind.NewCampaign && characterNameEditing && HandleCharacterNameInput(input))
            return;

        if (screen == Phase20ScreenKind.Merchant)
        {
            HandleMerchantScreenInput(input, tick);
            return;
        }

        if (screen == Phase20ScreenKind.Map && HandleMapPointerInput(input))
            return;

        if (screen == Phase20ScreenKind.Dialogue && HandleDialogueShortcutInput(input, tick))
            return;

        HandleUiHoverFeedback(input);
        Phase20ScreenModel model = BuildCurrentScreenModel();
        int count = model.Entries.Count;
        UiScreenState state = uiState.Get(screen, count);

        if (input.ScrollDelta != 0 && phase20Ui is not null && phase20Ui.RenderedScreenKind == screen)
        {
            UiHitRegion? scrollRegion = phase20Ui.HitTest(input.Pointer, UiControlKind.ScrollPane);
            int delta = input.ScrollDelta > 0 ? -3 : 3;
            if (scrollRegion?.Id.StartsWith("detail", StringComparison.Ordinal) == true)
            {
                state = state with { DetailScrollOffset = Math.Max(0, state.DetailScrollOffset + delta) };
            }
            else
            {
                int visible = Math.Max(1, phase20Ui.LastVisibleEntryCount);
                int maximum = Math.Max(0, count - visible);
                state = state with { ScrollOffset = Math.Clamp(state.ScrollOffset + delta, 0, maximum) };
            }
            uiState.Set(screen, state);
        }

        if (input.PointerMoved && Mouse.GetState().LeftButton == ButtonState.Pressed && phase20Ui is not null && phase20Ui.RenderedScreenKind == screen)
        {
            UiHitRegion? dragged = phase20Ui.HitTest(input.Pointer, UiControlKind.Slider);
            if (dragged is not null && dragged.Index >= 0 && dragged.Index < count)
            {
                SetScreenSelection(dragged.Index);
                ApplySliderFromPointer(dragged.Index, input.Pointer.X, dragged.Bounds);
                return;
            }
        }

        if (input.PointerPressed && phase20Ui is not null && phase20Ui.RenderedScreenKind == screen)
        {
            UiHitRegion? hit = phase20Ui.HitTest(input.Pointer);
            if (hit is not null)
            {
                state = state with { FocusedControlId = hit.Id };
                uiState.Set(screen, state);
                switch (hit.Kind)
                {
                    case UiControlKind.TabButton:
                        if (hit.Id.StartsWith("subtab:", StringComparison.Ordinal)) ActivateSubTabIndex(hit.Index);
                        else ActivateTabIndex(hit.Index);
                        return;
                    case UiControlKind.ActionButton when hit.Action.HasValue:
                        if (hit.Enabled) DispatchScreenAction(hit.Action.Value, tick);
                        else { screenStatus = "That action is unavailable."; EmitUiAudio(AudioEventKind.InterfaceError); }
                        return;
                    case UiControlKind.Stepper when hit.Action.HasValue:
                        if (hit.Index >= 0 && hit.Index < count) SetScreenSelection(hit.Index);
                        AdjustSelectedSetting(hit.Action.Value == UiAction.NavigateLeft ? -1 : 1);
                        return;
                    case UiControlKind.Toggle:
                    case UiControlKind.Dropdown:
                        if (hit.Index >= 0 && hit.Index < count) SetScreenSelection(hit.Index);
                        AdjustSelectedSetting(1);
                        return;
                    case UiControlKind.Slider:
                        if (hit.Index >= 0 && hit.Index < count)
                        {
                            SetScreenSelection(hit.Index);
                            ApplySliderFromPointer(hit.Index, input.Pointer.X, hit.Bounds);
                        }
                        return;
                    case UiControlKind.TextBox:
                        if (hit.Index >= 0 && hit.Index < count) SetScreenSelection(hit.Index);
                        BeginCharacterNameEdit();
                        return;
                    case UiControlKind.Button:
                        if (hit.Index >= 0 && hit.Index < count) SetScreenSelection(hit.Index);
                        ActivateScreenSelection(tick);
                        return;
                    case UiControlKind.Card:
                        if (hit.Index >= 0 && hit.Index < count)
                        {
                            SetScreenSelection(hit.Index);
                            if (CharacterCreationTabScreens.Contains(screen) || input.PointerDoubleClicked)
                                ActivateScreenSelection(tick);
                        }
                        return;
                    case UiControlKind.ListItem:
                        if (hit.Index >= 0 && hit.Index < count)
                        {
                            SetScreenSelection(hit.Index);
                            if (screen == Phase20ScreenKind.CharacterReview || input.PointerDoubleClicked) ActivateScreenSelection(tick);
                        }
                        return;
                }
            }
        }

        int gridColumns = UiUxFinalization.GridColumns(model.Presentation);
        if (input.IsRepeated(UiAction.NavigateUp)) MoveScreenSelection(gridColumns > 1 ? -gridColumns : -1, count);
        if (input.IsRepeated(UiAction.NavigateDown)) MoveScreenSelection(gridColumns > 1 ? gridColumns : 1, count);
        if (input.IsPressed(UiAction.NextTab)) { MoveCurrentTab(1); return; }
        if (input.IsPressed(UiAction.PreviousTab)) { MoveCurrentTab(-1); return; }
        if (input.IsPressed(UiAction.NavigateLeft))
        {
            if (SelectedEntryAdjustsValue(model)) AdjustSelectedSetting(-1);
            else if (gridColumns > 1) MoveScreenSelection(-1, count);
        }
        if (input.IsPressed(UiAction.NavigateRight))
        {
            if (SelectedEntryAdjustsValue(model)) AdjustSelectedSetting(1);
            else if (gridColumns > 1) MoveScreenSelection(1, count);
        }
        if (input.IsPressed(UiAction.PageUp)) MoveScreenSelection(-Math.Max(1, phase20Ui?.LastVisibleEntryCount ?? 8), count);
        if (input.IsPressed(UiAction.PageDown)) MoveScreenSelection(Math.Max(1, phase20Ui?.LastVisibleEntryCount ?? 8), count);
        if (input.IsPressed(UiAction.Delete)) HandleDeleteSelection();
        if (input.IsPressed(UiAction.Cancel)) { CancelCurrentScreen(); return; }
        if (input.IsPressed(UiAction.Confirm)) ActivateScreenSelection(tick);
    }

    private void HandleMerchantScreenInput(Phase20InputFrame input, ulong tick)
    {
        if (items is null) { CancelCurrentScreen(); return; }
        Phase20ScreenModel model = BuildCurrentScreenModel();
        IReadOnlyList<Phase20MenuEntry> playerEntries = model.SecondaryEntries ?? [];

        if (input.ScrollDelta != 0 && phase20Ui is not null && phase20Ui.RenderedScreenKind == Phase20ScreenKind.Merchant)
        {
            UiHitRegion? scroll = phase20Ui.HitTest(input.Pointer, UiControlKind.ScrollPane);
            int delta = input.ScrollDelta > 0 ? -3 : 3;
            if (scroll?.Id == "merchant-stock") merchantStockScroll = Math.Clamp(merchantStockScroll + delta, 0, Math.Max(0, model.Entries.Count - 1));
            else if (scroll?.Id == "player-stock") merchantPlayerScroll = Math.Clamp(merchantPlayerScroll + delta, 0, Math.Max(0, playerEntries.Count - 1));
            else if (scroll?.Id.StartsWith("detail", StringComparison.Ordinal) == true)
            {
                UiScreenState state = uiState.Get(screen, model.Entries.Count);
                uiState.Set(screen, state with { DetailScrollOffset = Math.Max(0, state.DetailScrollOffset + delta) });
            }
        }

        if (input.PointerPressed && phase20Ui is not null && phase20Ui.RenderedScreenKind == Phase20ScreenKind.Merchant)
        {
            UiHitRegion? hit = phase20Ui.HitTest(input.Pointer);
            if (hit is not null)
            {
                if (hit.Kind == UiControlKind.ActionButton && hit.Action.HasValue)
                {
                    if (hit.Enabled) DispatchScreenAction(hit.Action.Value, tick); else { screenStatus = "That trade action is unavailable."; EmitUiAudio(AudioEventKind.InterfaceError); }
                    return;
                }
                if (hit.Kind == UiControlKind.ListItem && hit.Id.StartsWith("merchant-entry:", StringComparison.Ordinal))
                {
                    merchantPane = MerchantPane.MerchantStock;
                    merchantStockSelection = Math.Clamp(hit.Index, 0, Math.Max(0, model.Entries.Count - 1));
                    if (input.PointerDoubleClicked) HandleMerchantAction(UiAction.MerchantBuyOne, tick);
                    return;
                }
                if (hit.Kind == UiControlKind.ListItem && hit.Id.StartsWith("player-entry:", StringComparison.Ordinal))
                {
                    merchantPane = MerchantPane.PlayerInventory;
                    merchantPlayerSelection = Math.Clamp(hit.Index, 0, Math.Max(0, playerEntries.Count - 1));
                    if (input.PointerDoubleClicked) HandleMerchantAction(UiAction.MerchantSellOne, tick);
                    return;
                }
            }
        }

        if (input.IsPressed(UiAction.NextTab)) { MoveCurrentTab(1); return; }
        if (input.IsPressed(UiAction.PreviousTab)) { MoveCurrentTab(-1); return; }
        if (input.IsPressed(UiAction.NavigateLeft)) { merchantPane = MerchantPane.MerchantStock; screenStatus = "Merchant stock selected."; return; }
        if (input.IsPressed(UiAction.NavigateRight)) { merchantPane = MerchantPane.PlayerInventory; screenStatus = "Your inventory selected."; return; }
        if (input.IsRepeated(UiAction.NavigateUp)) MoveMerchantSelection(-1, model);
        if (input.IsRepeated(UiAction.NavigateDown)) MoveMerchantSelection(1, model);
        if (input.IsPressed(UiAction.Cancel)) { CancelCurrentScreen(); return; }
        if (input.IsPressed(UiAction.Confirm))
            HandleMerchantAction(merchantPane == MerchantPane.MerchantStock ? UiAction.MerchantBuyOne : UiAction.MerchantSellOne, tick);
    }

    private void MoveMerchantSelection(int direction, Phase20ScreenModel model)
    {
        if (merchantPane == MerchantPane.MerchantStock)
        {
            merchantStockSelection = Math.Clamp(merchantStockSelection + direction, 0, Math.Max(0, model.Entries.Count - 1));
            merchantStockScroll = KeepMerchantSelectionVisible(merchantStockScroll, merchantStockSelection, model.Entries.Count);
        }
        else
        {
            int count = model.SecondaryEntries?.Count ?? 0;
            merchantPlayerSelection = Math.Clamp(merchantPlayerSelection + direction, 0, Math.Max(0, count - 1));
            merchantPlayerScroll = KeepMerchantSelectionVisible(merchantPlayerScroll, merchantPlayerSelection, count);
        }
    }

    private static int KeepMerchantSelectionVisible(int scroll, int selection, int count)
    {
        const int estimatedVisibleRows = 10;
        if (selection < scroll) scroll = selection;
        else if (selection >= scroll + estimatedVisibleRows) scroll = selection - estimatedVisibleRows + 1;
        return Math.Clamp(scroll, 0, Math.Max(0, count - 1));
    }

    private bool HandleMapPointerInput(Phase20InputFrame input)
    {
        if (phase20Ui is null || phase20Ui.RenderedScreenKind != Phase20ScreenKind.Map) return false;
        UiHitRegion? hit = phase20Ui.HitTest(input.Pointer);

        if (input.ScrollDelta != 0 && (hit?.Kind == UiControlKind.MapViewport || mapDragBounds.Contains(input.Pointer)))
        {
            AdjustMapZoom(input.ScrollDelta > 0 ? 0.35f : -0.35f);
            return true;
        }

        if (input.PointerPressed && hit?.Kind == UiControlKind.MapMarker)
        {
            SetScreenSelection(hit.Index);
            if (input.PointerDoubleClicked) ActivateScreenSelection(snapshot?.Tick ?? 0);
            return true;
        }

        if (input.PointerPressed && hit?.Kind == UiControlKind.MapViewport)
        {
            mapDragging = true;
            mapDragLast = input.Pointer;
            mapDragBounds = hit.Bounds;
            return true;
        }

        if (mapDragging && input.PointerMoved && Mouse.GetState().LeftButton == ButtonState.Pressed)
        {
            int dx = input.Pointer.X - mapDragLast.X;
            int dy = input.Pointer.Y - mapDragLast.Y;
            float span = 1f / Math.Clamp(mapZoom, 1f, 6f);
            mapPanX -= dx / (float)Math.Max(1, mapDragBounds.Width) * span;
            mapPanZ -= dy / (float)Math.Max(1, mapDragBounds.Height) * span;
            mapDragLast = input.Pointer;
            ClampMapPan();
            return true;
        }

        if (mapDragging && input.PointerReleased)
        {
            mapDragging = false;
            return true;
        }
        return false;
    }

    private void DispatchScreenAction(UiAction action, ulong tick)
    {
        // Explicit action buttons receive one restrained confirmation cue. List activation, tabs,
        // crafting and trading provide their own semantic sounds so they are intentionally excluded.
        if (action is not (UiAction.Confirm or UiAction.Cancel or UiAction.NextTab or UiAction.PreviousTab
            or UiAction.NavigateLeft or UiAction.NavigateRight
            or UiAction.CraftingCraftOne or UiAction.CraftingCraftFive or UiAction.CraftingCraftAll
            or UiAction.MerchantBuyOne or UiAction.MerchantBuyFive or UiAction.MerchantBuyAll
            or UiAction.MerchantSellOne or UiAction.MerchantSellFive or UiAction.MerchantSellAll))
            EmitUiAudio(AudioEventKind.InterfaceConfirm);

        switch (action)
        {
            case UiAction.Confirm: ActivateScreenSelection(tick); break;
            case UiAction.Cancel: CancelCurrentScreen(); break;
            case UiAction.Delete: HandleDeleteSelection(); break;
            case UiAction.NavigateLeft: AdjustSelectedSetting(-1); break;
            case UiAction.NavigateRight: AdjustSelectedSetting(1); break;
            case UiAction.NextTab: MoveCurrentTab(1); break;
            case UiAction.PreviousTab: MoveCurrentTab(-1); break;
            case UiAction.WizardBack: NavigateWizard(-1); break;
            case UiAction.WizardNext: NavigateWizard(1); break;
            case UiAction.WizardFinish: ActivateCharacterReview("start"); break;
            case UiAction.WizardReset:
                characterCreation.ResetAttributes();
                screenStatus = "Attributes reset. Reallocate the remaining points.";
                break;
            case UiAction.WizardRecommended:
                characterCreation.ApplyRecommendedAttributes();
                screenStatus = "Recommended balanced attribute spread applied.";
                break;
            case UiAction.InventoryUse:
            case UiAction.InventoryEquip:
            case UiAction.InventoryRepair:
            case UiAction.InventoryDrop:
            case UiAction.InventorySplit:
            case UiAction.InventoryInspect:
            case UiAction.InventorySort:
                HandleInventoryAction(action, tick);
                break;
            case UiAction.EquipmentChange:
            case UiAction.EquipmentEquip:
            case UiAction.EquipmentUnequip:
            case UiAction.EquipmentRepair:
            case UiAction.EquipmentInspect:
            case UiAction.EquipmentBack:
                HandleEquipmentAction(action, tick);
                break;
            case UiAction.SkillIncrease:
                ActivateScreenSelection(tick);
                break;
            case UiAction.PerkAcquire:
                ActivateScreenSelection(tick);
                break;
            case UiAction.CraftingCraftOne:
            case UiAction.CraftingCraftFive:
            case UiAction.CraftingCraftAll:
            case UiAction.CraftingToggleFavorite:
            case UiAction.CraftingToggleUnavailable:
                HandleCraftingAction(action, tick);
                break;
            case UiAction.ContainerTake:
            case UiAction.ContainerTakeAll:
                HandleContainerAction(action);
                break;
            case UiAction.MerchantBuyOne:
            case UiAction.MerchantBuyFive:
            case UiAction.MerchantBuyAll:
            case UiAction.MerchantSellOne:
            case UiAction.MerchantSellFive:
            case UiAction.MerchantSellAll:
            case UiAction.MerchantSwitchPane:
            case UiAction.MerchantInspect:
                HandleMerchantAction(action, tick);
                break;
            case UiAction.MapTrack:
            case UiAction.MapClearWaypoint:
            case UiAction.MapZoomIn:
            case UiAction.MapZoomOut:
            case UiAction.MapCenterPlayer:
                HandleMapAction(action);
                break;
            case UiAction.JournalTrack:
            case UiAction.JournalUntrack:
            case UiAction.JournalShowOnMap:
                HandleJournalAction(action);
                break;
            case UiAction.FactionShowOnMap:
                ShowSelectedSettlementOnMap();
                break;
            case UiAction.SettingsReset:
                ResetCurrentSettingsSection();
                break;
            default:
                if (action == UiAction.FocusCommit && screen == Phase20ScreenKind.BindingConflict) ActivateScreenSelection(tick);
                else ActivateScreenSelection(tick);
                break;
        }
    }

    private void HandleUiHoverFeedback(Phase20InputFrame input)
    {
        if (!input.PointerMoved || phase20Ui is null || phase20Ui.RenderedScreenKind != screen) return;
        UiHitRegion? hit = phase20Ui.HitTest(input.Pointer);
        string? id = hit is { Enabled: true } ? hit.Id : null;
        if (string.Equals(id, lastUiHoverId, StringComparison.Ordinal)) return;
        lastUiHoverId = id;
        if (id is not null) EmitUiAudio(AudioEventKind.InterfaceNavigate);
    }

    private void EmitUiAudio(AudioEventKind kind)
    {
        if (audioRuntime is null || audioBridge is null || snapshot is null) return;
        audioBridge.EmitInterface(audioRuntime, snapshot.Tick, kind);
    }

    private void SetScreenSelection(int selection)
    {
        Phase20ScreenModel model = BuildCurrentScreenModel();
        int count = model.Entries.Count;
        int previousSelection = screenSelection;
        screenSelection = count == 0 ? 0 : Math.Clamp(selection, 0, count - 1);
        if (screenSelection != previousSelection && latestUiInput?.ActiveDevice is UiInputDevice.Keyboard or UiInputDevice.Gamepad) EmitUiAudio(AudioEventKind.InterfaceNavigate);
        UiScreenState state = uiState.Get(screen, count) with { SelectedIndex = screenSelection, FocusedControlId = $"entry:{screenSelection}" };
        int visible = Math.Max(1, phase20Ui?.LastVisibleEntryCount ?? 1);
        state = state.EnsureSelectionVisible(visible);
        uiState.Set(screen, state);
    }

    private void ActivateScreenSelection(ulong tick)
    {
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return;
        Phase20MenuEntry entry = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)];
        if (!entry.Enabled) { screenStatus = entry.Description ?? "That option is unavailable."; EmitUiAudio(AudioEventKind.InterfaceError); return; }
        EmitUiAudio(AudioEventKind.InterfaceConfirm);
        switch (screen)
        {
            case Phase20ScreenKind.MainMenu: ActivateMainMenu(entry.Id); break;
            case Phase20ScreenKind.NewCampaign: ActivateNewCampaign(entry.Id); break;
            case Phase20ScreenKind.CharacterAttributes:
                screenStatus = "Use Left/Right or the − / + controls to allocate attributes.";
                break;
            case Phase20ScreenKind.CharacterSkills:
                if (Enum.TryParse(entry.Id, out SkillId skill))
                {
                    if (!characterCreation.CycleSpecialization(skill))
                        screenStatus = "Three skills are already specialized. Deselect one first.";
                    else
                        screenStatus = characterCreation.SpecializedSkills.Contains(skill) ? $"{skill} specialized." : $"{skill} specialization removed.";
                }
                break;
            case Phase20ScreenKind.CharacterBackground:
                characterCreation.SetBackground(entry.Id);
                screenStatus = $"{entry.Label} selected.";
                break;
            case Phase20ScreenKind.CharacterTraits:
                if (!characterCreation.ToggleTrait(entry.Id))
                    screenStatus = characterCreation.TraitIds.Count >= 2
                        ? "Two traits are already selected. Deselect one before choosing another."
                        : "That trait is incompatible with your current selection.";
                else screenStatus = characterCreation.TraitIds.Contains(entry.Id) ? $"{entry.Label} selected." : $"{entry.Label} deselected.";
                break;
            case Phase20ScreenKind.CharacterReview: ActivateCharacterReview(entry.Id); break;
            case Phase20ScreenKind.PauseMenu: ActivatePause(entry.Id); break;
            case Phase20ScreenKind.Inventory: ActivateInventory(entry.Id); break;
            case Phase20ScreenKind.ItemInspection: ReturnFromChildScreen(); break;
            case Phase20ScreenKind.ContainerLoot: HandleContainerAction(UiAction.ContainerTake); break;
            case Phase20ScreenKind.Equipment: ActivateEquipment(entry.Id); break;
            case Phase20ScreenKind.Map: ActivateMap(entry.Id); break;
            case Phase20ScreenKind.Journal: ActivateJournal(entry.Id); break;
            case Phase20ScreenKind.Factions:
                if (factionSection == FactionScreenSection.Settlements) ShowSelectedSettlementOnMap();
                break;
            case Phase20ScreenKind.Skills: ActivateSkill(entry.Id); break;
            case Phase20ScreenKind.Perks: ActivatePerk(entry.Id); break;
            case Phase20ScreenKind.Crafting: ActivateCrafting(entry.Id, tick); break;
            case Phase20ScreenKind.Merchant: ActivateMerchant(entry.Id, tick); break;
            case Phase20ScreenKind.Companion: ActivateCompanion(entry.Id, tick); break;
            case Phase20ScreenKind.Dialogue: ActivateDialogue(entry.Id, tick); break;
            case Phase20ScreenKind.LoadGame: if (entry.Id != "new-manual") TryLoadSlot(entry.Id); break;
            case Phase20ScreenKind.SaveGame: ActivateSaveSlot(entry.Id); break;
            case Phase20ScreenKind.Settings:
                if (entry.Id == "bindings") OpenChildScreen(Phase20ScreenKind.InputBindings);
                else AdjustSelectedSetting(1);
                break;
            case Phase20ScreenKind.Accessibility: AdjustSelectedSetting(1); break;
            case Phase20ScreenKind.InputBindings: BeginBindingCapture(entry.Id); break;
            case Phase20ScreenKind.BindingConflict: ActivateBindingConflict(entry.Id); break;
            case Phase20ScreenKind.Tutorials: screenStatus = $"Tutorial '{entry.Label}' queued for replay."; break;
            case Phase20ScreenKind.Credits: ReturnFromChildScreen(); break;
            case Phase20ScreenKind.Confirmation: ActivateConfirmation(entry.Id); break;
            case Phase20ScreenKind.Ending: OpenScreen(Phase20ScreenKind.Epilogue); break;
            case Phase20ScreenKind.Epilogue: OpenScreen(Phase20ScreenKind.MainMenu); break;
        }
    }

    private void ActivateMainMenu(string id)
    {
        switch (id)
        {
            case "continue":
                SaveSlotSummary? newest = saveSlots?.List().FirstOrDefault(static value => value.IsValid);
                if (newest is null) screenStatus = localization.Get("status.noSave");
                else TryLoadSlot(newest.Metadata.SlotId);
                break;
            case "new":
                screenHistory.Clear();
                ResetCharacterCreationUiState();
                pendingCampaignSeed = CreateRandomSeed();
                OpenScreen(Phase20ScreenKind.NewCampaign);
                break;
            case "load": OpenChildScreen(Phase20ScreenKind.LoadGame); break;
            case "settings": OpenChildScreen(Phase20ScreenKind.Settings); break;
            case "accessibility": OpenChildScreen(Phase20ScreenKind.Accessibility); break;
            case "tutorials": OpenChildScreen(Phase20ScreenKind.Tutorials); break;
            case "credits": OpenChildScreen(Phase20ScreenKind.Credits); break;
            case "quit":
                exitAfterConfirmation = true;
                OpenConfirmation("Quit Roentgen", localization.Get("confirm.quit"));
                break;
        }
    }

    private void ActivateNewCampaign(string id)
    {
        switch (id)
        {
            case "name": BeginCharacterNameEdit(); break;
            case "seed": pendingCampaignSeed = new CampaignSeed(pendingCampaignSeed.Value + 1UL); break;
            case "randomize":
                pendingCampaignSeed = CreateRandomSeed();
                screenStatus = "Campaign seed randomized.";
                break;
            case "difficulty": CycleDifficulty(1); break;
        }
    }

    private void ActivateCharacterReview(string id)
    {
        switch (id)
        {
            case "start":
                StableId prospectivePlayer = StableIdFactory.Create("player", $"phase20-{pendingCampaignSeed.Value:X16}");
                CharacterCreationValidation validation = characterCreation.Validate(prospectivePlayer);
                if (!validation.IsValid)
                {
                    screenStatus = string.Join(" ", validation.Errors);
                    return;
                }
                CreateCampaignSession(pendingCampaignSeed, characterCreation);
                CloseScreenToGameplay();
                screenStatus = "Campaign generated. Welcome to Roentgen.";
                break;
            case "identity": OpenScreen(Phase20ScreenKind.NewCampaign); break;
            case "attributes": OpenScreen(Phase20ScreenKind.CharacterAttributes); break;
            case "skills": OpenScreen(Phase20ScreenKind.CharacterSkills); break;
            case "background": OpenScreen(Phase20ScreenKind.CharacterBackground); break;
            case "traits": OpenScreen(Phase20ScreenKind.CharacterTraits); break;
        }
    }

    private void ActivatePause(string id)
    {
        switch (id)
        {
            case "resume": CloseScreenToGameplay(); break;
            case "save": OpenChildScreen(Phase20ScreenKind.SaveGame); break;
            case "load": OpenChildScreen(Phase20ScreenKind.LoadGame); break;
            case "settings": OpenChildScreen(Phase20ScreenKind.Settings); break;
            case "accessibility": OpenChildScreen(Phase20ScreenKind.Accessibility); break;
            case "tutorials": OpenChildScreen(Phase20ScreenKind.Tutorials); break;
            case "main":
                returnToMenuAfterConfirmation = true;
                OpenConfirmation("Return to Main Menu", localization.Get("confirm.mainMenu"));
                break;
        }
    }

    private void ActivateInventory(string id)
    {
        if (items is null || !StableId.TryParse(id, out StableId itemId)) return;
        _ = items.ActivateInventoryItem(itemId);
        screenStatus = items.LastStatus;
        combat?.RefreshFromItemWorld();
    }

    private void ActivateEquipment(string id)
    {
        if (items is null) return;
        if (equipmentView == EquipmentViewMode.Slots)
        {
            if (!Enum.TryParse(id, out EquipmentSlot slot)) return;
            equipmentSelectedSlot = slot;
            equipmentView = EquipmentViewMode.CompatibleItems;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Equipment);
            screenStatus = $"Compatible items for {slot}.";
            return;
        }
        if (!StableId.TryParse(id, out StableId itemId)) return;
        _ = items.EquipInventoryItemToSlot(itemId, equipmentSelectedSlot, snapshot?.Tick ?? 0);
        screenStatus = items.LastStatus;
        screenSelection = 0;
        uiState.Reset(Phase20ScreenKind.Equipment);
        combat?.RefreshFromItemWorld();
    }

    private QuestTrackerHudModel? BuildTrackedQuestHud()
    {
        if (trackedQuestId.IsEmpty || narrative is null) return null;
        QuestJournalEntry? quest = new Phase16NarrativeScreenBuilder().BuildJournal(narrative.World, JournalDisplayName).FirstOrDefault(value => value.QuestId == trackedQuestId);
        if (quest is null || quest.Status is QuestStatus.Completed or QuestStatus.Failed)
        {
            trackedQuestId = StableId.Empty;
            return null;
        }
        string? distance = null;
        if (TryGetQuestMapTarget(trackedQuestId, out StableId target) && scene is not null && player is not null &&
            TryGetMapNormalizedPosition(target, out float targetX, out float targetZ, out string targetName, out _))
        {
            WorldGenerationProfile profile = scene.Topology.Profile;
            float playerX = ((int)(player.Player.FeetPosition.XMillimeters / 10) - profile.MinimumWorldXCentimeters) / (float)(profile.WorldWidthMeters * 100);
            float playerZ = ((int)(player.Player.FeetPosition.ZMillimeters / 10) - profile.MinimumWorldZCentimeters) / (float)(profile.WorldDepthMeters * 100);
            double dx = (targetX - playerX) * profile.WorldWidthMeters;
            double dz = (targetZ - playerZ) * profile.WorldDepthMeters;
            distance = $"{targetName} • {Math.Sqrt(dx * dx + dz * dz):0} m";
        }
        return new QuestTrackerHudModel(quest.Title, quest.CurrentObjective, distance);
    }

    private WorldMapModel BuildWorldMap()
    {
        var topology = scene!.Topology;
        var profile = topology.Profile;
        float X(int x) => (x - profile.MinimumWorldXCentimeters) / (float)(profile.WorldWidthMeters * 100);
        float Z(int z) => (z - profile.MinimumWorldZCentimeters) / (float)(profile.WorldDepthMeters * 100);
        if (worldMap is null)
        {
            const int count = 96;
            var colors = new uint[count * count];
            var evaluator = new TerrainHeightEvaluator(topology);
            for (int z = 0; z < count; z++)
            for (int x = 0; x < count; x++)
            {
                int wx = profile.MinimumWorldXCentimeters + x * profile.WorldWidthMeters * 100 / (count - 1);
                int wz = profile.MinimumWorldZCentimeters + z * profile.WorldDepthMeters * 100 / (count - 1);
                int h = evaluator.SampleHeightCentimeters(wx, wz);
                Color c = TerrainSurfacePalette.ColorFor(evaluator.SampleSurface(wx, wz, h, 0));
                float shade = Math.Clamp(0.6f + (h - profile.MinimumElevationCentimeters) /
                    (float)Math.Max(1, profile.MaximumElevationCentimeters - profile.MinimumElevationCentimeters) * 0.5f, 0.55f, 1f);
                colors[z * count + x] = new Color(c.ToVector3() * shade).PackedValue;
            }
            worldMap = new WorldMapModel(
                topology.ContentChecksum.ToString(), profile.WorldWidthMeters, profile.WorldDepthMeters, count, colors,
                topology.Reservations.Select(p => new MapMarker(p.Id.ToString(), p.Name, X(p.Center.XCentimeters), Z(p.Center.ZCentimeters), p.Purpose.ToString())).ToArray(),
                topology.Roads.Segments.Select(road => new MapRoad(road.Polyline.Select(p => (X(p.XCentimeters), Z(p.ZCentimeters))).ToArray())).ToArray(),
                0, 0, 0, null);
        }

        HashSet<StableId> questTargets = GetQuestMapTargets();
        var markerList = worldMap.Markers.Select(marker =>
        {
            bool questTarget = StableId.TryParse(marker.Id, out StableId id) && questTargets.Contains(id);
            return marker with { QuestTarget = questTarget };
        }).ToList();
        HashSet<string> existingMarkerIds = markerList.Select(static marker => marker.Id).ToHashSet(StringComparer.Ordinal);
        foreach (StableId target in questTargets.Order())
        {
            if (existingMarkerIds.Contains(target.ToString())) continue;
            if (!TryGetMapNormalizedPosition(target, out float markerX, out float markerZ, out string markerName, out string markerKind)) continue;
            markerList.Add(new MapMarker(target.ToString(), markerName, markerX, markerZ, markerKind, QuestTarget: true));
        }
        MapMarker[] markers = markerList.ToArray();
        IReadOnlyList<MapMarker> visible = FilterMapMarkers(markers);
        string? selected = visible.Count == 0 ? null : visible[Math.Clamp(screenSelection, 0, visible.Count - 1)].Id;
        ClampMapPan();
        return worldMap with
        {
            Markers = markers,
            PlayerX = X((int)(player!.Player.FeetPosition.XMillimeters / 10)),
            PlayerZ = Z((int)(player.Player.FeetPosition.ZMillimeters / 10)),
            HeadingRadians = MathHelper.ToRadians(player.Player.BodyYawUnits / 10000f),
            TrackedId = trackedMapMarkerId.IsEmpty ? null : trackedMapMarkerId.ToString(),
            Zoom = mapZoom,
            PanX = mapPanX,
            PanZ = mapPanZ,
            SelectedId = selected
        };
    }

    private Phase20ScreenModel BuildSpatialMapScreen()
    {
        if (scene is null || player is null) return new(Phase20ScreenKind.Map, "Map", "No campaign", [], 0, [], 0, null, string.Empty, screenStatus);
        WorldMapModel map = BuildWorldMap();
        IReadOnlyList<MapMarker> visible = FilterMapMarkers(map.Markers);
        Phase20MenuEntry[] entries = visible.Select(marker =>
        {
            double dx = (marker.X - map.PlayerX) * map.WidthMeters;
            double dz = (marker.Z - map.PlayerZ) * map.DepthMeters;
            string badge = marker.Id == map.TrackedId ? "WAYPOINT" : marker.QuestTarget ? "QUEST" : marker.Kind.ToUpperInvariant();
            return new Phase20MenuEntry(marker.Id, marker.Name, $"{Math.Sqrt(dx * dx + dz * dz):0} m", marker.QuestTarget ? $"{marker.Kind} • current quest objective" : marker.Kind, true, false, badge);
        }).ToArray();

        Phase20Panel? detail = null;
        if (visible.Count > 0)
        {
            MapMarker selected = visible[Math.Clamp(screenSelection, 0, visible.Count - 1)];
            double dx = (selected.X - map.PlayerX) * map.WidthMeters;
            double dz = (selected.Z - map.PlayerZ) * map.DepthMeters;
            var lines = new List<string>
            {
                $"Type: {PlayerFacingMapKind(selected.Kind)}",
                $"Distance: {Math.Sqrt(dx * dx + dz * dz):0} m",
                selected.QuestTarget ? "Quest objective: Yes" : "Quest objective: No",
                selected.Id == map.TrackedId ? "Waypoint: TRACKED" : "Waypoint: Not tracked"
            };
            if (StableId.TryParse(selected.Id, out StableId id) && society is not null)
            {
                SettlementState? settlement = society.World.Settlements.FirstOrDefault(value => value.Id == id);
                if (settlement is not null)
                {
                    lines.Add($"Controller: {society.World.GetFaction(settlement.ControllingFactionId).Plan.DisplayName}");
                    lines.Add($"Population: {settlement.Population.TotalPopulation}");
                    lines.Add($"Stability: {settlement.StabilityPermille / 10.0:F0}%");
                }
            }
            detail = new Phase20Panel(selected.Name, lines, Status: selected.Id == map.TrackedId ? "Current waypoint" : null);
        }

        return new Phase20ScreenModel(
            Phase20ScreenKind.Map,
            "Wasteland Atlas",
            "Mouse wheel zooms • drag the map to pan • select a marker or location to inspect it",
            entries,
            screenSelection,
            Phase20ScreenFactory.GameplayTabs,
            5,
            detail,
            string.Empty,
            string.IsNullOrEmpty(screenStatus) ? "Set a waypoint or inspect a known location." : screenStatus,
            ActionPrompts:
            [
                new(UiAction.MapTrack, "Set Waypoint", entries.Length > 0, ShowBinding: false),
                new(UiAction.MapClearWaypoint, "Clear Waypoint", !trackedMapMarkerId.IsEmpty, ShowBinding: false),
                new(UiAction.MapZoomOut, "Zoom -", mapZoom > 1.01f, ShowBinding: false),
                new(UiAction.MapZoomIn, "Zoom +", mapZoom < 5.99f, ShowBinding: false),
                new(UiAction.MapCenterPlayer, "Center on You", ShowBinding: false),
                new(UiAction.Cancel, "Close")
            ],
            SubTabs: Phase20ScreenFactory.MapTabs,
            SelectedSubTab: (int)mapFilter);
    }

    private IReadOnlyList<MapMarker> FilterMapMarkers(IReadOnlyList<MapMarker> markers) => markers.Where(marker => mapFilter switch
    {
        MapFilter.All => true,
        MapFilter.Settlements => marker.Kind.Equals(nameof(LandmarkPurpose.Settlement), StringComparison.OrdinalIgnoreCase),
        MapFilter.Ruins => marker.Kind.Equals(nameof(LandmarkPurpose.Ruin), StringComparison.OrdinalIgnoreCase),
        MapFilter.Infrastructure => marker.Kind.Equals(nameof(LandmarkPurpose.Infrastructure), StringComparison.OrdinalIgnoreCase),
        MapFilter.Quests => marker.QuestTarget,
        _ => true
    }).OrderByDescending(static marker => marker.QuestTarget).ThenBy(static marker => marker.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    private HashSet<StableId> GetQuestMapTargets()
    {
        var result = new HashSet<StableId>();
        if (narrative is null || scene is null) return result;
        foreach (QuestInstanceState quest in narrative.World.Quests.Where(static q => q.Status is QuestStatus.Generated or QuestStatus.Offered or QuestStatus.Active or QuestStatus.Suspended))
        {
            foreach (ObjectiveRuntimeState runtime in quest.Objectives.Values.Where(static state => state.Revealed && state.Status is ObjectiveStatus.Active or ObjectiveStatus.Dormant))
            {
                QuestObjectiveDefinition? definition = quest.Plan.Kernel.Objectives.FirstOrDefault(value => value.Id == runtime.ObjectiveId);
                if (definition is null || string.IsNullOrWhiteSpace(definition.TargetSlotId)) continue;
                StableId target = quest.Plan.ResolveSlot(definition.TargetSlotId);
                if (!target.IsEmpty && TryGetMapNormalizedPosition(target, out _, out _, out _, out _)) result.Add(target);
            }
        }
        return result;
    }

    private bool TryGetQuestMapTarget(StableId questId, out StableId targetId)
    {
        targetId = StableId.Empty;
        if (narrative is null || scene is null || questId.IsEmpty) return false;
        QuestInstanceState? quest = narrative.World.Quests.FirstOrDefault(value => value.Id == questId);
        if (quest is null) return false;
        IEnumerable<ObjectiveRuntimeState> candidates = quest.Objectives.Values
            .Where(static state => state.Revealed && state.Status is not ObjectiveStatus.Completed and not ObjectiveStatus.Failed and not ObjectiveStatus.Skipped)
            .OrderBy(static state => state.Status == ObjectiveStatus.Active ? 0 : 1)
            .ThenBy(static state => state.ChangedTick);
        foreach (ObjectiveRuntimeState runtime in candidates)
        {
            QuestObjectiveDefinition? definition = quest.Plan.Kernel.Objectives.FirstOrDefault(value => value.Id == runtime.ObjectiveId);
            if (definition is null || string.IsNullOrWhiteSpace(definition.TargetSlotId)) continue;
            StableId target = quest.Plan.ResolveSlot(definition.TargetSlotId);
            if (!target.IsEmpty && TryGetMapNormalizedPosition(target, out _, out _, out _, out _)) { targetId = target; return true; }
        }
        return false;
    }

    private bool TryGetMapNormalizedPosition(StableId id, out float x, out float z, out string name, out string kind)
    {
        x = z = 0;
        name = JournalDisplayName(id);
        kind = "Quest";
        if (scene is null || id.IsEmpty) return false;
        RegionalTopology topology = scene.Topology;
        WorldGenerationProfile profile = topology.Profile;
        float X(int centimeters) => (centimeters - profile.MinimumWorldXCentimeters) / (float)(profile.WorldWidthMeters * 100);
        float Z(int centimeters) => (centimeters - profile.MinimumWorldZCentimeters) / (float)(profile.WorldDepthMeters * 100);

        LandmarkReservation? reservation = topology.Reservations.FirstOrDefault(value => value.Id == id);
        if (reservation is not null)
        {
            x = X(reservation.Center.XCentimeters); z = Z(reservation.Center.ZCentimeters); name = reservation.Name; kind = reservation.Purpose.ToString(); return true;
        }

        InteractiveTargetDefinition? target = scene.InteractionWorld.Targets.FirstOrDefault(value => value.Id == id || value.SourceId == id);
        if (target is not null)
        {
            x = X(target.WorldPosition.XCentimeters); z = Z(target.WorldPosition.ZCentimeters); name = JournalDisplayName(id); kind = "Quest"; return true;
        }

        if (id == Phase16Content.ProspectClinicId)
        {
            LandmarkReservation start = topology.Reservations.First(static value => value.Purpose == LandmarkPurpose.Settlement);
            x = X(start.Center.XCentimeters); z = Z(start.Center.ZCentimeters); name = "Prospect Clinic"; kind = nameof(LandmarkPurpose.Settlement); return true;
        }
        return false;
    }

    private bool QuestHasMapTarget(StableId questId) => TryGetQuestMapTarget(questId, out _);

    private string JournalDisplayName(StableId id)
    {
        if (id.IsEmpty) return "Unknown";
        if (id == Phase16Content.ProspectClinicId) return "Prospect Clinic";
        if (id == Phase16Content.MeridianRelayId) return "Meridian Relay";
        if (id == Phase16Content.QuarantineDepotId) return "Quarantine Depot";
        var reservation = scene?.Topology.Reservations.FirstOrDefault(value => value.Id == id);
        if (reservation is not null) return reservation.Name;
        SettlementState? settlement = society?.World.Settlements.FirstOrDefault(value => value.Id == id);
        if (settlement is not null) return settlement.Plan.DisplayName;
        FactionState? faction = society?.World.Factions.FirstOrDefault(value => value.Id == id);
        if (faction is not null) return faction.Plan.DisplayName;
        if (scene?.CharacterHandles.Any(value => value.State.Id == id) == true) return "Known contact";
        return "Known location or contact";
    }

    private static string PlayerFacingMapKind(string kind) => kind switch
    {
        nameof(LandmarkPurpose.Settlement) => "Settlement",
        nameof(LandmarkPurpose.Ruin) => "Ruin",
        nameof(LandmarkPurpose.Infrastructure) => "Infrastructure",
        nameof(LandmarkPurpose.Hazard) => "Hazard",
        nameof(LandmarkPurpose.QuestReserved) => "Special location",
        _ => kind
    };

    private void ActivateMap(string id)
    {
        if (!StableId.TryParse(id, out StableId markerId)) return;
        trackedMapMarkerId = markerId;
        string name = TryGetMapNormalizedPosition(markerId, out _, out _, out string resolvedName, out _) ? resolvedName : "location";
        screenStatus = $"Waypoint set: {name}.";
    }

    private void ShowSelectedSettlementOnMap()
    {
        if (screen != Phase20ScreenKind.Factions || factionSection != FactionScreenSection.Settlements) return;
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return;
        string id = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id;
        const string prefix = "settlement:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal) || !StableId.TryParse(id[prefix.Length..], out StableId settlementId))
        {
            screenStatus = "That settlement cannot be located on the atlas.";
            return;
        }
        if (!TryGetMapNormalizedPosition(settlementId, out float x, out float z, out string name, out _))
        {
            screenStatus = "That settlement has no known atlas position.";
            return;
        }
        trackedMapMarkerId = settlementId;
        mapFilter = MapFilter.Settlements;
        mapZoom = Math.Max(mapZoom, 2.5f);
        mapPanX = x;
        mapPanZ = z;
        ClampMapPan();
        screenSelection = 0;
        uiState.Reset(Phase20ScreenKind.Map);
        screenStatus = $"Waypoint set: {name}.";
        OpenScreen(Phase20ScreenKind.Map);
    }

    private void HandleMapAction(UiAction action)
    {
        switch (action)
        {
            case UiAction.MapTrack:
                ActivateScreenSelection(snapshot?.Tick ?? 0);
                break;
            case UiAction.MapClearWaypoint:
                trackedMapMarkerId = StableId.Empty;
                screenStatus = "Waypoint cleared.";
                break;
            case UiAction.MapZoomIn: AdjustMapZoom(0.5f); break;
            case UiAction.MapZoomOut: AdjustMapZoom(-0.5f); break;
            case UiAction.MapCenterPlayer: CenterMapOnPlayer(); break;
        }
    }

    private void AdjustMapZoom(float delta)
    {
        mapZoom = Math.Clamp(mapZoom + delta, 1f, 6f);
        ClampMapPan();
        screenStatus = $"Map zoom {mapZoom:0.0}x.";
    }

    private void CenterMapOnPlayer()
    {
        WorldMapModel map = BuildWorldMap();
        mapPanX = map.PlayerX;
        mapPanZ = map.PlayerZ;
        ClampMapPan();
        screenStatus = "Map centered on your position.";
    }

    private void CenterMapOnMarker(StableId id, bool zoomToObjective = false)
    {
        WorldMapModel map = BuildWorldMap();
        MapMarker? marker = map.Markers.FirstOrDefault(value => value.Id == id.ToString());
        if (marker is null) return;
        if (zoomToObjective) mapZoom = Math.Max(mapZoom, 2.25f);
        mapPanX = marker.X;
        mapPanZ = marker.Z;
        ClampMapPan();
    }

    private void ClampMapPan()
    {
        float span = 1f / Math.Clamp(mapZoom, 1f, 6f);
        float half = span / 2f;
        mapPanX = Math.Clamp(mapPanX, half, 1f - half);
        mapPanZ = Math.Clamp(mapPanZ, half, 1f - half);
    }

    private void ActivateJournal(string id)
    {
        if (!TryParsePrefixedStableId(id, "quest", out StableId questId)) return;
        trackedQuestId = questId;
        screenStatus = "Quest tracked. Its current objective will remain visible on the HUD.";
    }

    private void HandleJournalAction(UiAction action)
    {
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return;
        string id = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id;
        if (!TryParsePrefixedStableId(id, "quest", out StableId questId)) return;
        switch (action)
        {
            case UiAction.JournalTrack:
                trackedQuestId = questId;
                screenStatus = "Quest tracked.";
                break;
            case UiAction.JournalUntrack:
                if (trackedQuestId == questId) trackedQuestId = StableId.Empty;
                screenStatus = "Quest tracking cleared.";
                break;
            case UiAction.JournalShowOnMap:
                if (!TryGetQuestMapTarget(questId, out StableId target)) { screenStatus = "No mapped objective is currently known for this quest."; return; }
                trackedQuestId = questId;
                trackedMapMarkerId = target;
                mapFilter = MapFilter.Quests;
                CenterMapOnMarker(target, zoomToObjective: true);
                OpenScreen(Phase20ScreenKind.Map);
                screenStatus = "Quest objective shown on the atlas and set as your waypoint.";
                break;
        }
    }

    private static bool TryParsePrefixedStableId(string id, string prefix, out StableId value)
    {
        string marker = prefix + ":";
        if (!id.StartsWith(marker, StringComparison.Ordinal)) { value = StableId.Empty; return false; }
        return StableId.TryParse(id[marker.Length..], out value);
    }

    private void ActivateSkill(string id)
    {
        if (progression is null || player is null || !Enum.TryParse(id, out SkillId skill)) return;
        ProgressionCommandResult result = progression.CharacterScreen.IncreaseSkill(player.Player.Id, skill);
        screenStatus = result.Message;
    }

    private void ActivatePerk(string id)
    {
        if (progression is null || player is null) return;
        ProgressionCommandResult result = progression.CharacterScreen.AcquirePerk(player.Player.Id, id);
        screenStatus = result.Message;
        focus?.ApplyAccessibility(userSettings.Accessibility);
    }

    private void ActivateCrafting(string id, ulong tick)
    {
        if (items is null) return;
        CraftingResult result = items.CraftKnownRecipeMultiple(id, 1, tick);
        screenStatus = result.Message;
        combat?.RefreshFromItemWorld();
    }

    private void ActivateMerchant(string id, ulong tick)
    {
        if (!StableId.TryParse(id, out _)) return;
        HandleMerchantAction(merchantPane == MerchantPane.MerchantStock ? UiAction.MerchantBuyOne : UiAction.MerchantSellOne, tick);
    }

    private void HandleMerchantAction(UiAction action, ulong tick)
    {
        if (items is null) return;
        if (action == UiAction.MerchantSwitchPane)
        {
            merchantPane = merchantPane == MerchantPane.MerchantStock ? MerchantPane.PlayerInventory : MerchantPane.MerchantStock;
            screenStatus = merchantPane == MerchantPane.MerchantStock ? "Merchant stock selected." : "Your inventory selected.";
            return;
        }

        Phase20ScreenModel model = BuildCurrentScreenModel();
        IReadOnlyList<Phase20MenuEntry> activeEntries = merchantPane == MerchantPane.MerchantStock ? model.Entries : model.SecondaryEntries ?? [];
        int activeSelection = merchantPane == MerchantPane.MerchantStock ? merchantStockSelection : merchantPlayerSelection;
        if (activeEntries.Count == 0) { screenStatus = "There is nothing available in this trade pane."; return; }
        Phase20MenuEntry entry = activeEntries[Math.Clamp(activeSelection, 0, activeEntries.Count - 1)];
        if (!StableId.TryParse(entry.Id, out StableId itemId)) return;

        if (action == UiAction.MerchantInspect)
        {
            inspectingItemId = itemId;
            OpenChildScreen(Phase20ScreenKind.ItemInspection);
            return;
        }

        bool buy = action is UiAction.MerchantBuyOne or UiAction.MerchantBuyFive or UiAction.MerchantBuyAll;
        bool sell = action is UiAction.MerchantSellOne or UiAction.MerchantSellFive or UiAction.MerchantSellAll;
        if (!buy && !sell) return;
        merchantPane = buy ? MerchantPane.MerchantStock : MerchantPane.PlayerInventory;
        MerchantTradePreviewUiModel preview = items.BuildMerchantTradePreview(itemId, merchantSelling: buy);
        int requested = action is UiAction.MerchantBuyFive or UiAction.MerchantSellFive ? 5
            : action is UiAction.MerchantBuyAll or UiAction.MerchantSellAll ? preview.MaximumAffordableQuantity
            : 1;
        if (requested <= 0) { screenStatus = buy ? "You cannot afford that trade." : "The merchant cannot afford that trade."; return; }
        requested = Math.Min(requested, preview.MaximumAffordableQuantity);
        TradeExecutionResult result = buy
            ? items.BuyFromMerchant(itemId, requested, tick)
            : items.SellToMerchant(itemId, requested, tick);
        screenStatus = result.Message;
        EmitUiAudio(result.Success ? AudioEventKind.InterfaceTransaction : AudioEventKind.InterfaceError);
        combat?.RefreshFromItemWorld();

        Phase20ScreenModel refreshed = Phase20ScreenFactory.Merchant(items, merchantStockSelection, merchantPlayerSelection, merchantStockScroll, merchantPlayerScroll, merchantPane, screenStatus);
        merchantStockSelection = Math.Clamp(merchantStockSelection, 0, Math.Max(0, refreshed.Entries.Count - 1));
        merchantPlayerSelection = Math.Clamp(merchantPlayerSelection, 0, Math.Max(0, (refreshed.SecondaryEntries?.Count ?? 0) - 1));
        merchantStockScroll = Math.Clamp(merchantStockScroll, 0, Math.Max(0, refreshed.Entries.Count - 1));
        merchantPlayerScroll = Math.Clamp(merchantPlayerScroll, 0, Math.Max(0, (refreshed.SecondaryEntries?.Count ?? 0) - 1));
    }

    private void ActivateCompanion(string id, ulong tick)
    {
        if (combat is null) return;
        CompanionCommandKind? kind = id switch
        {
            "follow" => CompanionCommandKind.Follow,
            "stay" => CompanionCommandKind.Stay,
            "attack" => CompanionCommandKind.AttackTarget,
            "cover" => CompanionCommandKind.TakeCover,
            "regroup" => CompanionCommandKind.Regroup,
            "avoid" => CompanionCommandKind.AvoidCombat,
            "prefer-ranged" => CompanionCommandKind.PreferRanged,
            "prefer-melee" => CompanionCommandKind.PreferMelee,
            _ => null
        };
        if (!kind.HasValue)
        {
            screenStatus = companionSection is CompanionScreenSection.Overview or CompanionScreenSection.Equipment
                ? "This page is informational. Choose Commands or Behavior to issue an order."
                : "Select a companion command.";
            return;
        }
        CompanionCommandResult result = combat.CommandCompanion(kind.Value, tick);
        screenStatus = result.Message;
    }

    private void ActivateDialogue(string id, ulong tick)
    {
        if (narrative?.CurrentDialogue is not DialogueRenderedNode current) return;
        EnsureDialogueHistory(current);
        DialogueRenderedChoice? selected = current.Choices.FirstOrDefault(value => string.Equals(value.Id, id, StringComparison.Ordinal));
        if (selected is null || !selected.Enabled) { screenStatus = selected?.DisabledReason ?? "That response is unavailable."; return; }
        AppendDialogueHistory(current.SpeakerName, current.Text);
        AppendDialogueHistory("YOU", selected.Text);
        _ = narrative.ChooseDialogue(id, BuildNarrativeQuery(tick), BuildDialoguePresentation());
        screenSelection = 0;
        uiState.Set(Phase20ScreenKind.Dialogue, new UiScreenState());
        if (narrative.CurrentDialogue is DialogueRenderedNode next)
        {
            dialogueHistorySessionId = next.SessionId;
        }
        else
        {
            AppendDialogueHistory("SYSTEM", "Conversation ended.");
            CloseScreenToGameplay();
        }
    }

    private bool HandleDialogueShortcutInput(Phase20InputFrame input, ulong tick)
    {
        if (narrative?.CurrentDialogue is null) return false;
        Keys[] numberRow = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9];
        Keys[] numberPad = [Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4, Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9];
        for (int index = 0; index < numberRow.Length; index++)
        {
            if (!input.NewlyPressedKeys.Contains(numberRow[index]) && !input.NewlyPressedKeys.Contains(numberPad[index])) continue;
            Phase20ScreenModel model = BuildDialogueScreen();
            if (index >= model.Entries.Count)
            {
                screenStatus = $"There is no response {index + 1}.";
                return true;
            }
            SetScreenSelection(index);
            Phase20MenuEntry entry = model.Entries[index];
            if (!entry.Enabled)
            {
                screenStatus = entry.Description ?? "That response is unavailable.";
                return true;
            }
            ActivateDialogue(entry.Id, tick);
            return true;
        }
        return false;
    }

    private void EnsureDialogueHistory(DialogueRenderedNode node)
    {
        if (dialogueHistorySessionId == node.SessionId && dialogueHistory.Count > 0) return;
        dialogueHistory.Clear();
        dialogueHistorySessionId = node.SessionId;
    }

    private void AppendDialogueHistory(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        dialogueHistory.Add($"{speaker.ToUpperInvariant()}: {text.Trim()}");
        const int maximumHistoryLines = 80;
        if (dialogueHistory.Count > maximumHistoryLines)
            dialogueHistory.RemoveRange(0, dialogueHistory.Count - maximumHistoryLines);
    }

    private void ActivateSaveSlot(string id)
    {
        string slotId = id == "new-manual" ? saveSlots?.NextManualSlotId() ?? "manual-001" : id;
        TryWriteSave(slotId, SaveSlotKind.Manual, id == "new-manual" ? $"Manual Save {slotId[^3..]}" : "Manual Save");
    }

    private void ActivateConfirmation(string id)
    {
        if (id == "no") { CancelConfirmation(); return; }
        if (!dropItemAfterConfirmation.IsEmpty && items is not null && player is not null)
        {
            StableId dropping = dropItemAfterConfirmation;
            dropItemAfterConfirmation = StableId.Empty;
            FixedVector3 feet = player.Player.FeetPosition;
            _ = items.DropInventoryItem(dropping, new FixedItemPosition(feet.XMillimeters, feet.YMillimeters, feet.ZMillimeters), snapshot?.Tick ?? 0);
            string resultStatus = items.LastStatus;
            combat?.RefreshFromItemWorld();
            ReturnFromChildScreen();
            screenStatus = resultStatus;
            return;
        }
        if (deleteSaveAfterConfirmation is not null)
        {
            saveSlots?.Delete(deleteSaveAfterConfirmation);
            deleteSaveAfterConfirmation = null;
            ReturnFromChildScreen();
            screenStatus = "Save deleted.";
            return;
        }
        if (exitAfterConfirmation) { Exit(); return; }
        if (returnToMenuAfterConfirmation)
        {
            returnToMenuAfterConfirmation = false;
            screenHistory.Clear();
            OpenScreen(Phase20ScreenKind.MainMenu);
            return;
        }
        CancelConfirmation();
    }

    private void CancelConfirmation()
    {
        exitAfterConfirmation = false;
        returnToMenuAfterConfirmation = false;
        deleteSaveAfterConfirmation = null;
        dropItemAfterConfirmation = StableId.Empty;
        ReturnFromChildScreen();
    }

    private void HandleDeleteSelection()
    {
        if (screen == Phase20ScreenKind.InputBindings)
        {
            userSettings = userSettings with { Bindings = InputBindingDefaults.Create() };
            PersistSettings("Bindings reset to defaults.");
            return;
        }
        if (screen == Phase20ScreenKind.Inventory)
        {
            HandleInventoryAction(UiAction.InventoryDrop, snapshot?.Tick ?? 0);
            return;
        }
        if (screen is not (Phase20ScreenKind.SaveGame or Phase20ScreenKind.LoadGame) || saveSlots is null) return;
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return;
        string id = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id;
        if (id == "new-manual") return;
        deleteSaveAfterConfirmation = id;
        OpenConfirmation("Delete Save", localization.Get("confirm.deleteSave"));
    }

    private void CancelCurrentScreen()
    {
        EmitUiAudio(AudioEventKind.InterfaceCancel);
        switch (screen)
        {
            case Phase20ScreenKind.MainMenu: return;
            case Phase20ScreenKind.NewCampaign: OpenScreen(Phase20ScreenKind.MainMenu); return;
            case Phase20ScreenKind.CharacterAttributes: OpenScreen(Phase20ScreenKind.NewCampaign); return;
            case Phase20ScreenKind.CharacterSkills: OpenScreen(Phase20ScreenKind.CharacterAttributes); return;
            case Phase20ScreenKind.CharacterBackground: OpenScreen(Phase20ScreenKind.CharacterSkills); return;
            case Phase20ScreenKind.CharacterTraits: OpenScreen(Phase20ScreenKind.CharacterBackground); return;
            case Phase20ScreenKind.CharacterReview: OpenScreen(Phase20ScreenKind.CharacterTraits); return;
            case Phase20ScreenKind.ItemInspection: ReturnFromChildScreen(); return;
            case Phase20ScreenKind.Equipment when equipmentView == EquipmentViewMode.CompatibleItems:
                ReturnEquipmentToSlots();
                return;
            case Phase20ScreenKind.ContainerLoot:
                player?.CloseOpenContainer();
                if (player is not null) snapshot = player.BuildPresentationSnapshot();
                CloseScreenToGameplay();
                return;
            case Phase20ScreenKind.Confirmation: CancelConfirmation(); return;
            case Phase20ScreenKind.PauseMenu: CloseScreenToGameplay(); return;
            case Phase20ScreenKind.LoadGame:
            case Phase20ScreenKind.SaveGame:
            case Phase20ScreenKind.Settings:
            case Phase20ScreenKind.Accessibility:
            case Phase20ScreenKind.InputBindings:
                bindingCapture = null;
                ReturnFromChildScreen(); return;
            case Phase20ScreenKind.BindingConflict:
                bindingConflict = null;
                ReturnFromChildScreen(); return;
            case Phase20ScreenKind.Tutorials:
            case Phase20ScreenKind.Credits:
                ReturnFromChildScreen(); return;
            case Phase20ScreenKind.Dialogue:
                narrative?.CancelConversation();
                CloseScreenToGameplay();
                return;
            default: CloseScreenToGameplay(); return;
        }
    }

    private static readonly Phase20ScreenKind[] GameplayTabScreens =
    [
        Phase20ScreenKind.Inventory, Phase20ScreenKind.Equipment, Phase20ScreenKind.Character,
        Phase20ScreenKind.Skills, Phase20ScreenKind.Perks, Phase20ScreenKind.Map,
        Phase20ScreenKind.Journal, Phase20ScreenKind.Factions, Phase20ScreenKind.Crafting,
        Phase20ScreenKind.Companion
    ];

    private static readonly Phase20ScreenKind[] CharacterCreationTabScreens =
    [
        Phase20ScreenKind.NewCampaign, Phase20ScreenKind.CharacterAttributes, Phase20ScreenKind.CharacterSkills,
        Phase20ScreenKind.CharacterBackground, Phase20ScreenKind.CharacterTraits, Phase20ScreenKind.CharacterReview
    ];

    private void MoveCurrentTab(int direction)
    {
        if (screen is Phase20ScreenKind.Settings or Phase20ScreenKind.Accessibility)
        {
            int current = screen == Phase20ScreenKind.Accessibility ? (int)Phase20SettingsSection.Accessibility : (int)settingsSection;
            ActivateSubTabIndex((current + Math.Sign(direction) + Phase20ScreenFactory.SettingsTabs.Length) % Phase20ScreenFactory.SettingsTabs.Length);
            return;
        }

        Phase20ScreenKind[] tabs = GameplayTabScreens.Contains(screen) ? GameplayTabScreens : CharacterCreationTabScreens.Contains(screen) ? CharacterCreationTabScreens : [];
        if (tabs.Length == 0) return;
        int index = Array.IndexOf(tabs, screen);
        index = (index + Math.Sign(direction) + tabs.Length) % tabs.Length;
        EmitUiAudio(AudioEventKind.InterfaceTab);
        if (CharacterCreationTabScreens.Contains(screen)) TryOpenCreationStage(index);
        else OpenScreen(tabs[index]);
    }

    private void ActivateTabIndex(int index)
    {
        Phase20ScreenKind[] tabs = GameplayTabScreens.Contains(screen) ? GameplayTabScreens : CharacterCreationTabScreens.Contains(screen) ? CharacterCreationTabScreens : [];
        if (index < 0 || index >= tabs.Length) return;
        if (CharacterCreationTabScreens.Contains(screen))
        {
            if (tabs[index] != screen) EmitUiAudio(AudioEventKind.InterfaceTab);
            TryOpenCreationStage(index);
            return;
        }
        if (tabs[index] != screen) { EmitUiAudio(AudioEventKind.InterfaceTab); OpenScreen(tabs[index]); }
    }

    private void ResetCharacterCreationUiState()
    {
        characterNameEditing = false;
        characterNameBeforeEdit = string.Empty;
        foreach (Phase20ScreenKind kind in CharacterCreationTabScreens) uiState.Reset(kind);
    }

    private void MoveScreenSelection(int delta, int count)
    {
        if (count <= 0) { SetScreenSelection(0); return; }
        SetScreenSelection(Math.Clamp(screenSelection + delta, 0, count - 1));
    }

    private bool SelectedEntryAdjustsValue(Phase20ScreenModel model)
    {
        if (screen is Phase20ScreenKind.CharacterAttributes or Phase20ScreenKind.NewCampaign or Phase20ScreenKind.Settings or Phase20ScreenKind.Accessibility)
        {
            if (model.Entries.Count == 0) return false;
            return screen is Phase20ScreenKind.CharacterAttributes or Phase20ScreenKind.NewCampaign
                || UiUxFinalization.IsAdjustableEntry(model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)]);
        }
        return false;
    }

    private void AdjustSelectedSetting(int direction)
    {
        if (direction == 0) return;
        if (screen == Phase20ScreenKind.CharacterAttributes)
        {
            if (screenSelection < ProgressionRules.AttributeOrder.Count)
            {
                AttributeId attribute = ProgressionRules.AttributeOrder[screenSelection];
                if (!characterCreation.AdjustAttribute(attribute, Math.Sign(direction)))
                    screenStatus = direction > 0 ? "No attribute points remain or this attribute is at maximum." : "That attribute is already at its minimum.";
            }
            return;
        }
        if (screen == Phase20ScreenKind.NewCampaign)
        {
            Phase20ScreenModel model = BuildCurrentScreenModel();
            if (model.Entries.Count == 0) return;
            string id = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id;
            if (id == "seed") pendingCampaignSeed = new CampaignSeed(unchecked(pendingCampaignSeed.Value + (ulong)(long)Math.Sign(direction)));
            else if (id == "difficulty") CycleDifficulty(direction);
            return;
        }
        if (screen == Phase20ScreenKind.Settings) AdjustSettingsPage(direction);
        else if (screen == Phase20ScreenKind.Accessibility) AdjustAccessibilitySetting(direction);
    }

    private void AdjustSettingsPage(int direction)
    {
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return;
        string id = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id;
        int step = Math.Sign(direction);
        switch (settingsSection)
        {
            case Phase20SettingsSection.Gameplay:
            {
                Phase20GameplaySettings value = userSettings.Gameplay;
                value = id switch
                {
                    "difficulty" => value with { Difficulty = Cycle(value.Difficulty, step) },
                    "tutorials" => value with { TutorialsEnabled = !value.TutorialsEnabled },
                    "autosaves" => value with { AutosavesEnabled = !value.AutosavesEnabled },
                    "autosave-slots" => value with { AutosaveSlots = value.AutosaveSlots + step },
                    "autosave-interval" => value with { AutosaveIntervalMinutes = value.AutosaveIntervalMinutes + step },
                    _ => value
                };
                userSettings = userSettings with { Gameplay = value.Validate() };
                PersistSettings("Gameplay setting applied.");
                break;
            }
            case Phase20SettingsSection.Controls:
            {
                Phase20GameplaySettings gameplay = userSettings.Gameplay;
                Phase20AccessibilitySettings accessibility = userSettings.Accessibility;
                gameplay = id switch
                {
                    "mouse" => gameplay with { MouseSensitivity = gameplay.MouseSensitivity + step * 0.02f },
                    "gamepad" => gameplay with { GamepadLookSensitivity = gameplay.GamepadLookSensitivity + step * 0.2f },
                    "invert" => gameplay with { InvertVerticalLook = !gameplay.InvertVerticalLook },
                    _ => gameplay
                };
                accessibility = id switch
                {
                    "aim" => accessibility with { AimBehavior = Cycle(accessibility.AimBehavior, step) },
                    "crouch" => accessibility with { CrouchBehavior = Cycle(accessibility.CrouchBehavior, step) },
                    "sprint" => accessibility with { SprintBehavior = Cycle(accessibility.SprintBehavior, step) },
                    _ => accessibility
                };
                userSettings = userSettings with { Gameplay = gameplay.Validate(), Accessibility = accessibility.Validate() };
                ApplyCameraSettings();
                PersistSettings("Control setting applied.");
                break;
            }
            case Phase20SettingsSection.Video:
            {
                Phase20VideoSettings video = userSettings.Video;
                Phase20GameplaySettings gameplay = userSettings.Gameplay;
                switch (id)
                {
                    case "fullscreen": video = video with { Fullscreen = !video.Fullscreen }; break;
                    case "resolution": CycleResolution(step); return;
                    case "vsync": video = video with { VSync = !video.VSync }; break;
                    case "fov": gameplay = gameplay with { FieldOfViewDegrees = gameplay.FieldOfViewDegrees + step * 5f }; break;
                }
                userSettings = userSettings with { Video = video.Validate(), Gameplay = gameplay.Validate() };
                ApplyVideoSettings();
                ApplyCameraSettings();
                PersistSettings("Video setting applied.");
                break;
            }
            case Phase20SettingsSection.Audio:
            {
                Phase20AudioInterfaceSettings audio = userSettings.Audio;
                audio = id switch
                {
                    "audio-master" => audio with { MasterPermille = audio.MasterPermille + step * 50 },
                    "audio-music" => audio with { MusicPermille = audio.MusicPermille + step * 50 },
                    "audio-interface" => audio with { InterfacePermille = audio.InterfacePermille + step * 50 },
                    "audio-weapons" => audio with { WeaponsPermille = audio.WeaponsPermille + step * 50 },
                    "audio-creatures" => audio with { CreaturesPermille = audio.CreaturesPermille + step * 50 },
                    "audio-foley" => audio with { FoleyPermille = audio.FoleyPermille + step * 50 },
                    "audio-ambience" => audio with { AmbiencePermille = audio.AmbiencePermille + step * 50 },
                    "audio-focus" => audio with { FocusPermille = audio.FocusPermille + step * 50 },
                    _ => audio
                };
                userSettings = userSettings with { Audio = audio.Validate() };
                PersistSettings("Audio volume applied.");
                RestartAudioForSettings();
                break;
            }
        }
    }

    private void AdjustAccessibilitySetting(int direction)
    {
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return;
        string id = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id;
        int step = Math.Sign(direction);
        Phase20AccessibilitySettings value = userSettings.Accessibility;
        value = id switch
        {
            "ui-scale" => value with { InterfaceScale = value.InterfaceScale + step * 0.05f },
            "subtitle-scale" => value with { SubtitleScale = value.SubtitleScale + step * 0.05f },
            "contrast" => value with { HighContrast = !value.HighContrast },
            "color" => value with { ColorVisionMode = Cycle(value.ColorVisionMode, step) },
            "shake" => value with { DisableCameraShake = !value.DisableCameraShake },
            "bob" => value with { DisableHeadBob = !value.DisableHeadBob },
            "flashes" => value with { DisableFlashes = !value.DisableFlashes },
            "motion-blur" => value with { DisableMotionBlur = !value.DisableMotionBlur },
            "motion" => value with { ReduceMotion = !value.ReduceMotion },
            "reticle" => value with { PersistentReticle = !value.PersistentReticle },
            "dialogue-pause" => value with { PauseDuringDialogue = !value.PauseDuringDialogue },
            "menu-pause" => value with { PauseInMenus = !value.PauseInMenus },
            "focus-camera" => value with { CinematicFocusCamera = !value.CinematicFocusCamera },
            "focus-intensity" => value with { FocusOverlayIntensityPermille = value.FocusOverlayIntensityPermille + step * 50 },
            "focus-flash" => value with { FocusFlashIntensityPermille = value.FocusFlashIntensityPermille + step * 50 },
            "aim" => value with { AimBehavior = Cycle(value.AimBehavior, step) },
            "crouch" => value with { CrouchBehavior = Cycle(value.CrouchBehavior, step) },
            "sprint" => value with { SprintBehavior = Cycle(value.SprintBehavior, step) },
            _ => value
        };
        userSettings = userSettings with { Accessibility = value.Validate() };
        focus?.ApplyAccessibility(userSettings.Accessibility);
        ApplyCameraSettings();
        PersistSettings("Accessibility setting applied.");
    }

    private void ActivateSubTabIndex(int index)
    {
        // Sub-tabs are semantically tabs regardless of which system owns them. Pointer clicks and
        // keyboard/controller cycling therefore share the same low-key tab cue.
        EmitUiAudio(AudioEventKind.InterfaceTab);
        if (screen == Phase20ScreenKind.Inventory)
        {
            if (index < 0 || index >= Phase20ScreenFactory.InventoryTabs.Length) return;
            inventoryCategory = (InventoryCategory)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Inventory);
            screenStatus = $"Inventory category: {inventoryCategory}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Equipment)
        {
            if (index < 0 || index >= Phase20ScreenFactory.EquipmentTabs.Length) return;
            if ((EquipmentViewMode)index == EquipmentViewMode.Slots) ReturnEquipmentToSlots();
            else
            {
                if (equipmentView == EquipmentViewMode.Slots && TryGetSelectedEquipmentSlot(out EquipmentSlot slot)) equipmentSelectedSlot = slot;
                equipmentView = EquipmentViewMode.CompatibleItems;
                screenSelection = 0;
                uiState.Reset(Phase20ScreenKind.Equipment);
                screenStatus = $"Compatible items for {equipmentSelectedSlot}.";
            }
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Skills)
        {
            if (index < 0 || index >= Phase20ScreenFactory.SkillTabs.Length) return;
            skillCategory = (SkillCategory)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Skills);
            screenStatus = $"Skill category: {Phase20ScreenFactory.SkillTabs[index]}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Perks)
        {
            if (index < 0 || index >= Phase20ScreenFactory.PerkTabs.Length) return;
            perkFilter = (PerkFilter)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Perks);
            screenStatus = $"Perk filter: {Phase20ScreenFactory.PerkTabs[index]}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Crafting)
        {
            if (index < 0 || index >= Phase20ScreenFactory.CraftingTabs.Length) return;
            craftingCategory = (CraftingCategory)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Crafting);
            screenStatus = $"Crafting category: {Phase20ScreenFactory.CraftingTabs[index]}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Map)
        {
            if (index < 0 || index >= Phase20ScreenFactory.MapTabs.Length) return;
            mapFilter = (MapFilter)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Map);
            screenStatus = $"Map filter: {Phase20ScreenFactory.MapTabs[index]}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Journal)
        {
            if (index < 0 || index >= Phase20ScreenFactory.JournalTabs.Length) return;
            journalFilter = (JournalFilter)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Journal);
            screenStatus = $"Journal: {Phase20ScreenFactory.JournalTabs[index]}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Factions)
        {
            if (index < 0 || index >= Phase20ScreenFactory.FactionTabs.Length) return;
            factionSection = (FactionScreenSection)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Factions);
            screenStatus = $"Faction view: {Phase20ScreenFactory.FactionTabs[index]}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen == Phase20ScreenKind.Companion)
        {
            if (index < 0 || index >= Phase20ScreenFactory.CompanionTabs.Length) return;
            companionSection = (CompanionScreenSection)index;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Companion);
            screenStatus = $"Companion view: {Phase20ScreenFactory.CompanionTabs[index]}.";
            phase20Ui?.InvalidateHitMap();
            return;
        }

        if (screen is Phase20ScreenKind.Settings or Phase20ScreenKind.Accessibility)
        {
            if (index < 0 || index >= Phase20ScreenFactory.SettingsTabs.Length) return;
            Phase20SettingsSection target = (Phase20SettingsSection)index;
            screenSelection = 0;
            if (target == Phase20SettingsSection.Accessibility)
            {
                uiState.Reset(Phase20ScreenKind.Accessibility);
                OpenScreen(Phase20ScreenKind.Accessibility);
            }
            else
            {
                settingsSection = target;
                uiState.Reset(Phase20ScreenKind.Settings);
                OpenScreen(Phase20ScreenKind.Settings);
            }
        }
    }

    private void NavigateWizard(int direction)
    {
        int current = Array.IndexOf(CharacterCreationTabScreens, screen);
        if (current < 0) return;
        if (direction < 0 && current == 0)
        {
            OpenScreen(Phase20ScreenKind.MainMenu);
            return;
        }
        TryOpenCreationStage(current + Math.Sign(direction));
    }

    private bool TryOpenCreationStage(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= CharacterCreationTabScreens.Length) return false;
        int current = Array.IndexOf(CharacterCreationTabScreens, screen);
        if (targetIndex <= current)
        {
            OpenScreen(CharacterCreationTabScreens[targetIndex]);
            return true;
        }

        if (targetIndex >= 1 && string.IsNullOrWhiteSpace(characterCreation.CharacterName))
        {
            screenStatus = "Enter a character name before continuing.";
            return false;
        }
        if (targetIndex >= 2 && characterCreation.AttributePointsRemaining != 0)
        {
            screenStatus = $"Allocate all attribute points first. {characterCreation.AttributePointsRemaining} remain.";
            return false;
        }
        if (targetIndex >= 3 && characterCreation.SpecializedSkills.Count != 3)
        {
            screenStatus = $"Choose exactly three specialized skills first. {characterCreation.SpecializedSkills.Count}/3 selected.";
            return false;
        }
        if (targetIndex >= 5 && characterCreation.TraitIds.Count != 2)
        {
            screenStatus = $"Choose exactly two compatible traits first. {characterCreation.TraitIds.Count}/2 selected.";
            return false;
        }

        OpenScreen(CharacterCreationTabScreens[targetIndex]);
        return true;
    }

    private void BeginCharacterNameEdit()
    {
        if (screen != Phase20ScreenKind.NewCampaign) return;
        characterNameBeforeEdit = characterCreation.CharacterName;
        characterNameEditing = true;
        screenStatus = "Editing character name. Enter confirms; Esc cancels.";
        phase20Ui?.InvalidateHitMap();
    }

    private bool HandleCharacterNameInput(Phase20InputFrame input)
    {
        if (!characterNameEditing) return false;
        if (input.IsPressed(UiAction.Cancel))
        {
            characterCreation.CharacterName = characterNameBeforeEdit;
            characterNameEditing = false;
            screenStatus = "Name edit cancelled.";
            return true;
        }
        if (input.IsPressed(UiAction.Confirm))
        {
            if (string.IsNullOrWhiteSpace(characterCreation.CharacterName))
            {
                characterCreation.CharacterName = characterNameBeforeEdit;
                screenStatus = "A character name is required.";
            }
            else screenStatus = $"Character name set to {characterCreation.CharacterName}.";
            characterNameEditing = false;
            return true;
        }

        bool shift = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);
        foreach (Keys key in input.NewlyPressedKeys)
        {
            if (key == Keys.Back)
            {
                if (characterCreation.CharacterName.Length > 0)
                    characterCreation.CharacterName = characterCreation.CharacterName[..^1];
                continue;
            }
            char? character = KeyToCharacter(key, shift);
            if (character.HasValue)
                characterCreation.CharacterName += character.Value;
        }

        if (input.PointerPressed && phase20Ui is not null)
        {
            UiHitRegion? hit = phase20Ui.HitTest(input.Pointer);
            if (hit?.Kind != UiControlKind.TextBox)
            {
                if (string.IsNullOrWhiteSpace(characterCreation.CharacterName))
                    characterCreation.CharacterName = characterNameBeforeEdit;
                characterNameEditing = false;
                return false;
            }
        }
        return input.NewlyPressedKeys.Count > 0;
    }

    private static char? KeyToCharacter(Keys key, bool shift)
    {
        int keyValue = (int)key;
        if (keyValue >= (int)Keys.A && keyValue <= (int)Keys.Z)
        {
            char value = (char)('a' + (keyValue - (int)Keys.A));
            return shift ? char.ToUpperInvariant(value) : value;
        }
        if (keyValue >= (int)Keys.D0 && keyValue <= (int)Keys.D9)
        {
            int digit = keyValue - (int)Keys.D0;
            if (!shift) return (char)('0' + digit);
            const string shifted = ")!@#$%^&*(";
            return shifted[digit];
        }
        if (keyValue >= (int)Keys.NumPad0 && keyValue <= (int)Keys.NumPad9) return (char)('0' + (keyValue - (int)Keys.NumPad0));
        return key switch
        {
            Keys.Space => ' ',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPeriod => '.',
            Keys.OemComma => ',',
            Keys.OemQuotes => '\'',
            _ => null
        };
    }

    private bool TryGetSelectedStableId(out StableId itemId)
    {
        itemId = StableId.Empty;
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return false;
        return StableId.TryParse(model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id, out itemId);
    }

    private bool TryGetSelectedEquipmentSlot(out EquipmentSlot slot)
    {
        slot = equipmentSelectedSlot;
        if (screen != Phase20ScreenKind.Equipment) return false;
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0) return false;
        return Enum.TryParse(model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id, out slot);
    }

    private void ReturnEquipmentToSlots()
    {
        EquipmentSlot slot = equipmentSelectedSlot;
        equipmentView = EquipmentViewMode.Slots;
        IReadOnlyList<EquipmentSlotUiModel>? slots = items?.BuildEquipmentSlots();
        int selection = 0;
        if (slots is not null)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].Slot != slot) continue;
                selection = index;
                break;
            }
        }
        screenSelection = selection;
        uiState.Set(Phase20ScreenKind.Equipment, new UiScreenState { SelectedIndex = selection });
        screenStatus = $"Equipment slot: {slot}.";
        phase20Ui?.InvalidateHitMap();
    }

    private void HandleEquipmentAction(UiAction action, ulong tick)
    {
        if (items is null) return;
        if (action == UiAction.EquipmentBack)
        {
            ReturnEquipmentToSlots();
            return;
        }

        if (equipmentView == EquipmentViewMode.Slots)
        {
            if (!TryGetSelectedEquipmentSlot(out EquipmentSlot slot))
            {
                screenStatus = "Select an equipment slot first.";
                return;
            }
            equipmentSelectedSlot = slot;
            StableId current = items.GetEquippedItem(slot);
            switch (action)
            {
                case UiAction.EquipmentChange:
                    equipmentView = EquipmentViewMode.CompatibleItems;
                    screenSelection = 0;
                    uiState.Reset(Phase20ScreenKind.Equipment);
                    screenStatus = $"Compatible items for {slot}.";
                    break;
                case UiAction.EquipmentUnequip:
                    _ = items.UnequipEquipment(slot, tick);
                    screenStatus = items.LastStatus;
                    combat?.RefreshFromItemWorld();
                    break;
                case UiAction.EquipmentRepair:
                    if (current.IsEmpty) { screenStatus = "Nothing is equipped in that slot."; return; }
                    _ = items.RepairWithAvailableMaterial(current, tick);
                    screenStatus = items.LastStatus;
                    combat?.RefreshFromItemWorld();
                    break;
                case UiAction.EquipmentInspect:
                    if (current.IsEmpty) { screenStatus = "Nothing is equipped in that slot."; return; }
                    inspectingItemId = current;
                    uiState.Reset(Phase20ScreenKind.ItemInspection);
                    OpenChildScreen(Phase20ScreenKind.ItemInspection);
                    break;
            }
            return;
        }

        Phase20ScreenModel model = BuildCurrentScreenModel();
        StableId selectedItem = StableId.Empty;
        if (model.Entries.Count > 0)
            _ = StableId.TryParse(model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id, out selectedItem);
        StableId equipped = items.GetEquippedItem(equipmentSelectedSlot);
        switch (action)
        {
            case UiAction.EquipmentEquip:
                if (selectedItem.IsEmpty) { screenStatus = "Select a compatible item first."; return; }
                _ = items.EquipInventoryItemToSlot(selectedItem, equipmentSelectedSlot, tick);
                screenStatus = items.LastStatus;
                screenSelection = 0;
                uiState.Reset(Phase20ScreenKind.Equipment);
                combat?.RefreshFromItemWorld();
                break;
            case UiAction.EquipmentRepair:
                if (equipped.IsEmpty) { screenStatus = "Nothing is equipped in that slot."; return; }
                _ = items.RepairWithAvailableMaterial(equipped, tick);
                screenStatus = items.LastStatus;
                combat?.RefreshFromItemWorld();
                break;
            case UiAction.EquipmentInspect:
                if (selectedItem.IsEmpty) { screenStatus = "Select an item first."; return; }
                inspectingItemId = selectedItem;
                uiState.Reset(Phase20ScreenKind.ItemInspection);
                OpenChildScreen(Phase20ScreenKind.ItemInspection);
                break;
        }
    }

    private void HandleCraftingAction(UiAction action, ulong tick)
    {
        if (items is null) return;
        if (action == UiAction.CraftingToggleUnavailable)
        {
            craftingHideUnavailable = !craftingHideUnavailable;
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Crafting);
            screenStatus = craftingHideUnavailable ? "Unavailable recipes hidden." : "Unavailable recipes shown.";
            return;
        }

        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (model.Entries.Count == 0)
        {
            screenStatus = "No recipe is selected.";
            return;
        }
        string recipeId = model.Entries[Math.Clamp(screenSelection, 0, model.Entries.Count - 1)].Id;
        if (action == UiAction.CraftingToggleFavorite)
        {
            bool added = craftingFavorites.Add(recipeId);
            if (!added) craftingFavorites.Remove(recipeId);
            screenStatus = added ? "Recipe added to favorites." : "Recipe removed from favorites.";
            return;
        }

        int requested = action switch
        {
            UiAction.CraftingCraftFive => 5,
            UiAction.CraftingCraftAll => 0,
            _ => 1
        };
        CraftingResult result = items.CraftKnownRecipeMultiple(recipeId, requested, tick);
        screenStatus = result.Message;
        EmitUiAudio(result.Success ? AudioEventKind.InterfaceCraft : AudioEventKind.InterfaceError);
        combat?.RefreshFromItemWorld();
        if (craftingHideUnavailable && items.GetMaximumCraftCount(recipeId) <= 0)
        {
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Crafting);
        }
    }

    private void HandleInventoryAction(UiAction action, ulong tick)
    {
        if (items is null) return;
        if (action == UiAction.InventorySort)
        {
            inventorySort = Cycle(inventorySort, 1);
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.Inventory);
            screenStatus = $"Inventory sorted by {inventorySort}.";
            return;
        }
        if (!TryGetSelectedStableId(out StableId itemId))
        {
            screenStatus = "Select an inventory item first.";
            return;
        }

        switch (action)
        {
            case UiAction.InventoryUse:
                _ = items.ActivateInventoryItem(itemId);
                screenStatus = items.LastStatus;
                combat?.RefreshFromItemWorld();
                break;
            case UiAction.InventoryEquip:
                _ = items.EquipInventoryItem(itemId, tick);
                screenStatus = items.LastStatus;
                combat?.RefreshFromItemWorld();
                break;
            case UiAction.InventoryRepair:
                _ = items.RepairWithAvailableMaterial(itemId, tick);
                screenStatus = items.LastStatus;
                combat?.RefreshFromItemWorld();
                break;
            case UiAction.InventorySplit:
                _ = items.SplitInventoryStackHalf(itemId, tick);
                screenStatus = items.LastStatus;
                break;
            case UiAction.InventoryDrop:
                if (items.KindFor(itemId) == ItemKind.Quest)
                {
                    screenStatus = "Quest items cannot be dropped.";
                    return;
                }
                dropItemAfterConfirmation = itemId;
                OpenConfirmation("Drop Item", $"Drop {items.DisplayNameFor(itemId)} from your carried inventory?");
                break;
            case UiAction.InventoryInspect:
                inspectingItemId = itemId;
                uiState.Reset(Phase20ScreenKind.ItemInspection);
                OpenChildScreen(Phase20ScreenKind.ItemInspection);
                break;
        }
    }

    private void HandleContainerAction(UiAction action)
    {
        if (player is null) return;
        if (action == UiAction.ContainerTakeAll)
        {
            int moved = player.TransferAllOpenContainerItems();
            snapshot = player.BuildPresentationSnapshot();
            screenSelection = 0;
            uiState.Reset(Phase20ScreenKind.ContainerLoot);
            screenStatus = moved > 0 ? $"Took {moved} item{(moved == 1 ? string.Empty : "s")}." : "Container is empty.";
            return;
        }
        if (action != UiAction.ContainerTake) return;
        if (!TryGetSelectedStableId(out StableId itemId))
        {
            screenStatus = "Select an item to take.";
            return;
        }
        bool taken = player.TransferOpenContainerItem(itemId);
        snapshot = player.BuildPresentationSnapshot();
        screenSelection = Math.Min(screenSelection, Math.Max(0, snapshot.ContainerItems.Count - 1));
        uiState.Set(Phase20ScreenKind.ContainerLoot, new UiScreenState { SelectedIndex = screenSelection });
        screenStatus = taken ? "Item taken." : "That item is no longer available.";
    }

    private void ApplySliderFromPointer(int index, int pointerX, Rectangle bounds)
    {
        float normalized = bounds.Width <= 1 ? 0f : Math.Clamp((pointerX - bounds.Left) / (float)bounds.Width, 0f, 1f);
        Phase20ScreenModel model = BuildCurrentScreenModel();
        if (index < 0 || index >= model.Entries.Count) return;
        string id = model.Entries[index].Id;

        if (screen == Phase20ScreenKind.Settings)
        {
            if (settingsSection == Phase20SettingsSection.Controls)
            {
                Phase20GameplaySettings gameplay = userSettings.Gameplay;
                if (id == "mouse") gameplay = gameplay with { MouseSensitivity = 0.02f + normalized * 0.98f };
                if (id == "gamepad") gameplay = gameplay with { GamepadLookSensitivity = 0.2f + normalized * 7.8f };
                userSettings = userSettings with { Gameplay = gameplay.Validate() };
                ApplyCameraSettings();
                PersistSettings("Control sensitivity applied.");
                return;
            }
            if (settingsSection == Phase20SettingsSection.Video && id == "fov")
            {
                userSettings = userSettings with { Gameplay = userSettings.Gameplay with { FieldOfViewDegrees = 55f + normalized * 55f } };
                ApplyCameraSettings();
                PersistSettings("Field of view applied.");
                return;
            }
            if (settingsSection == Phase20SettingsSection.Audio)
            {
                int value = (int)MathF.Round(normalized * 1000f);
                Phase20AudioInterfaceSettings audio = userSettings.Audio;
                audio = id switch
                {
                    "audio-master" => audio with { MasterPermille = value },
                    "audio-music" => audio with { MusicPermille = value },
                    "audio-interface" => audio with { InterfacePermille = value },
                    "audio-weapons" => audio with { WeaponsPermille = value },
                    "audio-creatures" => audio with { CreaturesPermille = value },
                    "audio-foley" => audio with { FoleyPermille = value },
                    "audio-ambience" => audio with { AmbiencePermille = value },
                    "audio-focus" => audio with { FocusPermille = value },
                    _ => audio
                };
                userSettings = userSettings with { Audio = audio.Validate() };
                PersistSettings("Audio volume applied.");
                RestartAudioForSettings();
                return;
            }
        }

        if (screen == Phase20ScreenKind.Accessibility)
        {
            Phase20AccessibilitySettings value = userSettings.Accessibility;
            value = id switch
            {
                "ui-scale" => value with { InterfaceScale = 0.75f + normalized * 1.25f },
                "subtitle-scale" => value with { SubtitleScale = 0.75f + normalized * 1.25f },
                "focus-intensity" => value with { FocusOverlayIntensityPermille = (int)MathF.Round(normalized * 1000f) },
                "focus-flash" => value with { FocusFlashIntensityPermille = (int)MathF.Round(normalized * 1000f) },
                _ => value
            };
            userSettings = userSettings with { Accessibility = value.Validate() };
            focus?.ApplyAccessibility(userSettings.Accessibility);
            ApplyCameraSettings();
            PersistSettings("Accessibility slider applied.");
        }
    }

    private void ResetCurrentSettingsSection()
    {
        if (screen == Phase20ScreenKind.Accessibility || settingsSection == Phase20SettingsSection.Accessibility)
        {
            userSettings = userSettings with { Accessibility = new Phase20AccessibilitySettings() };
            focus?.ApplyAccessibility(userSettings.Accessibility);
            ApplyCameraSettings();
            PersistSettings("Accessibility settings reset to defaults.");
            return;
        }

        Phase20GameplaySettings gameplayDefaults = new();
        switch (settingsSection)
        {
            case Phase20SettingsSection.Gameplay:
                userSettings = userSettings with
                {
                    Gameplay = userSettings.Gameplay with
                    {
                        Difficulty = gameplayDefaults.Difficulty,
                        TutorialsEnabled = gameplayDefaults.TutorialsEnabled,
                        AutosavesEnabled = gameplayDefaults.AutosavesEnabled,
                        AutosaveSlots = gameplayDefaults.AutosaveSlots,
                        AutosaveIntervalMinutes = gameplayDefaults.AutosaveIntervalMinutes
                    }
                };
                break;
            case Phase20SettingsSection.Controls:
                Phase20AccessibilitySettings accessDefaults = new();
                userSettings = userSettings with
                {
                    Gameplay = userSettings.Gameplay with
                    {
                        MouseSensitivity = gameplayDefaults.MouseSensitivity,
                        GamepadLookSensitivity = gameplayDefaults.GamepadLookSensitivity,
                        InvertVerticalLook = gameplayDefaults.InvertVerticalLook
                    },
                    Accessibility = userSettings.Accessibility with
                    {
                        AimBehavior = accessDefaults.AimBehavior,
                        CrouchBehavior = accessDefaults.CrouchBehavior,
                        SprintBehavior = accessDefaults.SprintBehavior
                    }
                };
                ApplyCameraSettings();
                break;
            case Phase20SettingsSection.Video:
                userSettings = userSettings with
                {
                    Video = new Phase20VideoSettings(),
                    Gameplay = userSettings.Gameplay with { FieldOfViewDegrees = gameplayDefaults.FieldOfViewDegrees }
                };
                ApplyVideoSettings();
                ApplyCameraSettings();
                break;
            case Phase20SettingsSection.Audio:
                userSettings = userSettings with { Audio = new Phase20AudioInterfaceSettings() };
                RestartAudioForSettings();
                break;
        }
        PersistSettings($"{settingsSection} settings reset to defaults.");
    }

    private void CycleResolution(int direction)
    {
        int current = Array.FindIndex(ResolutionPresets, value => value.Width == userSettings.Video.Width && value.Height == userSettings.Video.Height);
        if (current < 0) current = Array.FindIndex(ResolutionPresets, value => value.Width >= userSettings.Video.Width && value.Height >= userSettings.Video.Height);
        if (current < 0) current = 0;
        int next = (current + Math.Sign(direction) + ResolutionPresets.Length) % ResolutionPresets.Length;
        (int width, int height) = ResolutionPresets[next];
        userSettings = userSettings with { Video = userSettings.Video with { Width = width, Height = height } };
        ApplyVideoSettings();
        PersistSettings($"Resolution set to {width} × {height}.");
    }

    private void ApplyVideoSettings()
    {
        Phase20VideoSettings video = userSettings.Video.Validate();
        graphics.PreferredBackBufferWidth = video.Width;
        graphics.PreferredBackBufferHeight = video.Height;
        graphics.IsFullScreen = video.Fullscreen;
        graphics.SynchronizeWithVerticalRetrace = video.VSync;
        try
        {
            graphics.ApplyChanges();
        }
        catch (Exception exception)
        {
            logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Video setting apply failed: {exception.Message}");
            graphics.IsFullScreen = false;
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;
            graphics.ApplyChanges();
            userSettings = userSettings with { Video = video with { Fullscreen = false, Width = 1280, Height = 720 } };
        }
    }

    private void BeginBindingCapture(string id)
    {
        if (!Enum.TryParse(id, out UiAction action)) return;
        InputBinding original = userSettings.Bindings.TryGetValue(action, out InputBinding? configured)
            ? configured
            : InputBindingDefaults.Create()[action];
        bindingCapture = new UiBindingCapture(action, original);
        screenStatus = $"Listening for a new input for {action}. Press Cancel to abort.";
    }

    private void HandleBindingCaptureInput(Phase20InputFrame input)
    {
        if (bindingCapture is null) return;
        if (input.IsPressed(UiAction.Cancel))
        {
            UiAction action = bindingCapture.Action;
            bindingCapture = null;
            screenStatus = $"Binding capture for {action} cancelled.";
            return;
        }

        UiPhysicalInput? physical = input.FirstPhysicalInput();
        if (!physical.HasValue) return;
        UiBindingCapture capture = bindingCapture;
        if (capture.OriginalBinding.Contains(physical.Value))
        {
            bindingCapture = null;
            screenStatus = $"Binding for {capture.Action} is unchanged.";
            return;
        }
        InputBinding proposed = capture.OriginalBinding.WithPhysical(physical.Value);
        UiAction[] conflicts = userSettings.Bindings
            .Where(value => value.Key != capture.Action && value.Value.Contains(physical.Value))
            .Select(static value => value.Key)
            .OrderBy(static value => value)
            .ToArray();

        if (conflicts.Length == 0)
        {
            var bindings = userSettings.Bindings.ToDictionary(static value => value.Key, static value => value.Value);
            bindings[capture.Action] = proposed;
            bindingCapture = null;
            userSettings = userSettings with { Bindings = bindings };
            PersistSettings($"Binding for {capture.Action} updated to {physical.Value}.");
            return;
        }

        UiPhysicalInput outgoing = capture.OriginalBinding.Preferred(physical.Value.Kind);
        bindingConflict = new UiBindingConflict(capture.Action, physical.Value, outgoing, conflicts, proposed);
        bindingCapture = null;
        uiState.Reset(Phase20ScreenKind.BindingConflict);
        OpenChildScreen(Phase20ScreenKind.BindingConflict);
    }

    private void ActivateBindingConflict(string id)
    {
        if (bindingConflict is null) { ReturnFromChildScreen(); return; }
        UiBindingConflict conflict = bindingConflict;
        if (id == "cancel")
        {
            bindingConflict = null;
            ReturnFromChildScreen();
            screenStatus = "Binding change cancelled.";
            return;
        }

        var bindings = userSettings.Bindings.ToDictionary(static value => value.Key, static value => value.Value);
        bindings[conflict.TargetAction] = conflict.ProposedBinding;
        if (id == "swap" && conflict.ConflictingActions.Count == 1)
        {
            UiAction other = conflict.ConflictingActions[0];
            InputBinding otherBinding = bindings.TryGetValue(other, out InputBinding? current) ? current : InputBindingDefaults.Create()[other];
            InputBinding changed = otherBinding.RemovePhysical(conflict.Incoming);
            if (HasPhysical(conflict.Outgoing)) changed = changed.WithPhysical(conflict.Outgoing);
            bindings[other] = changed;
        }
        else if (id == "replace")
        {
            foreach (UiAction other in conflict.ConflictingActions)
            {
                InputBinding otherBinding = bindings.TryGetValue(other, out InputBinding? current) ? current : InputBindingDefaults.Create()[other];
                bindings[other] = otherBinding.RemovePhysical(conflict.Incoming);
            }
        }
        else return;

        userSettings = userSettings with { Bindings = bindings };
        string status = id == "swap" ? "Bindings swapped." : "Conflicting binding replaced.";
        bindingConflict = null;
        ReturnFromChildScreen();
        PersistSettings(status);
    }

    private static bool HasPhysical(UiPhysicalInput input) => input.Kind switch
    {
        UiBindingInputKind.Keyboard => input.Key != Keys.None,
        UiBindingInputKind.Mouse => input.MouseButton != UiMouseButton.None,
        UiBindingInputKind.Gamepad => input.GamepadButton != Buttons.None,
        _ => false
    };

    private void CycleDifficulty(int direction)
    {
        userSettings = userSettings with { Gameplay = userSettings.Gameplay with { Difficulty = Cycle(userSettings.Gameplay.Difficulty, direction) } };
        PersistSettings("Difficulty updated.");
    }

    private static T Cycle<T>(T value, int direction) where T : struct, Enum
    {
        T[] values = Enum.GetValues<T>();
        int index = Array.IndexOf(values, value);
        return values[(index + Math.Sign(direction) + values.Length) % values.Length];
    }

    private void PersistSettings(string status)
    {
        userSettings = userSettings.Validate();
        settingsStore?.Save(userSettings);
        screenStatus = status;
    }

    private void ApplyCameraSettings()
    {
        camera = new FirstPersonCameraRig(new FirstPersonCameraSettings(
            FieldOfViewDegrees: userSettings.Gameplay.FieldOfViewDegrees,
            HeadBobIntensity: userSettings.Accessibility.DisableHeadBob ? 0f : 0.35f,
            LandingMotionIntensity: userSettings.Accessibility.ReduceMotion ? 0f : 0.25f));
        gameplayInput.MouseSensitivity = userSettings.Gameplay.MouseSensitivity;
        gameplayInput.StickSensitivity = userSettings.Gameplay.GamepadLookSensitivity;
        gameplayInput.InvertVerticalLook = userSettings.Gameplay.InvertVerticalLook;
        gameplayInput.CrouchBehavior = userSettings.Accessibility.CrouchBehavior == ToggleBehavior.Toggle ? BindingBehavior.Toggle : BindingBehavior.Hold;
        gameplayInput.SprintBehavior = userSettings.Accessibility.SprintBehavior == ToggleBehavior.Toggle ? BindingBehavior.Toggle : BindingBehavior.Hold;
    }

    private void OpenScreen(Phase20ScreenKind kind)
    {
        if (screen != Phase20ScreenKind.None)
        {
            UiScreenState outgoing = uiState.Get(screen) with { SelectedIndex = screenSelection };
            uiState.Set(screen, outgoing);
        }
        screen = kind;
        lastUiHoverId = null;
        UiScreenState incoming = uiState.Get(kind);
        screenSelection = incoming.SelectedIndex;
        screenStatus = string.Empty;
        mouseCaptured = kind == Phase20ScreenKind.None;
        IsMouseVisible = kind != Phase20ScreenKind.None;
        phase20Ui?.InvalidateHitMap();
    }

    private void OpenChildScreen(Phase20ScreenKind kind)
    {
        if (screen != Phase20ScreenKind.None) screenHistory.Push(screen);
        OpenScreen(kind);
    }

    private void ReturnFromChildScreen()
    {
        Phase20ScreenKind target = screenHistory.Count > 0
            ? screenHistory.Pop()
            : sessionReady ? Phase20ScreenKind.PauseMenu : Phase20ScreenKind.MainMenu;
        OpenScreen(target);
    }

    private void OpenConfirmation(string title, string body)
    {
        if (screen != Phase20ScreenKind.None)
        {
            uiState.Set(screen, uiState.Get(screen) with { SelectedIndex = screenSelection });
            screenHistory.Push(screen);
        }
        screen = Phase20ScreenKind.Confirmation;
        screenSelection = 1;
        uiState.Set(Phase20ScreenKind.Confirmation, new UiScreenState { SelectedIndex = 1 });
        screenStatus = $"{title}|{body}";
        mouseCaptured = false;
        phase20Ui?.InvalidateHitMap();
    }

    private void CloseScreenToGameplay()
    {
        if (screen != Phase20ScreenKind.None) uiState.Set(screen, uiState.Get(screen) with { SelectedIndex = screenSelection });
        screen = Phase20ScreenKind.None;
        lastUiHoverId = null;
        screenHistory.Clear();
        screenSelection = 0;
        mouseCaptured = true;
        IsMouseVisible = false;
        phase20Ui?.InvalidateHitMap();
    }

    private Phase20ScreenModel BuildCurrentScreenModel()
    {
        bool hasSave = saveSlots?.List().Any(static value => value.IsValid) == true;
        Phase20ScreenModel model = screen switch
        {
            Phase20ScreenKind.MainMenu => Phase20ScreenFactory.MainMenu(localization, screenSelection, hasSave, screenStatus),
            Phase20ScreenKind.NewCampaign or Phase20ScreenKind.CharacterAttributes or Phase20ScreenKind.CharacterSkills or Phase20ScreenKind.CharacterBackground or Phase20ScreenKind.CharacterTraits or Phase20ScreenKind.CharacterReview => Phase20ScreenFactory.NewCampaign(screen, characterCreation, pendingCampaignSeed, userSettings.Gameplay.Difficulty, screenSelection, screenStatus, characterNameEditing),
            Phase20ScreenKind.PauseMenu => Phase20ScreenFactory.PauseMenu(screenSelection, screenStatus),
            Phase20ScreenKind.Inventory when items is not null => Phase20ScreenFactory.Inventory(items, screenSelection, inventoryCategory, inventorySort, screenStatus),
            Phase20ScreenKind.ItemInspection when items is not null && !inspectingItemId.IsEmpty => Phase20ScreenFactory.ItemInspection(items, inspectingItemId, screenStatus),
            Phase20ScreenKind.ContainerLoot when snapshot is not null => Phase20ScreenFactory.ContainerLoot(snapshot, screenSelection, screenStatus),
            Phase20ScreenKind.Equipment when items is not null => Phase20ScreenFactory.Equipment(items, screenSelection, equipmentView, equipmentSelectedSlot, screenStatus),
            Phase20ScreenKind.Character when progression is not null && player is not null => Phase20ScreenFactory.Character(progression, player.Player.Id, screenSelection, screenStatus),
            Phase20ScreenKind.Skills when progression is not null && player is not null => Phase20ScreenFactory.Skills(progression, player.Player.Id, screenSelection, skillCategory, screenStatus),
            Phase20ScreenKind.Perks when progression is not null && player is not null => Phase20ScreenFactory.Perks(progression, player.Player.Id, screenSelection, perkFilter, screenStatus),
            Phase20ScreenKind.Map => BuildSpatialMapScreen(),
            Phase20ScreenKind.Journal when narrative is not null && campaign is not null => Phase20ScreenFactory.Journal(narrative, campaign, screenSelection, journalFilter, trackedQuestId, JournalDisplayName, QuestHasMapTarget, screenStatus),
            Phase20ScreenKind.Factions when society is not null && player is not null => Phase20ScreenFactory.Factions(society, player.Player.Id, screenSelection, factionSection, screenStatus),
            Phase20ScreenKind.Crafting when items is not null => Phase20ScreenFactory.Crafting(items, screenSelection, craftingCategory, craftingHideUnavailable, craftingFavorites, screenStatus),
            Phase20ScreenKind.Companion when combat is not null => Phase20ScreenFactory.Companion(combat, screenSelection, companionSection, screenStatus),
            Phase20ScreenKind.Settings => Phase20ScreenFactory.Settings(userSettings, screenSelection, settingsSection, screenStatus),
            Phase20ScreenKind.Accessibility => Phase20ScreenFactory.Accessibility(userSettings, screenSelection, screenStatus),
            Phase20ScreenKind.InputBindings => Phase20ScreenFactory.InputBindings(userSettings, screenSelection, screenStatus, bindingCapture?.Action),
            Phase20ScreenKind.BindingConflict when bindingConflict is not null => Phase20ScreenFactory.BindingConflict(bindingConflict, screenSelection, screenStatus),
            Phase20ScreenKind.SaveGame or Phase20ScreenKind.LoadGame => Phase20ScreenFactory.SaveSlots(screen, saveSlots?.List() ?? [], screenSelection, screenStatus),
            Phase20ScreenKind.Tutorials => Phase20ScreenFactory.Tutorials(screenSelection, screenStatus),
            Phase20ScreenKind.Credits => Phase20ScreenFactory.Credits(localization, screenStatus),
            Phase20ScreenKind.Dialogue => BuildDialogueScreen(),
            Phase20ScreenKind.Merchant => BuildMerchantScreen(),
            Phase20ScreenKind.Ending => BuildEndingScreen(),
            Phase20ScreenKind.Epilogue => BuildEpilogueScreen(),
            Phase20ScreenKind.Confirmation => BuildConfirmationScreen(),
            _ => new Phase20ScreenModel(Phase20ScreenKind.Error, "Interface Error", "The requested screen is unavailable.", [new("close", "Return")], 0, [], 0, null, string.Empty, screenStatus)
        };
        return ApplyUiFoundation(model);
    }

    private Phase20ScreenModel ApplyUiFoundation(Phase20ScreenModel model)
    {
        if (model.Kind == Phase20ScreenKind.Merchant)
        {
            UiScreenState merchantState = uiState.Get(screen, model.Entries.Count);
            return UiUxFinalization.Normalize(model with
            {
                DetailScrollOffset = merchantState.DetailScrollOffset,
                FocusedControlId = merchantState.FocusedControlId,
                ActionPrompts = model.ActionPrompts ?? BuildActionPrompts(model)
            });
        }

        int count = model.Entries.Count;
        UiScreenState state = uiState.Get(screen, count) with { SelectedIndex = count == 0 ? 0 : Math.Clamp(screenSelection, 0, count - 1), SelectedTab = model.SelectedTab };
        uiState.Set(screen, state);
        screenSelection = state.SelectedIndex;
        return UiUxFinalization.Normalize(model with
        {
            SelectedIndex = state.SelectedIndex,
            ScrollOffset = state.ScrollOffset,
            DetailScrollOffset = state.DetailScrollOffset,
            FocusedControlId = state.FocusedControlId,
            ActionPrompts = model.ActionPrompts ?? BuildActionPrompts(model)
        });
    }

    private IReadOnlyList<Phase20ActionPrompt> BuildActionPrompts(Phase20ScreenModel model)
    {
        bool hasEntry = model.Entries.Count > 0;
        return model.Kind switch
        {
            Phase20ScreenKind.MainMenu => [new(UiAction.Confirm, "Select", hasEntry)],
            Phase20ScreenKind.NewCampaign => [new(UiAction.Confirm, "Select", hasEntry), new(UiAction.NavigateLeft, "Previous Value"), new(UiAction.NavigateRight, "Next Value"), new(UiAction.Cancel, "Back")],
            Phase20ScreenKind.CharacterAttributes => [new(UiAction.NavigateLeft, "Decrease"), new(UiAction.NavigateRight, "Increase"), new(UiAction.Confirm, "Select", hasEntry), new(UiAction.Cancel, "Back")],
            Phase20ScreenKind.CharacterSkills or Phase20ScreenKind.CharacterBackground or Phase20ScreenKind.CharacterTraits or Phase20ScreenKind.CharacterReview => [new(UiAction.Confirm, "Select", hasEntry), new(UiAction.Cancel, "Back")],
            Phase20ScreenKind.Inventory => [new(UiAction.Confirm, "Use/Equip", hasEntry), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Equipment => [new(UiAction.EquipmentChange, "Change", hasEntry, ShowBinding: false), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Skills => [new(UiAction.SkillIncrease, "Increase", hasEntry, ShowBinding: false), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Perks => [new(UiAction.PerkAcquire, "Acquire", hasEntry, ShowBinding: false), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Map => [new(UiAction.Confirm, "Track", hasEntry), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Journal => [new(UiAction.Confirm, "Track", hasEntry), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Factions or Phase20ScreenKind.Character => [new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Crafting => [new(UiAction.CraftingCraftOne, "Craft 1", hasEntry, ShowBinding: false), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Companion => [new(UiAction.Confirm, "Issue Order", hasEntry), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Dialogue => [new(UiAction.Confirm, "Choose", hasEntry), new(UiAction.Cancel, "Leave")],
            Phase20ScreenKind.Merchant => [new(UiAction.Confirm, "Trade", hasEntry), new(UiAction.NavigateLeft, "Sell"), new(UiAction.NavigateRight, "Buy"), new(UiAction.Cancel, "Close")],
            Phase20ScreenKind.Settings or Phase20ScreenKind.Accessibility => [new(UiAction.NavigateLeft, "Decrease"), new(UiAction.NavigateRight, "Increase"), new(UiAction.Confirm, "Select", hasEntry), new(UiAction.Cancel, "Back")],
            Phase20ScreenKind.InputBindings => [new(UiAction.Confirm, bindingCapture is null ? "Remap" : "Listening", hasEntry), new(UiAction.Delete, "Reset All"), new(UiAction.Cancel, "Back")],
            Phase20ScreenKind.BindingConflict => [new(UiAction.Confirm, "Select", hasEntry), new(UiAction.Cancel, "Cancel")],
            Phase20ScreenKind.LoadGame => [new(UiAction.Confirm, "Load", hasEntry), new(UiAction.Delete, "Delete", hasEntry, true), new(UiAction.Cancel, "Back")],
            Phase20ScreenKind.SaveGame => [new(UiAction.Confirm, "Save", hasEntry), new(UiAction.Delete, "Delete", hasEntry, true), new(UiAction.Cancel, "Back")],
            Phase20ScreenKind.PauseMenu or Phase20ScreenKind.Tutorials or Phase20ScreenKind.Credits or Phase20ScreenKind.Ending or Phase20ScreenKind.Epilogue or Phase20ScreenKind.Confirmation => [new(UiAction.Confirm, "Select", hasEntry), new(UiAction.Cancel, "Back")],
            _ => [new(UiAction.Cancel, "Back")]
        };
    }

    private Phase20ScreenModel BuildDialogueScreen()
    {
        DialogueRenderedNode? node = narrative?.CurrentDialogue;
        if (node is null)
            return new Phase20ScreenModel(Phase20ScreenKind.Dialogue, "Dialogue", "No conversation is active", [new("close", "Close")], 0, [], 0, null, string.Empty, screenStatus, true, true, ActionPrompts: [new(UiAction.Cancel, "Close")], Presentation: Phase20ScreenPresentation.DialogueConversation);

        EnsureDialogueHistory(node);
        Phase20MenuEntry[] entries = node.Choices.Select((choice, index) =>
        {
            string? skill = choice.HasSkillCheck ? SocietyUiText.Humanize(choice.SkillLabel ?? "Skill") : null;
            string? value = choice.HasSkillCheck ? $"{skill} • Difficulty {choice.Difficulty}" : null;
            string? description = choice.Enabled
                ? (choice.HasSkillCheck ? $"Skill check: {skill}, difficulty {choice.Difficulty}." : $"Press {index + 1} to choose this response.")
                : choice.DisabledReason;
            return new Phase20MenuEntry(choice.Id, $"{index + 1}. {choice.Text}", value, description, choice.Enabled, false, choice.HasSkillCheck ? "CHECK" : null);
        }).ToArray();

        bool canChoose = entries.Length > 0 && entries[Math.Clamp(screenSelection, 0, entries.Length - 1)].Enabled;
        return new Phase20ScreenModel(Phase20ScreenKind.Dialogue, node.SpeakerName, node.Text, entries, screenSelection, [], 0,
            new Phase20Panel("Conversation History", dialogueHistory.Count == 0 ? new[] { "No prior lines in this conversation." } : dialogueHistory.ToArray()), string.Empty, screenStatus, true, true,
            ActionPrompts: [new(UiAction.Confirm, "Choose", canChoose), new(UiAction.Cancel, "Leave")],
            Presentation: Phase20ScreenPresentation.DialogueConversation);
    }

    private Phase20ScreenModel BuildMerchantScreen()
    {
        if (items is null) return new Phase20ScreenModel(Phase20ScreenKind.Merchant, "Merchant", "Unavailable", [], 0, [], 0, null, string.Empty, screenStatus);
        return Phase20ScreenFactory.Merchant(items, merchantStockSelection, merchantPlayerSelection, merchantStockScroll, merchantPlayerScroll, merchantPane, screenStatus);
    }

    private Phase20ScreenModel BuildEndingScreen()
    {
        string ending = campaign is null ? "unresolved" : campaign.Campaign.SelectedEndingId.ToString();
        return new Phase20ScreenModel(Phase20ScreenKind.Ending, "Campaign Ending", ending, [new("continue", "View Epilogue")], 0, [], 0, new Phase20Panel("Final State", ["The campaign director has reached a valid convergence.", $"Resolution: {ending}"]), "ENTER CONTINUE", screenStatus, true, true);
    }

    private Phase20ScreenModel BuildEpilogueScreen()
    {
        string[] lines = society?.World.Settlements.OrderBy(static value => value.Id).Select(value => $"{value.Plan.DisplayName}: population {value.Population.TotalPopulation}, stability {value.StabilityPermille / 10.0:F1}%").ToArray() ?? [];
        return new Phase20ScreenModel(Phase20ScreenKind.Epilogue, "World-State Epilogue", "Consequences reconstructed from the final deterministic world state", [new("main", "Return to Main Menu")], 0, [], 0, new Phase20Panel("Settlements", lines), "ENTER RETURN", screenStatus, true, true);
    }

    private Phase20ScreenModel BuildConfirmationScreen()
    {
        string[] parts = screenStatus.Split('|', 2);
        string title = parts.Length > 0 ? parts[0] : "Confirm";
        string body = parts.Length > 1 ? parts[1] : "Confirm this action?";
        return Phase20ScreenFactory.Confirmation(title, body, screenSelection, string.Empty);
    }

    private bool CanReachActor(StableId id)
    {
        if(scene is null||player is null||combat is null)return false;
        var handle=scene.CharacterHandles.FirstOrDefault(h=>h.State.Id==id);
        if(handle is null||!combat.Runtime.State.TryGetActor(id,out var actor)||actor is null||actor.IsDead)return false;
        FixedVector3 eye=player.Player.FeetPosition+new FixedVector3(0,1500,0);
        FixedVector3 delta=handle.State.FeetPosition+new FixedVector3(0,1400,0)-eye;
        double distance=Math.Sqrt((double)delta.XMillimeters*delta.XMillimeters+(double)delta.YMillimeters*delta.YMillimeters+(double)delta.ZMillimeters*delta.ZMillimeters);
        if(distance>3500)return false;
        return !new PlayerCollisionWorld(scene.TerrainCollision,scene.StructureCollision,scene.DoorCollision,scene.CharacterCollision).Raycast(eye,delta,Math.Max(1,(int)distance-100),CollisionQueryMask.All,out _);
    }
    private bool NearWorkshop()
    {
        if(scene is null||player is null)return false;
        return scene.Structures.Any(s=>s.Grammar.Purpose is Roentgen.World.Structures.BuildingPurpose.Clinic or Roentgen.World.Structures.BuildingPurpose.Workshop &&
            s.Footprint.Contains((int)(player.Player.FeetPosition.XMillimeters/10)-s.WorldOrigin.XCentimeters,(int)(player.Player.FeetPosition.ZMillimeters/10)-s.WorldOrigin.ZCentimeters));
    }
    private bool IsRegisteredLocation(StableId id)
    {
        if(scene is null||id.IsEmpty)return false;
        if(id==Phase16Content.ProspectClinicId||id==Phase16Content.MeridianRelayId||id==Phase16Content.QuarantineDepotId)return true;
        return scene.Topology.Reservations.Any(p=>p.Id==id)||scene.Structures.Any(s=>s.Id==id||s.Rooms.Any(r=>r.Id==id));
    }
    private void HandleDirectActorInteraction(ulong tick)
    {
        if(player is null||narrative is null)return;
        StableId target=player.LastActivatedTargetId;if(target.IsEmpty)return;
        if(target==Phase16Content.ProspectCaretakerId)
        {
            EnsureConversation(tick);
            if(narrative.CurrentDialogue is not null)OpenScreen(Phase20ScreenKind.Dialogue);
        }
        else if(target==Phase16Content.ProspectDeputyId)
        {
            OpenScreen(Phase20ScreenKind.Merchant);
        }
    }

    private void UpdatePhysicalQuestFacts(ulong tick)
    {
        if(player is null||narrative is null)return;
        if(player.Objects.Any(o=>o.Id==Phase16Content.ShipmentManifestId&&o.OwnerId==player.Player.Id))
            Fact("quest.evidence.discovered",Phase16Content.ShipmentManifestId);
        if(player.LastActivatedTargetId==Phase16Content.MeridianRelayId)
            Fact("quest.problem.resolved",Phase16Content.MeridianRelayId);
        void Fact(string predicate,StableId subject)
        {
            var id=StableIdFactory.Create("physical-world-fact",predicate+subject);
            if(narrative.World.Facts.Any(f=>f.Id==id))return;
            narrative.World.UpsertFact(new FactRecord(id,predicate,subject,NarrativeValue.FromBoolean(true),FactTruthState.True,FactScope.Quest,subject,tick,null));
        }
    }

    private void EnsureConversation(ulong tick)
    {
        if (narrative is null || player is null || narrative.CurrentDialogue is not null || !CanReachActor(Phase16Content.ProspectCaretakerId)) return;
        QuestInstanceState? quest = narrative.World.Quests.OrderBy(static value => value.Id).FirstOrDefault();
        if (quest is not null)
        {
            DialogueRenderedNode? started = narrative.StartConversation(quest.Id, [player.Player.Id, Phase16Content.ProspectCaretakerId], BuildNarrativeQuery(tick), BuildDialoguePresentation());
            if (started is not null)
            {
                dialogueHistory.Clear();
                dialogueHistorySessionId = StableId.Empty;
                EnsureDialogueHistory(started);
            }
        }
    }

    private NarrativeEffectReceipt? HandleOptionalNarrativeEffect(NarrativeExternalEffectRequest request)
    {
        if (progression is null || items is null) return null;
        switch (request.Kind)
        {
            case NarrativeEffectKind.GrantExperience:
                ExperienceGrantResult experience = progression.Commands.GrantExperience(request.ResponsibleActorId, new ExperienceAward(request.Id, "quest", request.QuestId.ToString(), Math.Max(0, request.Amount), ExperienceRepeatPolicy.Once, snapshot?.Tick ?? 0));
                return new NarrativeEffectReceipt(request.Id, experience.Accepted, experience.Message, progression.Repository.GetRequired(request.ResponsibleActorId).Revision);
            case NarrativeEffectKind.GrantRecipe:
                string recipeId = !string.IsNullOrWhiteSpace(request.Argument) ? request.Argument : request.Value.TokenValue ?? string.Empty;
                if (!items.Content.Recipes.ContainsKey(recipeId)) return new NarrativeEffectReceipt(request.Id, false, $"Unknown recipe '{recipeId}'.", 0);
                items.RecipeKnowledge.Learn(new RecipeKnowledgeRecord(recipeId, RecipeKnowledgeSource.Quest, request.QuestId.ToString(), snapshot?.Tick ?? 0));
                return new NarrativeEffectReceipt(request.Id, true, "Recipe learned.", items.World.Revision);
            default:
                return null;
        }
    }

    private INarrativeQueryContext BuildNarrativeQuery(ulong tick)
    {
        if (narrative is null || progression is null || player is null || items is null || combat is null || society is null) throw new InvalidOperationException("Narrative runtime is unavailable.");
        StableId playerId = player.Player.Id;
        return new BasicNarrativeQueryContext(
            narrative.World,
            tick,
            actorAlive: actor => combat.Runtime.State.TryGetActor(actor, out CombatActorState? state) ? state is not null && !state.IsDead : actor == playerId,
            itemExists: item => items.World.TryGetItem(item, out ItemInstanceState? value) && value is not null,
            actorHasItem: (actor, item) => items.World.TryGetItem(item, out ItemInstanceState? value) && value is not null && value.Location.HolderId == actor,
            locationReachable: location => IsRegisteredLocation(location),
            serviceOperating: service => society.World.Settlements.SelectMany(static value => value.Services).Any(value => value.Id == service && value.Operating),
            skill: (actor, skill) => actor == playerId ? progression.Capabilities.GetEffectiveSkill(actor, skill) : 40,
            behaviorTag: (actor, tag) => actor == playerId && progression.Capabilities.Get(actor).BehaviorTags.Contains(tag));
    }

    private IDialoguePresentationContext BuildDialoguePresentation()
    {
        var names = new Dictionary<StableId, string>
        {
            [player?.Player.Id ?? StableId.Empty] = "You",
            [Phase16Content.ProspectCaretakerId] = "Caretaker Vey",
            [Phase16Content.ProspectDeputyId] = "Deputy Hale",
            [Phase16Content.ProspectClinicId] = "Prospect Clinic",
            [Phase16Content.BlackwaterFactionId] = "Blackwater Recovery Authority",
            [Phase16Content.RedQuarryFactionId] = "Red Quarry Cooperative",
            [Phase16Content.MedicineResourceId] = "the medicine shipment",
            [Phase16Content.MeridianRelayId] = "the Meridian diagnostic relay",
            [Phase16Content.QuarantineDepotId] = "the quarantine depot",
            [Phase16Content.ShipmentManifestId] = "the Pale Manifest"
        };
        var profiles = new Dictionary<StableId, DialogueProfile>
        {
            [Phase16Content.ProspectCaretakerId] = new DialogueProfile(Phase16Content.ProspectCaretakerId, "Caretaker Vey", "clinical", 750, 650, 800, new Dictionary<string, string> { ["DIAGNOSIS"] = "diagnosis" }, new HashSet<string>())
        };
        return new BasicDialoguePresentationContext(profiles, names);
    }

    private Phase20SaveGame CaptureSave(string slotId, SaveSlotKind kind, string displayName)
    {
        if (player is null || progression is null || combat is null || items is null || society is null || narrative is null || campaign is null) throw new InvalidOperationException("No campaign session is active.");
        ulong tick = snapshot?.Tick ?? player.Tick;
        CampaignSnapshotV17 campaignSnapshot = CampaignSnapshotV17Serializer.Create(
            campaignSeed,
            tick,
            CampaignGenerator.CurrentGeneratorVersion,
            campaign.Campaign.Plan.Fingerprint.ToString(),
            items.Save(),
            society.Save(),
            narrative.Save(),
            campaign.Campaign);
        byte[] campaignPayload = CampaignSnapshotV17Serializer.WriteToBytes(campaignSnapshot);
        var progressionWriter = new CanonicalWriter();
        CharacterProgressionState character = progression.Repository.GetRequired(player.Player.Id);
        character.WriteCanonical(progressionWriter);
        var combatWriter = new CanonicalWriter();
        combat.Runtime.State.WriteCanonical(combatWriter);
        byte[] musicPayload = musicRuntime is null ? [] : MusicSnapshotV19Serializer.Write(musicRuntime.CaptureSnapshot());
        string location = society.World.Settlements.OrderBy(static value => value.Id).FirstOrDefault()?.Plan.DisplayName ?? "The Wastes";
        byte[] thumbnail = CaptureThumbnailPng();
        var metadata = new SaveGameMetadata(
            slotId,
            kind,
            displayName,
            DateTimeOffset.UtcNow,
            tick,
            campaignSeed.Value,
            checked((long)Math.Round(playedSeconds)),
            location,
            characterCreation.CharacterName,
            character.Level,
            BuildIdentity.AssemblyVersion.ToString(),
            Phase20SaveGameSerializer.FormatVersion,
            HasThumbnail: thumbnail.Length > 0);
        var versions = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["spatial-world"] = "2",
            ["phase20-save"] = Phase20SaveGameSerializer.FormatVersion.ToString(),
            ["campaign"] = CampaignGenerator.CurrentGeneratorVersion.ToString(),
            ["build"] = BuildIdentity.AssemblyVersion.ToString(),
            ["music"] = "19",
            ["soundfont"] = "GeneralUser GS bundled"
        };
        return new Phase20SaveGame(metadata, PlayerSaveState.Capture(player.Player), progressionWriter.ToArray(), player.CapturePhase20State(), combatWriter.ToArray(), campaignPayload, musicPayload, thumbnail, versions);
    }

    private byte[] CaptureThumbnailPng()
    {
        try
        {
            int sourceWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int sourceHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
            if (sourceWidth <= 0 || sourceHeight <= 0) return [];
            var source = new Color[checked(sourceWidth * sourceHeight)];
            GraphicsDevice.GetBackBufferData(source);
            const int targetWidth = 320;
            const int targetHeight = 180;
            var target = new Color[targetWidth * targetHeight];
            for (int y = 0; y < targetHeight; y++)
            {
                int sourceY = Math.Min(sourceHeight - 1, y * sourceHeight / targetHeight);
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Math.Min(sourceWidth - 1, x * sourceWidth / targetWidth);
                    target[y * targetWidth + x] = source[sourceY * sourceWidth + sourceX];
                }
            }
            using var texture = new Texture2D(GraphicsDevice, targetWidth, targetHeight, false, SurfaceFormat.Color);
            texture.SetData(target);
            using var stream = new MemoryStream();
            texture.SaveAsPng(stream, targetWidth, targetHeight);
            return stream.ToArray();
        }
        catch (Exception exception) when (exception is InvalidOperationException or OutOfMemoryException)
        {
            logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Save thumbnail capture failed: {exception.Message}");
            return [];
        }
    }

    private void TryWriteSave(string slotId, SaveSlotKind kind, string displayName)
    {
        try
        {
            saveSlots?.Write(CaptureSave(slotId, kind, displayName));
            screenStatus = localization.Get("status.saved");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            screenStatus = $"Save failed: {exception.Message}";
        }
    }

    private void TryLoadSlot(string slotId)
    {
        if (saveSlots is null) return;
        try
        {
            Phase20SaveGame save = saveSlots.Read(slotId);
            RestoreSave(save);
            screenStatus = save.Metadata.RecoveredFromBackup ? localization.Get("status.backupRecovered") : localization.Get("status.loaded");
            CloseScreenToGameplay();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            screenStatus = $"Load failed: {exception.Message}";
        }
    }

    private void RestoreSave(Phase20SaveGame save)
    {
        if (!save.GeneratorVersions.TryGetValue("spatial-world", out string? worldVersion) || worldVersion != "2")
            throw new NotSupportedException("This save predates the rebuilt world geometry. Start a new campaign; the original save remains unchanged.");
        CampaignSnapshotV17 savedCampaign = CampaignSnapshotV17Serializer.ReadFromBytes(save.CampaignPayload);
        CampaignSeed savedSeed = new(save.Metadata.CampaignSeed);
        StableId expectedPlayer = StableIdFactory.Create("player", $"phase20-{savedSeed.Value:X16}");
        if (save.Player.Id != expectedPlayer) throw new InvalidDataException("The save uses an incompatible player identity.");
        CreateCampaignSession(savedSeed, characterCreation);
        if (player is null || progression is null || combat is null || items is null || society is null || narrative is null || campaign is null) throw new InvalidOperationException("The restored campaign session could not be created.");
        player.RestorePhase20State(save.PlayerWorldPayload);
        CharacterProgressionState restoredProgression = CharacterProgressionState.ReadCanonical(save.ProgressionPayload);
        if (restoredProgression.CharacterId != player.Player.Id) throw new InvalidDataException("Progression identity does not match the restored campaign.");
        progression.Repository.Replace(restoredProgression);
        items.Load(savedCampaign.ItemSnapshot);
        society.Load(savedCampaign.SocietySnapshot, items.World);
        narrative.Load(savedCampaign.NarrativeSnapshot);
        campaign.LoadCampaignState(savedCampaign.CampaignSnapshot);
        combat.Runtime.Restore(save.CombatPayload);
        combat.RefreshFromItemWorld();
        if (musicRuntime is not null && save.MusicPayload.Length > 0) musicRuntime.RestoreSnapshot(MusicSnapshotV19Serializer.Read(save.MusicPayload));
        snapshot = player.Step(default);
        focus = new Phase20FocusRuntime(combat, progression, player.Player.Id, savedSeed, userSettings.Accessibility);
        playedSeconds = save.Metadata.PlayTimeSeconds;
        autosaveSeconds = 0;
        endingPresented = campaign.Campaign.Completed;
    }

    private void RestartAudioForSettings()
    {
        if (player is null) return;
        MusicSnapshotV19? musicSnapshot = musicRuntime?.CaptureSnapshot();
        audioHost?.Dispose();
        if (audioHost is null) audioRuntime?.Dispose();
        musicRuntime?.Dispose();
        audioHost = null;
        audioRuntime = null;
        audioBridge = null;
        musicRuntime = null;
        musicBridge = null;
        InitializeAudio(player.Player.Id);
        musicBridge = new Phase19MusicBridge(combat?.CompanionId ?? StableId.Empty);
        if (musicSnapshot is not null && musicRuntime is not null)
        {
            try { musicRuntime.RestoreSnapshot(musicSnapshot); }
            catch (Exception exception) { logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Music state could not be restored after changing volume: {exception.Message}"); }
        }
    }

    private void InitializeAudio(StableId playerId)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "config", "roentgen.settings.json");
        try { audioSettings = File.Exists(path) ? AudioSettingsLoader.LoadFromFile(path) : new AudioSettings(); }
        catch (Exception exception) { logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Audio settings could not be loaded; defaults will be used: {exception.Message}"); audioSettings = new AudioSettings(); }
        try { musicSettings = File.Exists(path) ? MusicSettingsLoader.LoadFromFile(path) : new MusicSettings(); }
        catch (Exception exception) { logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Music settings could not be loaded; defaults will be used: {exception.Message}"); musicSettings = new MusicSettings(); }
        Phase20AudioInterfaceSettings userAudio = userSettings.Audio.Validate();
        audioSettings = audioSettings with
        {
            MasterVolume = userAudio.Master,
            MusicVolume = userAudio.Music,
            InterfaceVolume = userAudio.Interface,
            WeaponsVolume = userAudio.Weapons,
            CreaturesVolume = userAudio.Creatures,
            FoleyVolume = userAudio.Foley,
            AmbienceVolume = userAudio.Ambience,
            FocusVolume = userAudio.Focus
        };

        if (!audioSettings.Enabled)
        {
            var silent = new SilentAudioRuntime(audioSettings); silent.Start(); audioRuntime = silent;
            audioBridge = new Phase18AudioBridge(playerId);
            musicRuntime = new SilentMusicRuntime(); musicRuntime.Start();
            return;
        }
        PlayromaAudioRuntime? runtime = null;
        PlayromaMonoGameHost? host = null;
        try
        {
            runtime = new PlayromaAudioRuntime(campaignSeed.Value, audioSettings);

            // Adaptive music is optional to the sound-effects device.  Build Fix 18 wrapped
            // both systems in one try/catch, so a SoundFont/catalog/music failure silently
            // disabled footsteps, weapons, UI, doors and ambience as collateral damage.
            if (musicSettings.Enabled)
            {
                try
                {
                    var music = new AdaptiveMusicRuntime(campaignSeed.Value, musicSettings, AppContext.BaseDirectory);
                    runtime.AttachMusicSource(music);
                    musicRuntime = music;
                }
                catch (Exception musicException)
                {
                    logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Adaptive music initialization failed; Playroma sound will continue: {musicException.Message}");
                    musicRuntime = new SilentMusicRuntime();
                    musicRuntime.Start();
                }
            }
            else
            {
                musicRuntime = new SilentMusicRuntime();
                musicRuntime.Start();
            }

            host = new PlayromaMonoGameHost(runtime, audioSettings);
            host.Start();
            logger.Write(LogLevel.Information, nameof(RoentgenGame), $"Playroma sound started at {runtime.SampleRate} Hz / {runtime.Channels} channel(s); PCM host is running.");
            audioRuntime = runtime;
            audioHost = host;
            audioBridge = new Phase18AudioBridge(playerId);

            if (snapshot is not null && combat is not null)
            {
                try
                {
                    CameraState state = camera.Build(snapshot.Player, 1f, GraphicsDevice.Viewport.AspectRatio);
                    audioBridge.Initialize(runtime, snapshot, combat.Presentation, state, player);
                }
                catch (Exception bridgeException)
                {
                    // Listener/region projection must not tear down an already healthy PCM host.
                    logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Audio presentation initialization was partially skipped; Playroma sound remains active: {bridgeException.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            host?.Dispose();
            if (host is null) runtime?.Dispose();
            musicRuntime?.Dispose();
            logger.Write(LogLevel.Warning, nameof(RoentgenGame), $"Playroma sound-device initialization failed; continuing silently: {exception.Message}");
            var silent = new SilentAudioRuntime(audioSettings); silent.Start(); audioRuntime = silent;
            audioHost = null;
            audioBridge = new Phase18AudioBridge(playerId);
            musicRuntime = new SilentMusicRuntime(); musicRuntime.Start();
        }
    }

    private void UpdateAudio(bool interfaceOpen, bool focusActive)
    {
        if (audioRuntime is null || audioBridge is null || snapshot is null || combat is null) return;
        CameraState state = camera.Build(snapshot.Player, 1f, GraphicsDevice.Viewport.AspectRatio);
        audioBridge.Update(audioRuntime, snapshot, combat.Presentation, state, interfaceOpen, player);
        if (musicRuntime is not null && musicBridge is not null && campaign is not null)
        {
            bool paused = interfaceOpen && userSettings.Accessibility.PauseInMenus;
            if (paused && musicRuntime.State == MusicRuntimeState.Running) musicRuntime.Pause();
            else if (!paused && musicRuntime.State == MusicRuntimeState.Paused) musicRuntime.Resume();
            musicBridge.Update(musicRuntime, snapshot, combat.Presentation, campaign, screen == Phase20ScreenKind.Dialogue, focusActive);
            musicRuntime.PumpControl();
        }
        if (audioSettings.MuteWhenUnfocused && !IsActive) audioRuntime.ApplyMixSnapshot(AudioMixSnapshotKind.Muted);
        if (audioHost is not null) audioHost.Pump();
        else audioRuntime.PumpControl();
    }

    private static CampaignSeed CreateRandomSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        ulong value = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        return new CampaignSeed(value == 0 ? 1UL : value);
    }

    // Compatibility markers retained for the cumulative phase contract validators.
    private Phase13OverlayModel? BuildItemOverlay() => null;
    private void HandleItemInterface(KeyboardState keyboard, ulong tick) { _ = keyboard; _ = tick; }
    private void DebugCommitCampaignChoice(ulong tick) { _ = tick; }
    private void DebugResolveConvergence(ulong tick) { _ = tick; }

    protected override void UnloadContent()
    {
        DisposeCampaignSession();
        phase20Ui?.Dispose();
        hud?.Dispose();
        viewWeapon?.Dispose();
        renderer?.Dispose();
        phase20Ui = null;
        hud = null;
        renderer = null;
        base.UnloadContent();
    }
}
