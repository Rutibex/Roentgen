using Microsoft.Xna.Framework;
using Roentgen.AI;
using Roentgen.Animation;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Generation;
using Roentgen.Physics;
using Roentgen.Rendering;
using Roentgen.Simulation;
using Roentgen.World;
using Roentgen.World.Structures;
using NumericsVector3 = System.Numerics.Vector3;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;

namespace Roentgen.App;

internal sealed class Phase9WorldScene : IDisposable
{
    private readonly Phase8WorldScene _world;
    private readonly RendererFoundation _renderer;
    private readonly CharacterGpuRegistry _characterGpu = new();
    private readonly CharacterTaskRunner _tasks = new();
    private readonly CharacterPoseRuntime _poseRuntime = new();
    private readonly DynamicCharacterCollisionRegistry _characterCollision = new();
    private readonly List<CharacterEntry> _characters = [];
    private readonly TerrainHeightEvaluator _heightEvaluator;
    private readonly GpuMesh _rifleMesh;
    private readonly GpuMesh _meleeMesh;
    private readonly GpuMesh _toolMesh;
    private readonly MaterialDefinition _equipmentMaterial = new("phase9-equipment", new Color(88, 91, 84), 0.62f, 0.45f);
    private bool _disposed;

    public Phase9WorldScene(RendererFoundation renderer, CampaignSeed? seed = null, WorldGenerationProfile? profile = null)
    {
        _renderer = renderer;
        _world = new Phase8WorldScene(renderer, seed, profile);
        _heightEvaluator = new TerrainHeightEvaluator(_world.Topology);
        _rifleMesh = renderer.UploadNow(ProceduralMeshFactory.CreateBox("phase9-rifle", new Vector3(0.09f, 0.11f, 0.82f), new Color(94, 98, 90)));
        _meleeMesh = renderer.UploadNow(ProceduralMeshFactory.CreateBox("phase9-melee", new Vector3(0.045f, 0.045f, 0.72f), new Color(116, 112, 98)));
        _toolMesh = renderer.UploadNow(ProceduralMeshFactory.CreateBox("phase9-tool", new Vector3(0.09f, 0.07f, 0.34f), new Color(126, 104, 62)));
        BuildRegressionCast();
        RefreshCharacterCollision();
    }

    public RegionalTopology Topology => _world.Topology;
    public IReadOnlyList<StructureBlueprint> Structures => _world.Structures;
    public TerrainStreamingSnapshot StreamingSnapshot => _world.StreamingSnapshot;
    public InteractionWorldDefinition InteractionWorld => _world.InteractionWorld;
    public DynamicDoorCollisionRegistry DoorCollision => _world.DoorCollision;
    public StructureCollisionRegistry StructureCollision => _world.StructureCollision;
    public TerrainCollisionRegistry TerrainCollision => _world.TerrainCollision;
    public DynamicCharacterCollisionRegistry CharacterCollision => _characterCollision;
    public FixedVector3 SpawnPosition => _world.SpawnPosition;
    public int CharacterCount => _characters.Count;
    public IReadOnlyList<Phase9CharacterHandle> CharacterHandles => _characters.Select(static entry => new Phase9CharacterHandle(entry.Product, entry.State, entry.DisplayName)).ToArray();

    public void TakeCombatControl(StableId characterId) => _tasks.Remove(characterId);

    public void Update(FixedVector3 playerPosition, bool runAmbientTasks = true)
    {
        _world.Update(playerPosition);
        if (runAmbientTasks) _tasks.Tick();
        UpdateCharacterLods(playerPosition);
        RefreshCharacterCollision();
    }

