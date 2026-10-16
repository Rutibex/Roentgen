using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Rpg;

public sealed class AttributeSet : ICanonicalWritable
{
    private readonly SortedDictionary<AttributeId, int> values = [];

    public AttributeSet(int initialValue = ProgressionRules.BaseAttribute)
    {
        if (initialValue is < ProgressionRules.MinimumAttribute or > ProgressionRules.MaximumAttribute)
            throw new ArgumentOutOfRangeException(nameof(initialValue));
        foreach (AttributeId id in ProgressionRules.AttributeOrder) values[id] = initialValue;
    }

    public int this[AttributeId id] => values[id];
    public IReadOnlyDictionary<AttributeId, int> Values => values;
    public int AllocatedAboveBase => values.Values.Sum() - ProgressionRules.BaseAttribute * values.Count;

    public void Set(AttributeId id, int value)
    {
        if (value is < ProgressionRules.MinimumAttribute or > ProgressionRules.MaximumAttribute)
            throw new ArgumentOutOfRangeException(nameof(value));
        values[id] = value;
    }

    public bool TryAdjust(AttributeId id, int delta)
    {
        int next = values[id] + delta;
        if (next is < ProgressionRules.MinimumAttribute or > ProgressionRules.MaximumAttribute) return false;
        values[id] = next;
        return true;
    }

    public AttributeSet Clone()
    {
        var clone = new AttributeSet();
        foreach ((AttributeId id, int value) in values) clone.values[id] = value;
        return clone;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteCount(values.Count);
        foreach ((AttributeId id, int value) in values)
        {
            writer.WriteInt32((int)id);
            writer.WriteInt32(value);
        }
    }

    public static AttributeSet ReadCanonical(CanonicalReader reader)
    {
        var result = new AttributeSet();
        int count = reader.ReadCount();
        if (count != ProgressionRules.AttributeOrder.Count) throw new InvalidDataException("Attribute count is invalid.");
        for (int index = 0; index < count; index++) result.Set((AttributeId)reader.ReadInt32(), reader.ReadInt32());
        return result;
    }
}

public sealed class SkillSet : ICanonicalWritable
{
    private readonly SortedDictionary<SkillId, int> values = [];

    public SkillSet()
    {
        foreach (SkillId id in ProgressionRules.SkillOrder) values[id] = 0;
    }

    public int this[SkillId id] => values[id];
    public IReadOnlyDictionary<SkillId, int> Values => values;

    public void Set(SkillId id, int value) => values[id] = Math.Clamp(value, ProgressionRules.MinimumSkill, ProgressionRules.MaximumSkill);

    public bool TryPurchaseRanks(SkillId id, int ranks, ref int availablePoints)
    {
        if (ranks <= 0) return false;
        int current = values[id];
        int cost = 0;
        for (int index = 0; index < ranks; index++)
        {
            if (current + index >= ProgressionRules.MaximumSkill) return false;
            cost = checked(cost + GetPointCost(current + index));
        }
        if (cost > availablePoints) return false;
        availablePoints -= cost;
        values[id] = current + ranks;
        return true;
    }

    public static int GetPointCost(int currentSkill) => currentSkill switch
    {
        <= 50 => 1,
        <= 75 => 2,
        _ => 3
    };

    public SkillSet Clone()
    {
        var clone = new SkillSet();
        foreach ((SkillId id, int value) in values) clone.values[id] = value;
        return clone;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteCount(values.Count);
        foreach ((SkillId id, int value) in values)
        {
            writer.WriteInt32((int)id);
            writer.WriteInt32(value);
        }
    }

    public static SkillSet ReadCanonical(CanonicalReader reader)
    {
        var result = new SkillSet();
        int count = reader.ReadCount();
        if (count != ProgressionRules.SkillOrder.Count) throw new InvalidDataException("Skill count is invalid.");
        for (int index = 0; index < count; index++) result.Set((SkillId)reader.ReadInt32(), reader.ReadInt32());
        return result;
    }
}

public sealed record SkillDefinition(
    SkillId Id,
    AttributeId PrimaryAttribute,
    AttributeId? SecondaryAttribute,
    int BaseValue = 5,
    int PrimaryMultiplier = 3,
    int SecondaryMultiplier = 2)
{
    public int CalculateStartingValue(AttributeSet attributes, bool specialized)
    {
        int value = BaseValue + attributes[PrimaryAttribute] * PrimaryMultiplier;
        if (SecondaryAttribute is AttributeId secondary) value += attributes[secondary] * SecondaryMultiplier;
        if (specialized) value += ProgressionRules.SpecializedSkillBonus;
        return Math.Clamp(value, 0, 100);
    }
}

public static class Phase12SkillDefinitions
{
    public static IReadOnlyDictionary<SkillId, SkillDefinition> All { get; } = new SortedDictionary<SkillId, SkillDefinition>
    {
        [SkillId.Ballistics] = new(SkillId.Ballistics, AttributeId.Awareness, AttributeId.Reflex),
        [SkillId.DirectedEnergy] = new(SkillId.DirectedEnergy, AttributeId.Intellect, AttributeId.Awareness),
        [SkillId.Demolitions] = new(SkillId.Demolitions, AttributeId.Intellect, AttributeId.Awareness),
        [SkillId.Melee] = new(SkillId.Melee, AttributeId.Physique, AttributeId.Reflex),
        [SkillId.Unarmed] = new(SkillId.Unarmed, AttributeId.Physique, AttributeId.Reflex),
        [SkillId.Athletics] = new(SkillId.Athletics, AttributeId.Physique, AttributeId.Vitality),
        [SkillId.Stealth] = new(SkillId.Stealth, AttributeId.Reflex, AttributeId.Awareness),
        [SkillId.Security] = new(SkillId.Security, AttributeId.Reflex, AttributeId.Intellect),
        [SkillId.Engineering] = new(SkillId.Engineering, AttributeId.Intellect, AttributeId.Physique),
        [SkillId.Science] = new(SkillId.Science, AttributeId.Intellect, null),
        [SkillId.Medicine] = new(SkillId.Medicine, AttributeId.Intellect, AttributeId.Awareness),
        [SkillId.Fieldcraft] = new(SkillId.Fieldcraft, AttributeId.Awareness, AttributeId.Vitality),
        [SkillId.Influence] = new(SkillId.Influence, AttributeId.Presence, AttributeId.Awareness),
        [SkillId.Commerce] = new(SkillId.Commerce, AttributeId.Presence, AttributeId.Intellect)
    };

    public static SkillSet CreateStartingSkills(AttributeSet attributes, IEnumerable<SkillId> specialized)
    {
        var set = new SkillSet();
        var tags = new HashSet<SkillId>(specialized);
        foreach ((SkillId id, SkillDefinition definition) in All) set.Set(id, definition.CalculateStartingValue(attributes, tags.Contains(id)));
        return set;
    }
}
