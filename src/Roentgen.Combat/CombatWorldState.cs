using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Combat;

public sealed record PendingCombatAction(
    StableId ActorId,
    StableId WeaponId,
    CombatActionKind Kind,
    FixedVector3 Origin,
    FixedVector3 Direction,
    StableId IntendedTargetId,
    StableId IntendedBodyPartId,
    ulong QueuedTick,
    ulong ImpactTick,
    ulong EndTick,
    ulong Sequence,
    bool ImpactResolved = false) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(ActorId);
        writer.WriteStableId(WeaponId);
        writer.WriteInt32((int)Kind);
        CombatActorState.WriteVector(writer, Origin);
        CombatActorState.WriteVector(writer, Direction);
        writer.WriteStableId(IntendedTargetId);
        writer.WriteStableId(IntendedBodyPartId);
        writer.WriteUInt64(QueuedTick);
        writer.WriteUInt64(ImpactTick);
        writer.WriteUInt64(EndTick);
        writer.WriteUInt64(Sequence);
        writer.WriteBoolean(ImpactResolved);
    }

    public static PendingCombatAction ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(),
        reader.ReadStableId(),
        (CombatActionKind)reader.ReadInt32(),
        CombatActorState.ReadVector(reader),
        CombatActorState.ReadVector(reader),
        reader.ReadStableId(),
        reader.ReadStableId(),
        reader.ReadUInt64(),
        reader.ReadUInt64(),
        reader.ReadUInt64(),
        reader.ReadUInt64(),
        reader.ReadBoolean());
}

public sealed record CombatFeedbackEntry(
    ulong Sequence,
    ulong Tick,
    CombatFeedbackKind Kind,
    StableId ActorId,
    StableId TargetId,
    StableId BodyPartId,
    FixedVector3 Origin,
    FixedVector3 Position,
    int Amount,
    string Message) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteUInt64(Sequence);
        writer.WriteUInt64(Tick);
        writer.WriteInt32((int)Kind);
        writer.WriteStableId(ActorId);
        writer.WriteStableId(TargetId);
        writer.WriteStableId(BodyPartId);
        CombatActorState.WriteVector(writer, Origin);
        CombatActorState.WriteVector(writer, Position);
        writer.WriteInt32(Amount);
        writer.WriteString(Message);
    }

    public static CombatFeedbackEntry ReadCanonical(CanonicalReader reader) => new(
        reader.ReadUInt64(),
        reader.ReadUInt64(),
        (CombatFeedbackKind)reader.ReadInt32(),
        reader.ReadStableId(),
        reader.ReadStableId(),
        reader.ReadStableId(),
        CombatActorState.ReadVector(reader),
        CombatActorState.ReadVector(reader),
        reader.ReadInt32(),
        reader.ReadRequiredString());
}

public sealed class CombatWorldState : ICanonicalWritable
{
    public const int SchemaVersion = 1;
    private readonly SortedDictionary<StableId, CombatActorState> _actors = [];
    private readonly SortedDictionary<StableId, WeaponRuntimeState> _weapons = [];
    private readonly SortedDictionary<StableId, ProjectileState> _projectiles = [];
    private readonly SortedDictionary<StableId, PendingCombatAction> _pendingActions = [];
    private readonly List<CombatFeedbackEntry> _feedback = [];

    public ulong Tick { get; set; }
    public ulong NextFeedbackSequence { get; set; }
    public ulong Revision { get; set; }
    public IReadOnlyCollection<CombatActorState> Actors => _actors.Values;
    public IReadOnlyCollection<WeaponRuntimeState> Weapons => _weapons.Values;
    public IReadOnlyCollection<ProjectileState> Projectiles => _projectiles.Values;
    public IReadOnlyCollection<PendingCombatAction> PendingActions => _pendingActions.Values;
    public IReadOnlyList<CombatFeedbackEntry> Feedback => _feedback;