    public RenderFrameSnapshot Build(long tick, CameraState camera, bool showDebug, PlayerSimulationRuntime runtime, IReadOnlyList<DebugLine>? combatDebugLines = null)
    {
        RenderFrameSnapshot worldSnapshot = _world.Build(tick, camera, showDebug, runtime);
        var opaqueItems = new List<RenderItem>(worldSnapshot.OpaqueItems);
        var characters = new List<CharacterRenderItem>(_characters.Count);
        foreach (CharacterEntry entry in _characters)
        {
            CharacterState state = entry.State;
            NumericsVector3 velocity = new(
                state.Velocity.XMillimeters / 1000f,
                state.Velocity.YMillimeters / 1000f,
                state.Velocity.ZMillimeters / 1000f);
            NumericsVector3 look = new(
                state.AttentionTargetPosition.XMillimeters - state.FeetPosition.XMillimeters,
                state.AttentionTargetPosition.YMillimeters - state.FeetPosition.YMillimeters,
                state.AttentionTargetPosition.ZMillimeters - state.FeetPosition.ZMillimeters);
            if (look.LengthSquared() < 1f)
            {
                float yaw = state.FacingYawUnits / 10000f;
                look = new NumericsVector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            }
            else
            {
                look = NumericsVector3.Normalize(look);
            }

            (float leftGround, NumericsVector3 leftNormal, float rightGround, NumericsVector3 rightNormal) = SampleFootSupports(state);
            var poseInput = new CharacterPoseInput(
                state.Action,
                state.ActionProgressPermille,
                state.LocomotionPhasePermille,
                velocity,
                state.FacingYawUnits,
                look,
                look,
                state.Injury,
                state.EquipmentPoseProfileId,
                state.Lod,
                leftGround,
                rightGround,
                leftNormal,
                rightNormal);
            PoseBuffer pose = _poseRuntime.Evaluate(entry.Product, poseInput);
            float yawRadians = state.FacingYawUnits / 10000f;
            XnaMatrix world = XnaMatrix.CreateRotationY(yawRadians) * XnaMatrix.CreateTranslation(
                state.FeetPosition.XMillimeters / 1000f,
                state.FeetPosition.YMillimeters / 1000f,
                state.FeetPosition.ZMillimeters / 1000f);
            bool highlighted = runtime.Player.FocusedTargetId == state.Id;
            characters.Add(new CharacterRenderItem(state.Id, entry.Gpu, pose, world, state.Lod, highlighted));
            AppendEquipmentRenderItem(opaqueItems, entry, pose, world);
        }

        IReadOnlyList<DebugLine> interactionHighlight = BuildCharacterInteractionHighlight(runtime.Player.FocusedTargetId);
        IReadOnlyList<DebugLine> debug = showDebug
            ? worldSnapshot.DebugLines.Concat(BuildCharacterDebug()).Concat(combatDebugLines ?? []).Concat(interactionHighlight).ToArray()
            : worldSnapshot.DebugLines.Concat(interactionHighlight).ToArray();
        return new RenderFrameSnapshot(
            worldSnapshot.SimulationTick,
            worldSnapshot.Camera,
            opaqueItems,
            worldSnapshot.Sun,
            worldSnapshot.Fog,
            worldSnapshot.ClearColor,
            debug,
            characters);
    }

    private void BuildRegressionCast()
    {
        CampaignSeed seed = _world.Topology.CampaignSeed;
        LandmarkReservation clinic = _world.Topology.Reservations.First(static value => value.Purpose == LandmarkPurpose.Settlement);
        LandmarkReservation intake = _world.Topology.Reservations.First(static value => value.Purpose == LandmarkPurpose.Ruin);
        LandmarkReservation relay = _world.Topology.Reservations.First(static value => value.Purpose == LandmarkPurpose.Infrastructure);
        AddHumanoid(seed, "clinic-caretaker", "clinic-caretaker", Offset(clinic, -500, 350), BuildCaretakerTasks(clinic));
        AddHumanoid(seed, "clinic-guard", "settlement-guard", Offset(clinic, 650, 600), BuildGuardTasks(clinic));
        AddHumanoid(seed, "clinic-technician", "workshop-technician", Offset(clinic, 900, -450), BuildTechnicianTasks(clinic));
        AddHumanoid(seed, "clinic-resident", "settlement-resident", Offset(clinic, -800, -350), BuildResidentTasks(clinic));
        AddHumanoid(seed, "blackwater-scavenger", "industrial-scavenger", Offset(intake, -500, 200), BuildScavengerTasks(intake));
        AddHumanoid(seed, "blackwater-hostile", "hostile-regression", Offset(intake, 600, -300), BuildHostileTasks(intake), injured: true);
        AddCreature(seed, "blackwater-quadruped", "blackwater-industrial-quadruped", Offset(intake, 1200, 450), BuildCreatureTasks(intake));
        AddHumanoid(seed, "relay-technician", "relay-technician", Offset(relay, -350, 350), BuildRelayTasks(relay));
    }

