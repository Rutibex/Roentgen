using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Entities;

public readonly record struct DamageComponent(DamageType Type, int Amount)
{
    public DamageComponent Clamped() => new(Type, Math.Max(0, Amount));
}

public sealed class BodyPartRuntimeState : ICanonicalWritable
{
    public BodyPartRuntimeState(CharacterTargetPart definition, int maximumIntegrity)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (maximumIntegrity <= 0) throw new ArgumentOutOfRangeException(nameof(maximumIntegrity));
        Definition = definition;
        MaximumIntegrity = maximumIntegrity;
        CurrentIntegrity = maximumIntegrity;
    }

    public CharacterTargetPart Definition { get; }
    public int MaximumIntegrity { get; }
    public int CurrentIntegrity { get; set; }
    public BodyPartFunctionalState FunctionalState { get; set; }
    public int TraumaPermille { get; set; }
    public ulong Revision { get; set; }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Definition.Id);
        writer.WriteString(Definition.Name);
        writer.WriteInt32((int)Definition.Region);
        writer.WriteString(Definition.BoneName);
        writer.WriteInt32(Definition.RadiusMillimeters);
        writer.WriteInt32(Definition.LengthMillimeters);
        writer.WriteInt32(Definition.ConsequenceWeightPermille);
        writer.WriteInt32(MaximumIntegrity);
        writer.WriteInt32(CurrentIntegrity);
        writer.WriteInt32((int)FunctionalState);
        writer.WriteInt32(TraumaPermille);
        writer.WriteUInt64(Revision);
    }

    public static BodyPartRuntimeState ReadCanonical(CanonicalReader reader)
    {
        var definition = new CharacterTargetPart(
            reader.ReadStableId(),
            reader.ReadRequiredString(),
            (AnatomicalRegion)reader.ReadInt32(),
            reader.ReadRequiredString(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());
        int maximum = reader.ReadInt32();
        var result = new BodyPartRuntimeState(definition, maximum)
        {
            CurrentIntegrity = reader.ReadInt32(),
            FunctionalState = (BodyPartFunctionalState)reader.ReadInt32(),
            TraumaPermille = reader.ReadInt32(),
            Revision = reader.ReadUInt64()
        };
        return result;
    }
}

public sealed class ArmorRuntimeState : ICanonicalWritable
{
    private readonly SortedSet<AnatomicalRegion> _coverage;
    private readonly SortedDictionary<DamageType, int> _thresholds;
    private readonly SortedDictionary<DamageType, int> _resistances;

    public ArmorRuntimeState(
        StableId id,
        string profileId,
        ArmorLayerKind layer,
        IEnumerable<AnatomicalRegion> coverage,
        IEnumerable<KeyValuePair<DamageType, int>> thresholds,
        IEnumerable<KeyValuePair<DamageType, int>> resistances,
        int conditionPermille = 1000)
    {
        if (id.IsEmpty) throw new ArgumentException("Armor ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        Id = id;
        ProfileId = profileId;
        Layer = layer;
        _coverage = new SortedSet<AnatomicalRegion>(coverage ?? throw new ArgumentNullException(nameof(coverage)));
        _thresholds = [];
        foreach ((DamageType type, int value) in thresholds ?? throw new ArgumentNullException(nameof(thresholds)))
        {
            _thresholds[type] = Math.Max(0, value);
        }
        _resistances = [];
        foreach ((DamageType type, int value) in resistances ?? throw new ArgumentNullException(nameof(resistances)))
        {
            _resistances[type] = Math.Clamp(value, 0, 950);
        }
        ConditionPermille = Math.Clamp(conditionPermille, 0, 1000);
    }

    public StableId Id { get; }
    public string ProfileId { get; }
    public ArmorLayerKind Layer { get; }
    public IReadOnlyCollection<AnatomicalRegion> Coverage => _coverage;
    public int ConditionPermille { get; set; }
    public ulong Revision { get; set; }

    public bool Covers(AnatomicalRegion region) => _coverage.Contains(region);
    public int Threshold(DamageType type) => _thresholds.GetValueOrDefault(type);
    public int ResistancePermille(DamageType type) => Math.Clamp(_resistances.GetValueOrDefault(type), 0, 950);

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(ProfileId);
        writer.WriteInt32((int)Layer);
        writer.WriteCount(_coverage.Count);
        foreach (AnatomicalRegion region in _coverage) writer.WriteInt32((int)region);
        writer.WriteCount(_thresholds.Count);
        foreach ((DamageType type, int value) in _thresholds)
        {
            writer.WriteInt32((int)type);
            writer.WriteInt32(value);
        }
        writer.WriteCount(_resistances.Count);
        foreach ((DamageType type, int value) in _resistances)
        {
            writer.WriteInt32((int)type);
            writer.WriteInt32(value);
        }
        writer.WriteInt32(ConditionPermille);
        writer.WriteUInt64(Revision);
    }

