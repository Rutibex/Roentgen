using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.AI;

public enum CharacterTaskKind
{
    MoveTo = 0,
    Wait = 1,
    FaceTarget = 2,
    Sit = 3,
    Converse = 4,
    UseTerminal = 5,
    RepairObject = 6,
    Equip = 7,
    Aim = 8,
    Reload = 9,
    MeleeDemonstration = 10
}

public sealed record CharacterTask(
    CharacterTaskKind Kind,
    FixedVector3 TargetPosition,
    StableId TargetEntityId,
    int DurationTicks,
    string EquipmentProfileId = "unarmed");

public sealed class CharacterTaskSequence
{
    public CharacterTaskSequence(IEnumerable<CharacterTask> tasks, bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        Tasks = tasks.ToArray();
        if (Tasks.Count == 0)
        {
            throw new ArgumentException("A task sequence must contain at least one task.", nameof(tasks));
        }

        Loop = loop;
    }

    public IReadOnlyList<CharacterTask> Tasks { get; }
    public bool Loop { get; }
}

public sealed class CharacterTaskRunner
{
    private readonly Dictionary<StableId, RuntimeEntry> _entries = [];

    public void Register(CharacterState character, CharacterTaskSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(sequence);
        _entries[character.Id] = new RuntimeEntry(character, sequence);
    }

    public bool Remove(StableId characterId) => _entries.Remove(characterId);

    public void Tick()
    {
        foreach (RuntimeEntry entry in _entries.Values.OrderBy(static value => value.Character.Id))
        {
            Advance(entry);
        }
    }

    private static void Advance(RuntimeEntry entry)
    {
        CharacterTask task = entry.Sequence.Tasks[entry.TaskIndex];
        entry.TaskTick++;
        switch (task.Kind)
        {
            case CharacterTaskKind.MoveTo:
                Move(entry.Character, task.TargetPosition, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.Wait:
                SetAction(entry.Character, CharacterActionKind.Idle, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.FaceTarget:
                Face(entry.Character, task.TargetPosition);
                SetAction(entry.Character, CharacterActionKind.Idle, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.Sit:
                SetAction(entry.Character, CharacterActionKind.Sit, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.Converse:
                entry.Character.Attention = CharacterAttentionKind.Converse;
                entry.Character.AttentionTargetId = task.TargetEntityId;
                entry.Character.AttentionTargetPosition = task.TargetPosition;
                SetAction(entry.Character, CharacterActionKind.Converse, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.UseTerminal:
                Face(entry.Character, task.TargetPosition);
                SetAction(entry.Character, CharacterActionKind.UseTerminal, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.RepairObject:
                Face(entry.Character, task.TargetPosition);
                SetAction(entry.Character, CharacterActionKind.RepairObject, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.Equip:
                entry.Character.EquipmentPoseProfileId = task.EquipmentProfileId;
                SetAction(entry.Character, CharacterActionKind.Idle, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.Aim:
                ApplyEquipmentProfile(entry.Character, task.EquipmentProfileId);
                Face(entry.Character, task.TargetPosition);
                SetAction(entry.Character, CharacterActionKind.Aim, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.Reload:
                ApplyEquipmentProfile(entry.Character, task.EquipmentProfileId);
                SetAction(entry.Character, CharacterActionKind.Reload, entry.TaskTick, task.DurationTicks);
                break;
            case CharacterTaskKind.MeleeDemonstration:
                ApplyEquipmentProfile(entry.Character, task.EquipmentProfileId);
                SetAction(entry.Character, CharacterActionKind.Melee, entry.TaskTick, task.DurationTicks);
                break;
        }

        int duration = Math.Max(1, task.DurationTicks);
        if (entry.TaskTick >= duration)
        {
            entry.TaskTick = 0;
            entry.TaskIndex++;
            if (entry.TaskIndex >= entry.Sequence.Tasks.Count)
            {
                entry.TaskIndex = entry.Sequence.Loop ? 0 : entry.Sequence.Tasks.Count - 1;
            }

            entry.Character.Revision++;
        }
    }

    private static void Move(CharacterState character, FixedVector3 target, int taskTick, int durationTicks)
    {
        int duration = Math.Max(1, durationTicks);
        long remainingTicks = Math.Max(1, duration - taskTick + 1);
        FixedVector3 delta = target - character.FeetPosition;
        long stepX = delta.XMillimeters / remainingTicks;
        long stepY = delta.YMillimeters / remainingTicks;
        long stepZ = delta.ZMillimeters / remainingTicks;
        character.Velocity = new FixedVector3(stepX * 60, stepY * 60, stepZ * 60);
        character.FeetPosition += new FixedVector3(stepX, stepY, stepZ);
        Face(character, target);
        double speed = Math.Sqrt((double)character.Velocity.XMillimeters * character.Velocity.XMillimeters + (double)character.Velocity.ZMillimeters * character.Velocity.ZMillimeters) / 1000.0;
        character.Action = speed > 3.2 ? CharacterActionKind.Run : CharacterActionKind.Walk;
        character.ActionProgressPermille = Math.Clamp(taskTick * 1000 / duration, 0, 1000);
        long distanceMillimeters = (long)Math.Sqrt((double)stepX * stepX + (double)stepZ * stepZ);
        character.LocomotionPhasePermille = (character.LocomotionPhasePermille + checked((int)(distanceMillimeters * 1000 / 1300))) % 1000;
    }

    private static void Face(CharacterState character, FixedVector3 target)
    {
        long dx = target.XMillimeters - character.FeetPosition.XMillimeters;
        long dz = target.ZMillimeters - character.FeetPosition.ZMillimeters;
        if (dx == 0 && dz == 0)
        {
            return;
        }

        double radians = Math.Atan2(dx, dz);
        character.FacingYawUnits = (int)Math.Round(radians * 10000.0, MidpointRounding.AwayFromZero);
        character.AttentionTargetPosition = target;
        character.Attention = CharacterAttentionKind.ObservePoint;
    }

    private static void ApplyEquipmentProfile(CharacterState character, string profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            character.EquipmentPoseProfileId = profileId;
        }
    }

    private static void SetAction(CharacterState character, CharacterActionKind action, int tick, int durationTicks)
    {
        character.Action = action;
        character.Velocity = FixedVector3.Zero;
        character.ActionProgressPermille = Math.Clamp(tick * 1000 / Math.Max(1, durationTicks), 0, 1000);
    }

    private sealed class RuntimeEntry(CharacterState character, CharacterTaskSequence sequence)
    {
        public CharacterState Character { get; } = character;
        public CharacterTaskSequence Sequence { get; } = sequence;
        public int TaskIndex { get; set; }
        public int TaskTick { get; set; }
    }
}