    private void AddHumanoid(CampaignSeed seed, string key, string archetype, FixedVector3 position, CharacterTaskSequence sequence, bool injured = false)
    {
        StableId id = key == "clinic-caretaker" ? Roentgen.Narrative.Phase16Content.ProspectCaretakerId : key == "clinic-technician" ? Roentgen.Narrative.Phase16Content.ProspectDeputyId : StableIdFactory.Create("phase9-character", key);
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(seed, id, archetype));
        var state = new CharacterState(id, product.Appearance, position)
        {
            EquipmentPoseProfileId = key.Contains("guard", StringComparison.Ordinal) || key.Contains("hostile", StringComparison.Ordinal)
                ? "rifle"
                : key.Contains("technician", StringComparison.Ordinal) ? "tool" : key.Contains("scavenger", StringComparison.Ordinal) ? "melee-one-handed" : "unarmed",
            Injury = injured ? new CharacterInjuryPresentation(0, 250, 150, 0, 500, 0, 350, 200) : CharacterInjuryPresentation.None
        };
        CharacterGpuAsset gpu = _characterGpu.UploadNow(_renderer, product);
        _characters.Add(new CharacterEntry(product, gpu, state, DisplayNameFor(key, archetype)));
        _tasks.Register(state, sequence);
    }

    private void AddCreature(CampaignSeed seed, string key, string archetype, FixedVector3 position, CharacterTaskSequence sequence)
    {
        StableId id = StableIdFactory.Create("phase9-creature", key);
        GeneratedCharacterProduct product = QuadrupedCreatureGenerator.Generate(new CharacterGenerationRequest(seed, id, archetype, CharacterSpecies.IndustrialQuadruped));
        var state = new CharacterState(id, product.Appearance, position);
        CharacterGpuAsset gpu = _characterGpu.UploadNow(_renderer, product);
        _characters.Add(new CharacterEntry(product, gpu, state, DisplayNameFor(key, archetype)));
        _tasks.Register(state, sequence);
    }

    private (float LeftHeight, NumericsVector3 LeftNormal, float RightHeight, NumericsVector3 RightNormal) SampleFootSupports(CharacterState state)
    {
        float yaw = state.FacingYawUnits / 10000f;
        float sideX = MathF.Cos(yaw) * 0.13f;
        float sideZ = -MathF.Sin(yaw) * 0.13f;
        (float leftHeight, NumericsVector3 leftNormal) = SampleSupport(state, -sideX, -sideZ);
        (float rightHeight, NumericsVector3 rightNormal) = SampleSupport(state, sideX, sideZ);
        return (leftHeight, leftNormal, rightHeight, rightNormal);
    }

    private (float HeightOffset, NumericsVector3 Normal) SampleSupport(CharacterState state, float offsetXMeters, float offsetZMeters)
    {
        int x = checked((int)(state.FeetPosition.XMillimeters / 10) + (int)MathF.Round(offsetXMeters * 100f));
        int z = checked((int)(state.FeetPosition.ZMillimeters / 10) + (int)MathF.Round(offsetZMeters * 100f));
        int center = _heightEvaluator.SampleHeightCentimeters(x, z);
        if(_world.StructureCollision.TrySampleSupport(x,(int)(state.FeetPosition.YMillimeters/10),z,35,60,out var support))
            return (support.HeightCentimeters/100f-state.FeetPosition.YMillimeters/1000f,new NumericsVector3((float)support.Normal.X,(float)support.Normal.Y,(float)support.Normal.Z));
        const int sample = 12;
        int left = _heightEvaluator.SampleHeightCentimeters(x - sample, z);
        int right = _heightEvaluator.SampleHeightCentimeters(x + sample, z);
        int back = _heightEvaluator.SampleHeightCentimeters(x, z - sample);
        int forward = _heightEvaluator.SampleHeightCentimeters(x, z + sample);
        var normal = NumericsVector3.Normalize(new NumericsVector3(left - right, sample * 2f, back - forward));
        float actorHeight = state.FeetPosition.YMillimeters / 1000f;
        return (center / 100f - actorHeight, normal);
    }

    private void AppendEquipmentRenderItem(List<RenderItem> items, CharacterEntry entry, PoseBuffer pose, XnaMatrix characterWorld)
    {
        string profile = entry.State.EquipmentPoseProfileId;
        if (string.Equals(profile, "unarmed", StringComparison.Ordinal) || entry.Product.Appearance.Species == CharacterSpecies.IndustrialQuadruped)
        {
            return;
        }

        int hand = entry.Product.Skeleton.Sockets.TryGetValue("weapon_primary", out int socketBone)
            ? socketBone
            : entry.Product.Skeleton.IndexOf("hand_r");
        if (hand < 0)
        {
            return;
        }

        System.Numerics.Matrix4x4[] boneWorld = pose.BuildWorldMatrices();
        XnaMatrix bone = CharacterRenderingBridge.ToXna(boneWorld[hand]);
        GpuMesh mesh;
        XnaMatrix local;
        if (string.Equals(profile, "rifle", StringComparison.Ordinal))
        {
            mesh = _rifleMesh;
            local = XnaMatrix.CreateRotationX(-MathHelper.PiOver2) * XnaMatrix.CreateTranslation(0f, 0f, -0.32f);
        }
        else if (string.Equals(profile, "melee-one-handed", StringComparison.Ordinal))
        {
            mesh = _meleeMesh;
            local = XnaMatrix.CreateRotationX(-MathHelper.PiOver2) * XnaMatrix.CreateTranslation(0f, 0f, -0.3f);
        }
        else
        {
            mesh = _toolMesh;
            local = XnaMatrix.CreateRotationX(-MathHelper.PiOver2) * XnaMatrix.CreateTranslation(0f, 0f, -0.13f);
        }

        items.Add(new RenderItem($"equipment-{entry.State.Id}", mesh, _equipmentMaterial, local * bone * characterWorld, CastsShadow: true, ReceivesShadow: true));
    }

    private FixedVector3 Offset(LandmarkReservation reservation, int xCentimeters, int zCentimeters)
    {
        int x = reservation.Center.XCentimeters + xCentimeters;
        int z = reservation.Center.ZCentimeters + zCentimeters;
        int height = _heightEvaluator.SampleHeightCentimeters(x, z);
        return new FixedVector3((long)x * 10, (long)height * 10, (long)z * 10);
    }

    private CharacterTaskSequence BuildCaretakerTasks(LandmarkReservation reservation) => new(
    [
        MoveTask(Offset(reservation, -500, 350), Offset(reservation, 150, 150), 180),
        new CharacterTask(CharacterTaskKind.UseTerminal, Offset(reservation, 200, 80), StableId.Empty, 150),
        MoveTask(Offset(reservation, 150, 150), Offset(reservation, -500, 350), 180),
        new CharacterTask(CharacterTaskKind.Converse, Offset(reservation, -900, 250), StableId.Empty, 160)
    ]);

    private CharacterTaskSequence BuildGuardTasks(LandmarkReservation reservation) => new(
    [
        MoveTask(Offset(reservation, 650, 600), Offset(reservation, -650, 600), 240),
        new CharacterTask(CharacterTaskKind.Aim, Offset(reservation, 0, 1800), StableId.Empty, 120, "rifle"),
        new CharacterTask(CharacterTaskKind.Reload, FixedVector3.Zero, StableId.Empty, 100, "rifle"),
        MoveTask(Offset(reservation, -650, 600), Offset(reservation, 650, 600), 240)
    ]);

    private CharacterTaskSequence BuildTechnicianTasks(LandmarkReservation reservation) => new(
    [
        new CharacterTask(CharacterTaskKind.RepairObject, Offset(reservation, 1100, -600), StableId.Empty, 220),
        MoveTask(Offset(reservation, 900, -450), Offset(reservation, 500, -250), 120),
        new CharacterTask(CharacterTaskKind.Wait, FixedVector3.Zero, StableId.Empty, 90),
        MoveTask(Offset(reservation, 500, -250), Offset(reservation, 900, -450), 120)
    ]);

    private CharacterTaskSequence BuildResidentTasks(LandmarkReservation reservation) => new(
    [
        new CharacterTask(CharacterTaskKind.Sit, FixedVector3.Zero, StableId.Empty, 220),
        new CharacterTask(CharacterTaskKind.Converse, Offset(reservation, -500, 350), StableId.Empty, 180),
        new CharacterTask(CharacterTaskKind.Wait, FixedVector3.Zero, StableId.Empty, 120)
    ]);

    private CharacterTaskSequence BuildScavengerTasks(LandmarkReservation reservation) => new(
    [
        MoveTask(Offset(reservation, -500, 200), Offset(reservation, 250, -150), 180),
        new CharacterTask(CharacterTaskKind.RepairObject, Offset(reservation, 350, -250), StableId.Empty, 180),
        MoveTask(Offset(reservation, 250, -150), Offset(reservation, -500, 200), 180)
    ]);

    private CharacterTaskSequence BuildHostileTasks(LandmarkReservation reservation) => new(
    [
        new CharacterTask(CharacterTaskKind.Aim, Offset(reservation, -900, 400), StableId.Empty, 150, "rifle"),
        new CharacterTask(CharacterTaskKind.Reload, FixedVector3.Zero, StableId.Empty, 100, "rifle"),
        new CharacterTask(CharacterTaskKind.MeleeDemonstration, Offset(reservation, 200, -100), StableId.Empty, 90, "melee-one-handed"),
        new CharacterTask(CharacterTaskKind.Wait, FixedVector3.Zero, StableId.Empty, 90)
    ]);

    private CharacterTaskSequence BuildCreatureTasks(LandmarkReservation reservation) => new(
    [
        MoveTask(Offset(reservation, 1200, 450), Offset(reservation, 450, 850), 210),
        new CharacterTask(CharacterTaskKind.Wait, FixedVector3.Zero, StableId.Empty, 100),
        MoveTask(Offset(reservation, 450, 850), Offset(reservation, 1200, 450), 210)
    ]);

    private CharacterTaskSequence BuildRelayTasks(LandmarkReservation reservation) => new(
    [
        new CharacterTask(CharacterTaskKind.UseTerminal, Offset(reservation, -200, 150), StableId.Empty, 200),
        MoveTask(Offset(reservation, -350, 350), Offset(reservation, 250, -100), 160),
        new CharacterTask(CharacterTaskKind.Converse, Offset(reservation, 0, 600), StableId.Empty, 140),
        MoveTask(Offset(reservation, 250, -100), Offset(reservation, -350, 350), 160)
    ]);

    private static CharacterTask MoveTask(FixedVector3 from, FixedVector3 to, int durationTicks)
    {
        _ = from;
        return new CharacterTask(CharacterTaskKind.MoveTo, to, StableId.Empty, durationTicks);
    }

    public void RefreshCharacterCollision()
    {
        _characterCollision.Clear();
        foreach (CharacterEntry entry in _characters)
        {
            int radius = entry.Product.Appearance.Species == CharacterSpecies.IndustrialQuadruped ? 500 : 360;
            _characterCollision.Set(entry.State.Id, entry.State.FeetPosition, radius, entry.Product.Appearance.HeightMillimeters);
        }
    }

    public IReadOnlyList<ActorInteractionTarget> BuildActorInteractionTargets()
    {
        return _characters
            .OrderBy(static entry => entry.State.Id)
            .Select(entry => new ActorInteractionTarget(
                entry.State.Id,
                entry.DisplayName,
                entry.State.FeetPosition,
                entry.Product.Appearance.HeightMillimeters,
                entry.Product.Appearance.Species == CharacterSpecies.IndustrialQuadruped ? 500 : 360,
                entry.Product.Appearance.Species == CharacterSpecies.IndustrialQuadruped ? "INSPECT" : "TALK",
                true))
            .ToArray();
    }

    private IReadOnlyList<DebugLine> BuildCharacterInteractionHighlight(StableId focusedId)
    {
        CharacterEntry? entry = _characters.FirstOrDefault(value => value.State.Id == focusedId);
        if (entry is null) return Array.Empty<DebugLine>();
        float radius = entry.Product.Appearance.Species == CharacterSpecies.IndustrialQuadruped ? 0.5f : 0.36f;
        float height = entry.Product.Appearance.HeightMillimeters / 1000f;
        Vector3 feet = new(entry.State.FeetPosition.XMillimeters / 1000f, entry.State.FeetPosition.YMillimeters / 1000f, entry.State.FeetPosition.ZMillimeters / 1000f);
        Vector3 min = feet + new Vector3(-radius, 0.03f, -radius);
        Vector3 max = feet + new Vector3(radius, height + 0.05f, radius);
        Color color = new(244, 224, 112);
        return BoxLines(min, max, color);
    }

    private static IReadOnlyList<DebugLine> BoxLines(Vector3 min, Vector3 max, Color color)
    {
        Vector3 p000=new(min.X,min.Y,min.Z),p001=new(min.X,min.Y,max.Z),p010=new(min.X,max.Y,min.Z),p011=new(min.X,max.Y,max.Z);
        Vector3 p100=new(max.X,min.Y,min.Z),p101=new(max.X,min.Y,max.Z),p110=new(max.X,max.Y,min.Z),p111=new(max.X,max.Y,max.Z);
        return new DebugLine[]{new(p000,p001,color),new(p000,p010,color),new(p000,p100,color),new(p111,p110,color),new(p111,p101,color),new(p111,p011,color),new(p001,p011,color),new(p001,p101,color),new(p010,p011,color),new(p010,p110,color),new(p100,p101,color),new(p100,p110,color)};
    }

    private static string DisplayNameFor(string key, string archetype) => key switch
    {
        "clinic-caretaker" => "Prospect Caretaker",
        "clinic-guard" => "Clinic Guard",
        "clinic-technician" => "Clinic Technician",
        "clinic-resident" => "Clinic Resident",
        "blackwater-scavenger" => "Blackwater Scavenger",
        "blackwater-hostile" => "Blackwater Raider",
        "blackwater-quadruped" => "Industrial Quadruped",
        "relay-technician" => "Relay Technician",
        _ => archetype.Replace('-', ' ')
    };

    private void UpdateCharacterLods(FixedVector3 player)
    {
        foreach (CharacterEntry entry in _characters)
        {
            long dx = entry.State.FeetPosition.XMillimeters - player.XMillimeters;
            long dz = entry.State.FeetPosition.ZMillimeters - player.ZMillimeters;
            double distance = Math.Sqrt((double)dx * dx + (double)dz * dz);
            entry.State.Lod = distance switch
            {
                < 25000 => CharacterLodLevel.Full,
                < 70000 => CharacterLodLevel.Reduced,
                _ => CharacterLodLevel.Distant
            };
        }
    }

    private IReadOnlyList<DebugLine> BuildCharacterDebug()
    {
        var lines = new List<DebugLine>();
        foreach (CharacterEntry entry in _characters)
        {
            Vector3 feet = new(
                entry.State.FeetPosition.XMillimeters / 1000f,
                entry.State.FeetPosition.YMillimeters / 1000f,
                entry.State.FeetPosition.ZMillimeters / 1000f);
            float height = entry.Product.Appearance.HeightMillimeters / 1000f;
            Color color = entry.Product.Appearance.Species == CharacterSpecies.IndustrialQuadruped ? Color.Orange : Color.Cyan;
            lines.Add(new DebugLine(feet, feet + Vector3.Up * height, color));
        }

        return lines;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _toolMesh.Dispose();
        _meleeMesh.Dispose();
        _rifleMesh.Dispose();
        _characterGpu.Dispose();
        _world.Dispose();
    }

    private sealed record CharacterEntry(GeneratedCharacterProduct Product, CharacterGpuAsset Gpu, CharacterState State, string DisplayName);
}

internal sealed record Phase9CharacterHandle(GeneratedCharacterProduct Product, CharacterState State, string DisplayName);
