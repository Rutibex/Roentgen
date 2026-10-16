using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record IngredientRequirement(string? ExactDefinitionId, string? RequiredTag, int Quantity, int MinimumConditionPermille = 0) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(ExactDefinitionId); writer.WriteString(RequiredTag); writer.WriteInt32(Quantity); writer.WriteInt32(MinimumConditionPermille); }
}
public sealed record ItemOutputDefinition(string DefinitionId, int Quantity, bool GenerateBlueprint) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(DefinitionId); writer.WriteInt32(Quantity); writer.WriteBoolean(GenerateBlueprint); }
}
public sealed record CraftingRecipeDefinition(string Id, CraftingOperationKind Operation, CraftingStationKind Station, IReadOnlyList<IngredientRequirement> Inputs, IReadOnlyList<ItemOutputDefinition> Outputs, IReadOnlyList<ItemOutputDefinition> Byproducts, SkillId Skill, int Difficulty, int DurationTicks, string FailureProfile, string DiscoveryProfile) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)Operation); writer.WriteInt32((int)Station); writer.WriteCount(Inputs.Count); foreach (IngredientRequirement value in Inputs) value.WriteCanonical(writer); writer.WriteCount(Outputs.Count); foreach (ItemOutputDefinition value in Outputs) value.WriteCanonical(writer); writer.WriteCount(Byproducts.Count); foreach (ItemOutputDefinition value in Byproducts) value.WriteCanonical(writer); writer.WriteInt32((int)Skill); writer.WriteInt32(Difficulty); writer.WriteInt32(DurationTicks); writer.WriteString(FailureProfile); writer.WriteString(DiscoveryProfile); }
}
public sealed record RecipeKnowledgeRecord(string RecipeId, RecipeKnowledgeSource Source, string SourceId, ulong LearnedTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(RecipeId); writer.WriteInt32((int)Source); writer.WriteString(SourceId); writer.WriteUInt64(LearnedTick); }
    public static RecipeKnowledgeRecord ReadCanonical(CanonicalReader reader) => new(reader.ReadRequiredString(), (RecipeKnowledgeSource)reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadUInt64());
}
public sealed class RecipeKnowledgeState : ICanonicalWritable
{
    private readonly SortedDictionary<string, RecipeKnowledgeRecord> records = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, RecipeKnowledgeRecord> KnownRecipes => records;
    public bool Knows(string recipeId) => records.ContainsKey(recipeId);
    public void Learn(RecipeKnowledgeRecord record) => records[record.RecipeId] = record;
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteCount(records.Count); foreach (RecipeKnowledgeRecord value in records.Values) value.WriteCanonical(writer); }
    public static RecipeKnowledgeState ReadCanonical(CanonicalReader reader)
    {
        var result = new RecipeKnowledgeState();
        int count = reader.ReadCount();
        for (int i = 0; i < count; i++) result.Learn(RecipeKnowledgeRecord.ReadCanonical(reader));
        return result;
    }
}
public sealed record CraftingPlan(StableId ActivityId, StableId ActorId, string RecipeId, StableId StationId, IReadOnlyList<StableId> InputItemIds, ulong StartedTick, ulong ResolutionSeed);
public sealed record CraftingResult(bool Success, bool CostlySuccess, string Message, IReadOnlyList<StableId> OutputItemIds);

public sealed class CraftingService
{
    private readonly ItemDefinitionCatalog itemCatalog; private readonly IReadOnlyDictionary<string, CraftingRecipeDefinition> recipes; private readonly IItemSkillCapabilityProvider capabilityProvider;
    public CraftingService(ItemDefinitionCatalog itemCatalog, IReadOnlyDictionary<string, CraftingRecipeDefinition> recipes, IItemSkillCapabilityProvider capabilityProvider) { this.itemCatalog = itemCatalog; this.recipes = recipes; this.capabilityProvider = capabilityProvider; }
    public CraftingResult Execute(ItemWorldState world, RecipeKnowledgeState knowledge, CraftingPlan plan)
    {
        if (!recipes.TryGetValue(plan.RecipeId, out CraftingRecipeDefinition? recipe)) return new(false, false, "unknown recipe", []);
        if (!knowledge.Knows(recipe.Id)) return new(false, false, "recipe not learned", []);
        ItemInstanceState[] candidates = plan.InputItemIds.Distinct().Order().Select(world.GetItem).ToArray();
        var available = candidates.ToDictionary(static value => value.Id, value => value.Quantity - world.ReservedQuantity(value.Id));
        var usage = new SortedDictionary<StableId, int>();
        foreach (IngredientRequirement requirement in recipe.Inputs)
        {
            int need = requirement.Quantity;
            foreach (ItemInstanceState candidate in candidates)
            {
                ItemDefinition definition = itemCatalog.Get(candidate.DefinitionId);
                bool match = (requirement.ExactDefinitionId is null || StringComparer.Ordinal.Equals(requirement.ExactDefinitionId, candidate.DefinitionId))
                    && (requirement.RequiredTag is null || definition.Tags.Contains(requirement.RequiredTag))
                    && candidate.Condition.OverallPermille >= requirement.MinimumConditionPermille;
                if (!match) continue;
                int remaining = available.GetValueOrDefault(candidate.Id);
                int used = Math.Min(need, Math.Max(0, remaining));
                if (used <= 0) continue;
                usage[candidate.Id] = usage.GetValueOrDefault(candidate.Id) + used;
                available[candidate.Id] = remaining - used;
                need -= used;
                if (need == 0) break;
            }
            if (need > 0) return new(false, false, "selected inputs do not satisfy recipe", []);
        }
        int skill = capabilityProvider.GetEffectiveSkill(plan.ActorId, recipe.Skill);
        int uncertainty = DeterministicRoll(plan.ResolutionSeed, recipe.Id) % 11 - 5;
        int margin = skill + uncertainty - recipe.Difficulty;
        if (margin < -10)
        {
            if (usage.Count > 0) Consume(world, usage.First().Key, 1);
            return new(false, false, "crafting failed", []);
        }
        foreach ((StableId itemId, int quantity) in usage) Consume(world, itemId, quantity);
        bool costly = margin < 0;
        var outputs = new List<StableId>();
        int ordinal = 0;
        foreach (ItemOutputDefinition output in recipe.Outputs.Concat(recipe.Byproducts))
        {
            itemCatalog.Get(output.DefinitionId);
            int quantity = costly && recipe.Outputs.Contains(output) ? Math.Max(1, output.Quantity / 2) : output.Quantity;
            StableId id = StableIdFactory.CreateChild(plan.ActivityId, "craft-output", ordinal++);
            var item = new ItemInstanceState(id, output.DefinitionId, null, ItemLocation.InInventory(plan.ActorId), quantity);
            world.AddItem(item);
            world.EnsureInventory(plan.ActorId).Add(id);
            outputs.Add(id);
        }
        return new(true, costly, costly ? "costly success" : "crafted", outputs);
    }
    private static void Consume(ItemWorldState world, StableId itemId, int quantity)
    {
        ItemInstanceState item = world.GetItem(itemId);
        if (quantity >= item.Quantity) world.DestroyItem(item.Id); else item.SetQuantity(item.Quantity - quantity);
    }
    private static int DeterministicRoll(ulong seed, string domain) { var writer = new CanonicalWriter(); writer.WriteUInt64(seed); writer.WriteString(domain); return (int)(writer.ComputeChecksum().A % 1000UL); }
}
