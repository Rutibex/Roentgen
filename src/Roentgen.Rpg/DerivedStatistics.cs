using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Rpg;

public sealed class DerivedStatSnapshot : ICanonicalWritable
{
    private readonly SortedDictionary<DerivedStatId, int> values;
    public DerivedStatSnapshot(IEnumerable<KeyValuePair<DerivedStatId, int>> values, ulong sourceRevision)
    {
        this.values = [];
        foreach ((DerivedStatId id, int value) in values) this.values[id] = value;
        SourceRevision = sourceRevision;
    }
    public ulong SourceRevision { get; }
    public int this[DerivedStatId id] => values[id];
    public IReadOnlyDictionary<DerivedStatId, int> Values => values;
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteUInt64(SourceRevision);
        writer.WriteCount(values.Count);
        foreach ((DerivedStatId id, int value) in values) { writer.WriteInt32((int)id); writer.WriteInt32(value); }
    }
}

public sealed class DerivedStatCalculator
{
    public DerivedStatSnapshot Calculate(CharacterProgressionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        AttributeSet a = state.Attributes;
        int level = state.Level;
        int vitality = a[AttributeId.Vitality];
        int reflex = a[AttributeId.Reflex];
        int awareness = a[AttributeId.Awareness];
        int physique = a[AttributeId.Physique];
        int fortune = a[AttributeId.Fortune];
        int intellect = a[AttributeId.Intellect];

        var values = new SortedDictionary<DerivedStatId, int>
        {
            [DerivedStatId.MaximumHealth] = 60 + vitality * 10 + (level - 1) * (3 + vitality / 3),
            [DerivedStatId.MaximumStamina] = 70 + vitality * 8 + reflex * 4,
            [DerivedStatId.CarryCapacityGrams] = (20 + physique * 7) * 1000,
            [DerivedStatId.MaximumFocusMilliunits] = (45 + awareness * 4 + reflex * 5) * 1000,
            [DerivedStatId.FocusRecoveryPerSecondMilliunits] = 3000 + reflex * 250,
            [DerivedStatId.Initiative] = awareness * 4 + reflex * 3 + fortune,
            [DerivedStatId.CriticalReserveMilliunits] = 2000 + fortune * 750,
            [DerivedStatId.KnockdownResistancePermille] = Math.Clamp(300 + physique * 55 + vitality * 25, 0, 1000),
            [DerivedStatId.ReloadSpeedPermille] = Math.Clamp(700 + reflex * 45, 700, 1300),
            [DerivedStatId.MovementSpeedPermille] = Math.Clamp(800 + reflex * 25 + vitality * 10, 800, 1250),
            [DerivedStatId.SkillPointsPerLevel] = 8 + intellect / 2
        };
        return new DerivedStatSnapshot(values, state.Revision);
    }
}

public sealed class FractionalRateAccumulator : ICanonicalWritable
{
    public int RemainderMilliunits { get; private set; }
    public int Advance(int perSecondMilliunits, int ticksPerSecond = ProgressionRules.TicksPerSecond)
    {
        if (perSecondMilliunits < 0) throw new ArgumentOutOfRangeException(nameof(perSecondMilliunits));
        int total = checked(RemainderMilliunits + perSecondMilliunits);
        int whole = total / ticksPerSecond;
        RemainderMilliunits = total % ticksPerSecond;
        return whole;
    }
    public void WriteCanonical(CanonicalWriter writer) => writer.WriteInt32(RemainderMilliunits);
}