    public static ArmorRuntimeState ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        string profileId = reader.ReadRequiredString();
        ArmorLayerKind layer = (ArmorLayerKind)reader.ReadInt32();
        int coverageCount = reader.ReadCount();
        var coverage = new List<AnatomicalRegion>(coverageCount);
        for (int index = 0; index < coverageCount; index++) coverage.Add((AnatomicalRegion)reader.ReadInt32());
        int thresholdCount = reader.ReadCount();
        var thresholds = new List<KeyValuePair<DamageType, int>>(thresholdCount);
        for (int index = 0; index < thresholdCount; index++) thresholds.Add(new((DamageType)reader.ReadInt32(), reader.ReadInt32()));
        int resistanceCount = reader.ReadCount();
        var resistances = new List<KeyValuePair<DamageType, int>>(resistanceCount);
        for (int index = 0; index < resistanceCount; index++) resistances.Add(new((DamageType)reader.ReadInt32(), reader.ReadInt32()));
        return new ArmorRuntimeState(id, profileId, layer, coverage, thresholds, resistances, reader.ReadInt32())
        {
            Revision = reader.ReadUInt64()
        };
    }
}

public sealed class WeaponRuntimeState : ICanonicalWritable
{
    public WeaponRuntimeState(StableId id, string definitionId, StableId ownerId, int magazineCapacity, int ammunition)
    {
        if (id.IsEmpty) throw new ArgumentException("Weapon ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        if (magazineCapacity < 0) throw new ArgumentOutOfRangeException(nameof(magazineCapacity));
        Id = id;
        DefinitionId = definitionId;
        OwnerId = ownerId;
        MagazineCapacity = magazineCapacity;
        Ammunition = Math.Clamp(ammunition, 0, magazineCapacity);
    }

    public StableId Id { get; }
    public string DefinitionId { get; }
    public StableId OwnerId { get; set; }
    public int MagazineCapacity { get; }
    public int Ammunition { get; set; }
    public int ReserveAmmunition { get; set; }
    public int ConditionPermille { get; set; } = 1000;
    public int HeatPermille { get; set; }
    public int RecoilPermille { get; set; }
    public ulong NextReadyTick { get; set; }
    public bool IsJammed { get; set; }
    public ulong AttackSequence { get; set; }
    public ulong Revision { get; set; }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(DefinitionId);
        writer.WriteStableId(OwnerId);
        writer.WriteInt32(MagazineCapacity);
        writer.WriteInt32(Ammunition);
        writer.WriteInt32(ReserveAmmunition);
        writer.WriteInt32(ConditionPermille);
        writer.WriteInt32(HeatPermille);
        writer.WriteInt32(RecoilPermille);
        writer.WriteUInt64(NextReadyTick);
        writer.WriteBoolean(IsJammed);
        writer.WriteUInt64(AttackSequence);
        writer.WriteUInt64(Revision);
    }

    public static WeaponRuntimeState ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        string definitionId = reader.ReadRequiredString();
        StableId ownerId = reader.ReadStableId();
        int capacity = reader.ReadInt32();
        var result = new WeaponRuntimeState(id, definitionId, ownerId, capacity, reader.ReadInt32())
        {
            ReserveAmmunition = reader.ReadInt32(),
            ConditionPermille = reader.ReadInt32(),
            HeatPermille = reader.ReadInt32(),
            RecoilPermille = reader.ReadInt32(),
            NextReadyTick = reader.ReadUInt64(),
            IsJammed = reader.ReadBoolean(),
            AttackSequence = reader.ReadUInt64(),
            Revision = reader.ReadUInt64()
        };
        return result;
    }
}

public sealed record CombatStatusState(CombatStatusKind Kind, int SeverityPermille, ulong ExpiresTick, ulong SourceTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)Kind);
        writer.WriteInt32(SeverityPermille);
        writer.WriteUInt64(ExpiresTick);
        writer.WriteUInt64(SourceTick);
    }

    public static CombatStatusState ReadCanonical(CanonicalReader reader) => new(
        (CombatStatusKind)reader.ReadInt32(),
        reader.ReadInt32(),
        reader.ReadUInt64(),
        reader.ReadUInt64());
}