    public void AddActor(CombatActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!_actors.TryAdd(actor.Id, actor)) throw new InvalidOperationException($"Combat actor '{actor.Id}' already exists.");
        Revision++;
    }

    public void AddWeapon(WeaponRuntimeState weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if (!_weapons.TryAdd(weapon.Id, weapon)) throw new InvalidOperationException($"Combat weapon '{weapon.Id}' already exists.");
        Revision++;
    }

    public void AddProjectile(ProjectileState projectile)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        if (!_projectiles.TryAdd(projectile.Id, projectile)) throw new InvalidOperationException($"Projectile '{projectile.Id}' already exists.");
        Revision++;
    }

    public bool RemoveProjectile(StableId id)
    {
        bool changed = _projectiles.Remove(id);
        if (changed) Revision++;
        return changed;
    }

    public CombatActorState GetActor(StableId id) => _actors.TryGetValue(id, out CombatActorState? actor) ? actor : throw new KeyNotFoundException($"Combat actor '{id}' does not exist.");
    public WeaponRuntimeState GetWeapon(StableId id) => _weapons.TryGetValue(id, out WeaponRuntimeState? weapon) ? weapon : throw new KeyNotFoundException($"Combat weapon '{id}' does not exist.");
    public bool TryGetActor(StableId id, out CombatActorState? actor) => _actors.TryGetValue(id, out actor);
    public bool TryGetWeapon(StableId id, out WeaponRuntimeState? weapon) => _weapons.TryGetValue(id, out weapon);
    public bool TryGetPendingAction(StableId actorId, out PendingCombatAction? action) => _pendingActions.TryGetValue(actorId, out action);
    public void SetPendingAction(PendingCombatAction action) { _pendingActions[action.ActorId] = action; Revision++; }
    public bool RemovePendingAction(StableId actorId) { bool changed = _pendingActions.Remove(actorId); if (changed) Revision++; return changed; }

    public CombatFeedbackEntry AddFeedback(ulong tick, CombatFeedbackKind kind, StableId actorId, StableId targetId, StableId bodyPartId, FixedVector3 origin, FixedVector3 position, int amount, string message)
    {
        var entry = new CombatFeedbackEntry(NextFeedbackSequence++, tick, kind, actorId, targetId, bodyPartId, origin, position, amount, message);
        _feedback.Add(entry);
        if (_feedback.Count > 128) _feedback.RemoveRange(0, _feedback.Count - 128);
        Revision++;
        return entry;
    }

    public StateChecksum ComputeChecksum()
    {
        var writer = new CanonicalWriter();
        WriteCanonical(writer);
        return writer.ComputeChecksum();
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString("roentgen.combat-world");
        writer.WriteInt32(SchemaVersion);
        writer.WriteUInt64(Tick);
        writer.WriteUInt64(NextFeedbackSequence);
        writer.WriteUInt64(Revision);
        writer.WriteCount(_actors.Count);
        foreach (CombatActorState actor in _actors.Values) actor.WriteCanonical(writer);
        writer.WriteCount(_weapons.Count);
        foreach (WeaponRuntimeState weapon in _weapons.Values) weapon.WriteCanonical(writer);
        writer.WriteCount(_projectiles.Count);
        foreach (ProjectileState projectile in _projectiles.Values) projectile.WriteCanonical(writer);
        writer.WriteCount(_pendingActions.Count);
        foreach (PendingCombatAction action in _pendingActions.Values) action.WriteCanonical(writer);
        writer.WriteCount(_feedback.Count);
        foreach (CombatFeedbackEntry entry in _feedback) entry.WriteCanonical(writer);
    }

    public static CombatWorldState ReadCanonical(ReadOnlyMemory<byte> payload)
    {
        var reader = new CanonicalReader(payload);
        string marker = reader.ReadRequiredString();
        if (!string.Equals(marker, "roentgen.combat-world", StringComparison.Ordinal)) throw new InvalidDataException("Combat state marker is invalid.");
        int schema = reader.ReadInt32();
        if (schema != SchemaVersion) throw new NotSupportedException($"Combat state schema {schema} is not supported.");
        var result = new CombatWorldState
        {
            Tick = reader.ReadUInt64(),
            NextFeedbackSequence = reader.ReadUInt64(),
            Revision = reader.ReadUInt64()
        };
        int actorCount = reader.ReadCount();
        for (int index = 0; index < actorCount; index++)
        {
            CombatActorState actor = CombatActorState.ReadCanonical(reader);
            result._actors.Add(actor.Id, actor);
        }
        int weaponCount = reader.ReadCount();
        for (int index = 0; index < weaponCount; index++)
        {
            WeaponRuntimeState weapon = WeaponRuntimeState.ReadCanonical(reader);
            result._weapons.Add(weapon.Id, weapon);
        }
        int projectileCount = reader.ReadCount();
        for (int index = 0; index < projectileCount; index++)
        {
            ProjectileState projectile = ProjectileState.ReadCanonical(reader);
            result._projectiles.Add(projectile.Id, projectile);
        }
        int actionCount = reader.ReadCount();
        for (int index = 0; index < actionCount; index++)
        {
            PendingCombatAction action = PendingCombatAction.ReadCanonical(reader);
            result._pendingActions.Add(action.ActorId, action);
        }
        int feedbackCount = reader.ReadCount();
        for (int index = 0; index < feedbackCount; index++) result._feedback.Add(CombatFeedbackEntry.ReadCanonical(reader));
        reader.EnsureComplete();
        return result;
    }

}