public sealed class CombatActorState : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, BodyPartRuntimeState> _bodyParts = [];
    private readonly SortedDictionary<StableId, ArmorRuntimeState> _armor = [];
    private readonly SortedDictionary<CombatStatusKind, CombatStatusState> _statuses = [];

    public CombatActorState(CharacterState presentation, CombatActorRole role, StableId factionId, int maximumHealth)
    {
        Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        if (factionId.IsEmpty) throw new ArgumentException("Faction ID cannot be empty.", nameof(factionId));
        if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        Role = role;
        FactionId = factionId;
        MaximumHealth = maximumHealth;
        Health = maximumHealth;
        StaminaPermille = 1000;
        MoralePermille = 1000;
    }

    public CombatActorState(CharacterState presentation, CombatActorRole role, string factionId, int maximumHealth)
        : this(presentation, role, StableIdFactory.Create("faction.legacy", factionId), maximumHealth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factionId);
    }

    public StableId Id => Presentation.Id;
    public CharacterState Presentation { get; }
    public CombatActorRole Role { get; set; }
    public StableId FactionId { get; set; }
    public int MaximumHealth { get; private set; }
    public int Health { get; set; }
    public int StaminaPermille { get; set; }
    public int MoralePermille { get; set; }
    public StableId EquippedWeaponId { get; set; }
    public StableId CurrentTargetId { get; set; }
    public CombatActionKind Action { get; set; }
    public ulong ActionStartedTick { get; set; }
    public ulong ActionImpactTick { get; set; }
    public ulong ActionEndsTick { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsRetreating { get; set; }
    public bool IsDead { get; set; }
    public ulong Revision { get; set; }
    public IReadOnlyCollection<BodyPartRuntimeState> BodyParts => _bodyParts.Values;
    public IReadOnlyCollection<ArmorRuntimeState> Armor => _armor.Values;
    public IReadOnlyCollection<CombatStatusState> Statuses => _statuses.Values;

    public void ApplyMaximumHealth(int newMaximum)
    {
        newMaximum = Math.Max(1, newMaximum);
        int missingHealth = Math.Max(0, MaximumHealth - Health);
        MaximumHealth = newMaximum;
        Health = Math.Clamp(newMaximum - missingHealth, 0, newMaximum);
        IsDead = Health <= 0;
        Revision++;
    }

    public void AddBodyPart(BodyPartRuntimeState part) => _bodyParts[part.Definition.Id] = part;
    public void AddArmor(ArmorRuntimeState armor) => _armor[armor.Id] = armor;
    public void SetStatus(CombatStatusState status) => _statuses[status.Kind] = status;
    public bool RemoveStatus(CombatStatusKind kind) => _statuses.Remove(kind);
    public bool TryGetBodyPart(StableId id, out BodyPartRuntimeState? part) => _bodyParts.TryGetValue(id, out part);
    public BodyPartRuntimeState GetRequiredBodyPart(StableId id) => _bodyParts.TryGetValue(id, out BodyPartRuntimeState? part) ? part : throw new KeyNotFoundException($"Body part '{id}' does not exist on actor '{Id}'.");

    public void WriteCanonical(CanonicalWriter writer)
    {
        Presentation.WriteCanonical(writer);
        writer.WriteInt32((int)Role);
        writer.WriteStableId(FactionId);
        writer.WriteInt32(MaximumHealth);
        writer.WriteInt32(Health);
        writer.WriteInt32(StaminaPermille);
        writer.WriteInt32(MoralePermille);
        writer.WriteStableId(EquippedWeaponId);
        writer.WriteStableId(CurrentTargetId);
        writer.WriteInt32((int)Action);
        writer.WriteUInt64(ActionStartedTick);
        writer.WriteUInt64(ActionImpactTick);
        writer.WriteUInt64(ActionEndsTick);
        writer.WriteBoolean(IsBlocking);
        writer.WriteBoolean(IsRetreating);
        writer.WriteBoolean(IsDead);
        writer.WriteUInt64(Revision);
        writer.WriteCount(_bodyParts.Count);
        foreach (BodyPartRuntimeState part in _bodyParts.Values) part.WriteCanonical(writer);
        writer.WriteCount(_armor.Count);
        foreach (ArmorRuntimeState armor in _armor.Values) armor.WriteCanonical(writer);
        writer.WriteCount(_statuses.Count);
        foreach (CombatStatusState status in _statuses.Values) status.WriteCanonical(writer);
    }

    public static CombatActorState ReadCanonical(CanonicalReader reader)
    {
        CharacterState presentation = ReadCharacter(reader);
        CombatActorRole role = (CombatActorRole)reader.ReadInt32();
        StableId faction = reader.ReadStableId();
        int maximum = reader.ReadInt32();
        var result = new CombatActorState(presentation, role, faction, maximum)
        {
            Health = reader.ReadInt32(),
            StaminaPermille = reader.ReadInt32(),
            MoralePermille = reader.ReadInt32(),
            EquippedWeaponId = reader.ReadStableId(),
            CurrentTargetId = reader.ReadStableId(),
            Action = (CombatActionKind)reader.ReadInt32(),
            ActionStartedTick = reader.ReadUInt64(),
            ActionImpactTick = reader.ReadUInt64(),
            ActionEndsTick = reader.ReadUInt64(),
            IsBlocking = reader.ReadBoolean(),
            IsRetreating = reader.ReadBoolean(),
            IsDead = reader.ReadBoolean(),
            Revision = reader.ReadUInt64()
        };
        int partCount = reader.ReadCount();
        for (int index = 0; index < partCount; index++) result.AddBodyPart(BodyPartRuntimeState.ReadCanonical(reader));
        int armorCount = reader.ReadCount();
        for (int index = 0; index < armorCount; index++) result.AddArmor(ArmorRuntimeState.ReadCanonical(reader));
        int statusCount = reader.ReadCount();
        for (int index = 0; index < statusCount; index++) result.SetStatus(CombatStatusState.ReadCanonical(reader));
        return result;
    }

    private static CharacterState ReadCharacter(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        var appearance = new CharacterAppearanceDescriptor(
            (CharacterSpecies)reader.ReadInt32(),
            reader.ReadRequiredString(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());
        var state = new CharacterState(id, appearance, ReadVector(reader))
        {
            Velocity = ReadVector(reader),
            FacingYawUnits = reader.ReadInt32(),
            Action = (CharacterActionKind)reader.ReadInt32(),
            ActionProgressPermille = reader.ReadInt32(),
            LocomotionPhasePermille = reader.ReadInt32(),
            Attention = (CharacterAttentionKind)reader.ReadInt32(),
            AttentionTargetId = reader.ReadStableId(),
            AttentionTargetPosition = ReadVector(reader),
            EquippedObjectId = reader.ReadStableId(),
            EquipmentPoseProfileId = reader.ReadRequiredString(),
            Lod = (CharacterLodLevel)reader.ReadInt32(),
            Injury = new CharacterInjuryPresentation(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
            Revision = reader.ReadUInt64()
        };
        return state;
    }

    public static FixedVector3 ReadVector(CanonicalReader reader) => new(reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64());
    public static void WriteVector(CanonicalWriter writer, FixedVector3 value)
    {
        writer.WriteInt64(value.XMillimeters);
        writer.WriteInt64(value.YMillimeters);
        writer.WriteInt64(value.ZMillimeters);
    }
}

public sealed class ProjectileState : ICanonicalWritable
{
    public ProjectileState(StableId id, StableId ownerId, StableId weaponId, string definitionId, FixedVector3 position, FixedVector3 velocity)
    {
        if (id.IsEmpty) throw new ArgumentException("Projectile ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        Id = id;
        OwnerId = ownerId;
        WeaponId = weaponId;
        DefinitionId = definitionId;
        Position = position;
        Velocity = velocity;
    }

    public StableId Id { get; }
    public StableId OwnerId { get; }
    public StableId WeaponId { get; }
    public string DefinitionId { get; }
    public FixedVector3 Position { get; set; }
    public FixedVector3 Velocity { get; set; }
    public int RadiusMillimeters { get; set; } = 20;
    public int RemainingTicks { get; set; } = 300;
    public int FuseTicks { get; set; }
    public bool Explosive { get; set; }
    public bool Active { get; set; } = true;
    public ulong Revision { get; set; }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteStableId(OwnerId);
        writer.WriteStableId(WeaponId);
        writer.WriteString(DefinitionId);
        CombatActorState.WriteVector(writer, Position);
        CombatActorState.WriteVector(writer, Velocity);
        writer.WriteInt32(RadiusMillimeters);
        writer.WriteInt32(RemainingTicks);
        writer.WriteInt32(FuseTicks);
        writer.WriteBoolean(Explosive);
        writer.WriteBoolean(Active);
        writer.WriteUInt64(Revision);
    }

    public static ProjectileState ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(),
        reader.ReadStableId(),
        reader.ReadStableId(),
        reader.ReadRequiredString(),
        CombatActorState.ReadVector(reader),
        CombatActorState.ReadVector(reader))
    {
        RadiusMillimeters = reader.ReadInt32(),
        RemainingTicks = reader.ReadInt32(),
        FuseTicks = reader.ReadInt32(),
        Explosive = reader.ReadBoolean(),
        Active = reader.ReadBoolean(),
        Revision = reader.ReadUInt64()
    };
}
