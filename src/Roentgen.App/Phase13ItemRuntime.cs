using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Items;
using Roentgen.Persistence;
using Roentgen.Rpg;
using Roentgen.UI;

namespace Roentgen.App;

/// <summary>
/// The Phase 13 authority used by the playable regression campaign.  Combat, equipment,
/// ammunition, crafting, repair, trade, loot and persistence all project from this state.
/// </summary>
public sealed class Phase13ItemRuntime
{
    private readonly ItemCombatDefinitionAdapter combatAdapter = new();
    private readonly InventoryCapacityService capacity = new();
    private readonly Phase13ItemScreenBuilder screens = new();
    private readonly IItemSkillCapabilityProvider skills;
    private readonly Dictionary<StableId, string> weaponAmmunition = [];

    public Phase13ItemRuntime(Phase13ContentBundle content, ItemWorldState world, ItemFactory factory, IItemSkillCapabilityProvider? skills = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.skills = skills ?? new FixedItemSkillCapabilityProvider(55);
        Transactions = new ItemTransactionService(Content.Items);
        Ammunition = new AmmunitionService(Content.Items, Content.Ammunition);
        Crafting = new CraftingService(Content.Items, Content.Recipes, this.skills);
        Repair = new RepairService(Content.Items, this.skills);
        Merchants = new MerchantService();
        Consumables = new ConsumableService(Content.Consumables);
    }

    public Phase13ContentBundle Content { get; }
    public ItemWorldState World { get; private set; }
    public ItemFactory Factory { get; }
    public ItemTransactionService Transactions { get; }
    public AmmunitionService Ammunition { get; }
    public CraftingService Crafting { get; }
    public RepairService Repair { get; }
    public MerchantService Merchants { get; }
    public ConsumableService Consumables { get; }
    public RecipeKnowledgeState RecipeKnowledge { get; private set; } = new();
    public StableId PlayerId { get; private set; }
    public StableId MerchantId { get; private set; }
    public StableId WorkbenchId { get; private set; }
    public StableId MedicalStationId { get; private set; }
    public StableId DiagnosticTableId { get; private set; }
    public StableId SidearmId { get; private set; }
    public StableId RifleId { get; private set; }
    public StableId MeleeId { get; private set; }
    public StableId ThrowableStackId { get; private set; }
    public StableId ActiveWeaponId { get; private set; }
    public string LastStatus { get; private set; } = "items ready";

    public static Phase13ItemRuntime CreateDefault()
    {
        Phase13ContentBundle content = Phase21ItemContent.Create();
        var world = new ItemWorldState();
        foreach (SettlementEconomyState economy in Phase13Content.CreateEconomyFixtures()) world.AddEconomy(economy);
        return new Phase13ItemRuntime(content, world, new ItemFactory(content));
    }

    public static Phase13ItemRuntime CreateIntegrated(
        StableId playerId,
        CampaignSeed seed,
        StartingEquipmentGrant grant,
        int carryCapacityGrams,
        IItemSkillCapabilityProvider skills)
    {
        Phase13ContentBundle content = Phase21ItemContent.Create();
        var world = new ItemWorldState();
        foreach (SettlementEconomyState economy in Phase13Content.CreateEconomyFixtures()) world.AddEconomy(economy);
        var runtime = new Phase13ItemRuntime(content, world, new ItemFactory(content), skills);
        runtime.InitializeRegressionCampaign(playerId, seed, grant, carryCapacityGrams);
        return runtime;
    }

    public IReadOnlyList<ItemInstanceState> CreateStarterEquipment(StableId playerId, CampaignSeed seed, StartingEquipmentGrant grant, int carryCapacityGrams)
    {
        World.EnsureInventory(playerId, carryCapacityGrams, Math.Max(10_000, carryCapacityGrams / 2));
        return Factory.InstantiateStartingPackage(World, playerId, seed, grant.ItemDefinitionIds);
    }

    public InventoryScreenModel BuildPlayerInventory() => screens.BuildInventory(PlayerId, World, Content.Items);

    public MerchantScreenModel BuildMerchant() => screens.BuildMerchant(MerchantId, PlayerId, World, Content.Items, World.GetEconomy(StableIdFactory.Create("settlement", "prospect-clinic")));

    public int CurrencyBalance(StableId ownerId)
    {
        SettlementEconomyState economy = World.GetEconomy(StableIdFactory.Create("settlement", "prospect-clinic"));
        return CountDefinition(ownerId, economy.CurrencyPolicy.CurrencyDefinitionId);
    }

    public int EffectiveCommerce => skills.GetEffectiveSkill(PlayerId, SkillId.Commerce);

    public MerchantTradePreviewUiModel BuildMerchantTradePreview(StableId itemId, bool merchantSelling, int requestedQuantity = 1)
    {
        ItemInstanceState item = World.GetItem(itemId);
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        SettlementEconomyState economy = World.GetEconomy(StableIdFactory.Create("settlement", "prospect-clinic"));
        EconomyCommodity commodity = ResolveCommodity(definition);
        int commerce = Math.Clamp(900 + EffectiveCommerce * 2, 850, 1150);
        PriceBreakdown price = new EconomyPriceCalculator().Calculate(definition, item, economy, commodity, merchantSelling, 1000, commerce);
        int available = Math.Max(0, item.Quantity - World.ReservedQuantity(item.Id));
        StableId payer = merchantSelling ? PlayerId : MerchantId;
        int affordable = price.FinalPriceMinorUnits <= 0 ? available : Math.Min(available, CurrencyBalance(payer) / price.FinalPriceMinorUnits);
        int quantity = Math.Clamp(requestedQuantity, 1, Math.Max(1, available));
        quantity = Math.Min(quantity, Math.Max(0, affordable));
        int neutralPrice = new EconomyPriceCalculator().Calculate(definition, item, economy, commodity, merchantSelling, 1000, 1000).FinalPriceMinorUnits;
        int delta = neutralPrice == 0 ? 0 : (price.FinalPriceMinorUnits - neutralPrice) * 100 / neutralPrice;
        bool better = merchantSelling ? delta < 0 : delta > 0;
        string tradeSide = merchantSelling ? "buy" : "sell";
        string commerceLabel = delta == 0
            ? $"Commerce {EffectiveCommerce}: neutral {tradeSide} pricing"
            : $"Commerce {EffectiveCommerce}: {Math.Abs(delta)}% {(better ? "better" : "worse")} {tradeSide} price";
        return new MerchantTradePreviewUiModel(item.Id, DisplayName(item.Id), merchantSelling, available, price.FinalPriceMinorUnits,
            affordable, quantity, price.FinalPriceMinorUnits * quantity, price.BaseValue, price.ConditionPermille, price.ScarcityPermille,
            price.TaxPermille, price.RiskPermille, price.RelationshipPermille, price.CommercePermille, commerceLabel);
    }

    public CraftingScreenModel BuildCrafting() => new(
        RecipeKnowledge.KnownRecipes.Keys.ToArray(),
        Content.Recipes.Values.Where(recipe => RecipeKnowledge.Knows(recipe.Id)).OrderBy(static recipe => recipe.Id, StringComparer.Ordinal).ToArray(),
        WorkbenchId,
        LastStatus);

    public string DisplayNameFor(StableId itemId) => DisplayName(itemId);

    public ItemInspectionModel BuildInspection(StableId itemId, bool includeDiagnostics = false)
    {
        ItemInstanceState item = World.GetItem(itemId);
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        var details = new List<string>
        {
            $"Type: {PlayerFacingKind(definition.Kind)}   Quantity: {item.Quantity}",
            $"Mass: {definition.MassGrams * item.Quantity / 1000f:F2} kg   Value: {definition.BaseValueMinorUnits * item.Quantity} scrip",
            $"Condition: {item.Condition.OverallPermille / 10.0:F1}%",
            $"Structural {item.Condition.StructuralPermille / 10.0:F0}% | Calibration {item.Condition.CalibrationPermille / 10.0:F0}% | Cleanliness {item.Condition.CleanlinessPermille / 10.0:F0}%",
            item.Condition.ContaminationPermille > 0 ? $"Contamination: {item.Condition.ContaminationPermille / 10.0:F0}%" : "Contamination: none detected"
        };
        if (item.Blueprint is not null)
        {
            details.Add($"Identity: {item.Blueprint.DisplayIdentity}");
            if (!string.IsNullOrWhiteSpace(item.Blueprint.Provenance.ManufacturerHistory)) details.Add($"History: {item.Blueprint.Provenance.ManufacturerHistory}");
            if (!string.IsNullOrWhiteSpace(item.Blueprint.Provenance.FactionAlteration)) details.Add($"Alteration: {item.Blueprint.Provenance.FactionAlteration}");
        }
        if (Content.WeaponFamilies.ContainsKey(item.DefinitionId))
        {
            WeaponCapabilitySnapshot capability = CompileWeapon(itemId);
            details.Add($"Weapon: {capability.AttackKind} | Range {capability.MaximumRangeMillimeters / 1000f:F1} m | Reliability {capability.ReliabilityPermille / 10.0:F0}%");
            details.Add($"Feed: {item.AmmunitionLoad?.LoadedCount ?? 0}/{capability.MagazineCapacity} | Recoil {capability.RecoilPermille / 10.0:F0}% | Spread {capability.SpreadPermille / 10.0:F0}%");
            details.Add("Damage: " + string.Join(", ", capability.Damage.Select(value => $"{value.Type} {value.Amount}")));
            foreach ((WeaponComponentSlot slot, StableId componentId) in item.InstalledComponents.OrderBy(static pair => pair.Key))
            {
                ItemInstanceState component = World.GetItem(componentId);
                WeaponComponentDefinition componentDefinition = Content.WeaponComponents[component.DefinitionId];
                details.Add($"{slot}: {componentDefinition.DisplayName}");
            }
        }
        if (Content.Armor.TryGetValue(item.DefinitionId, out ArmorItemDefinition? armor))
        {
            details.Add($"Armor: {armor.Layer} | Coverage: {string.Join(", ", armor.CoveredRegions.Order(StringComparer.Ordinal))}");
            details.Add("Protection: " + string.Join(", ", armor.Protection.Select(value => $"{value.Type} T{value.Threshold}/R{value.ResistancePermille / 10}%")));
            details.Add($"Mobility penalty {armor.MobilityPenaltyPermille / 10.0:F0}% | Noise {armor.NoisePermille / 10.0:F0}% | Environmental protection {armor.EnvironmentalProtectionPermille / 10.0:F0}%");
        }
        if (includeDiagnostics)
        {
            details.Add($"Definition: {definition.Id}");
            details.Add($"Item ID: {item.Id}");
            details.Add($"Location: {item.Location.Kind} / {item.Location.HolderId}");
            if (item.Blueprint is not null) details.Add($"Blueprint fingerprint: {item.Blueprint.Fingerprint}");
            if (item.RuntimeTags.Count > 0) details.Add("Runtime tags: " + string.Join(", ", item.RuntimeTags));
            if (definition.Tags.Count > 0) details.Add("Definition tags: " + string.Join(", ", definition.Tags.Order(StringComparer.Ordinal)));
        }
        return new ItemInspectionModel(itemId, DisplayName(itemId), details);
    }

    public ItemKind KindFor(StableId itemId) => Content.Items.Get(World.GetItem(itemId).DefinitionId).Kind;

    public bool CanRepairInventoryItem(StableId itemId)
    {
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null || item.Location.HolderId != PlayerId || item.Condition.OverallPermille >= 1000) return false;
        ItemKind kind = Content.Items.Get(item.DefinitionId).Kind;
        if (kind is not (ItemKind.Weapon or ItemKind.Armor or ItemKind.Tool or ItemKind.Container)) return false;
        return World.GetInventory(PlayerId).ItemIds.Select(World.GetItem).Any(value => value.Id != itemId && (Content.Items.Get(value.DefinitionId).Kind is ItemKind.Material or ItemKind.Component));
    }

    public bool CanSplitInventoryItem(StableId itemId)
    {
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null || item.Location.HolderId != PlayerId || item.Quantity <= 1) return false;
        return Content.Items.Get(item.DefinitionId).StackPolicy != StackPolicy.Never;
    }

    public bool CanEquipInventoryItem(StableId itemId)
    {
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null || item.Location.HolderId != PlayerId) return false;
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        return Enum.GetValues<EquipmentSlot>().Any(slot => EquipmentRules.CanEquip(definition, slot));
    }

    private static readonly EquipmentSlot[] PlayerEquipmentSlots =
    [
        EquipmentSlot.Sidearm, EquipmentSlot.Longarm, EquipmentSlot.Melee, EquipmentSlot.Throwable,
        EquipmentSlot.BackEquipment, EquipmentSlot.QuickConsumable1, EquipmentSlot.QuickConsumable2,
        EquipmentSlot.Underlayer, EquipmentSlot.Clothing, EquipmentSlot.Torso, EquipmentSlot.LeftArm,
        EquipmentSlot.RightArm, EquipmentSlot.LeftLeg, EquipmentSlot.RightLeg, EquipmentSlot.Head,
        EquipmentSlot.Respirator, EquipmentSlot.Utility, EquipmentSlot.PoweredFrame
    ];

    public IReadOnlyList<EquipmentSlotUiModel> BuildEquipmentSlots()
    {
        EquipmentLoadoutState loadout = World.EnsureLoadout(PlayerId);
        ItemInstanceState[] inventory = World.GetInventory(PlayerId).ItemIds.Select(World.GetItem).ToArray();
        return PlayerEquipmentSlots.Select(slot =>
        {
            StableId equipped = loadout.TryGet(slot, out StableId value) ? value : StableId.Empty;
            bool compatible = inventory.Any(item => IsPlayerFacingSlotCompatible(Content.Items.Get(item.DefinitionId), item.DefinitionId, slot));
            bool repair = !equipped.IsEmpty && CanRepairInventoryItem(equipped);
            return new EquipmentSlotUiModel(slot, EquipmentSlotLabel(slot), equipped, equipped.IsEmpty ? "Empty" : DisplayName(equipped), compatible, repair);
        }).ToArray();
    }

    public IReadOnlyList<EquipmentCandidateUiModel> BuildEquipmentCandidates(EquipmentSlot slot)
    {
        EquipmentLoadoutState loadout = World.EnsureLoadout(PlayerId);
        var ids = new List<StableId>();
        if (loadout.TryGet(slot, out StableId current) && !current.IsEmpty) ids.Add(current);
        ids.AddRange(World.GetInventory(PlayerId).ItemIds
            .Where(id => IsPlayerFacingSlotCompatible(Content.Items.Get(World.GetItem(id).DefinitionId), World.GetItem(id).DefinitionId, slot))
            .OrderBy(id => DisplayName(id), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static id => id));
        return ids.Distinct().Select(id =>
        {
            ItemInstanceState item = World.GetItem(id);
            ItemDefinition definition = Content.Items.Get(item.DefinitionId);
            return new EquipmentCandidateUiModel(id, DisplayName(id), definition.Kind, item.Condition.OverallPermille,
                definition.MassGrams * item.Quantity, definition.BaseValueMinorUnits * item.Quantity);
        }).ToArray();
    }

    public StableId GetEquippedItem(EquipmentSlot slot)
    {
        EquipmentLoadoutState loadout = World.EnsureLoadout(PlayerId);
        return loadout.TryGet(slot, out StableId itemId) ? itemId : StableId.Empty;
    }

    public bool IsEquippedInSlot(EquipmentSlot slot, StableId itemId) => GetEquippedItem(slot) == itemId;

    public bool EquipInventoryItemToSlot(StableId itemId, EquipmentSlot slot, ulong tick)
    {
        if (IsEquippedInSlot(slot, itemId))
        {
            LastStatus = $"{DisplayName(itemId)} is already equipped in {EquipmentSlotLabel(slot)}.";
            return true;
        }
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null || item.Location.Kind != ItemLocationKind.Inventory || item.Location.HolderId != PlayerId)
        {
            LastStatus = "item is unavailable";
            return false;
        }
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        if (!IsPlayerFacingSlotCompatible(definition, item.DefinitionId, slot))
        {
            LastStatus = $"{DisplayName(itemId)} is incompatible with {EquipmentSlotLabel(slot)}.";
            return false;
        }
        ItemTransactionResult result = Transactions.Execute(World, new ItemTransaction(
            StableIdFactory.CreateChild(PlayerId, "equip-slot-transaction", $"{itemId}:{slot}:{tick}:{World.Revision}"),
            tick,
            [new EquipItemOperation(PlayerId, itemId, slot)],
            []));
        LastStatus = result.Status == ItemTransactionStatus.Committed ? $"equipped {DisplayName(itemId)} in {EquipmentSlotLabel(slot)}" : result.Message;
        if (result.Status == ItemTransactionStatus.Committed && definition.Kind == ItemKind.Weapon) ActiveWeaponId = itemId;
        return result.Status == ItemTransactionStatus.Committed;
    }

    public EquipmentComparisonModel BuildEquipmentComparison(EquipmentSlot slot, StableId candidateItemId)
    {
        StableId currentItemId = GetEquippedItem(slot);
        string title = $"{EquipmentSlotLabel(slot)} Comparison";
        if (candidateItemId.IsEmpty)
        {
            if (currentItemId.IsEmpty) return new EquipmentComparisonModel(title, ["Nothing is equipped in this slot.", "Choose Compatible Items to browse replacements."]);
            return new EquipmentComparisonModel(title, BuildSingleEquipmentSummary(currentItemId, "CURRENT"));
        }
        if (currentItemId.IsEmpty)
        {
            var lines = new List<string> { "CURRENT: Empty", $"CANDIDATE: {DisplayName(candidateItemId)}" };
            lines.AddRange(BuildSingleEquipmentSummary(candidateItemId, "CANDIDATE").Skip(1));
            return new EquipmentComparisonModel(title, lines);
        }
        if (currentItemId == candidateItemId)
            return new EquipmentComparisonModel(title, ["This item is currently equipped.", .. BuildSingleEquipmentSummary(currentItemId, "CURRENT")]);

        ItemInstanceState current = World.GetItem(currentItemId);
        ItemInstanceState candidate = World.GetItem(candidateItemId);
        ItemDefinition currentDefinition = Content.Items.Get(current.DefinitionId);
        ItemDefinition candidateDefinition = Content.Items.Get(candidate.DefinitionId);
        var comparison = new List<string>
        {
            $"CURRENT: {DisplayName(currentItemId)}",
            $"CANDIDATE: {DisplayName(candidateItemId)}",
            CompareNumber("Condition", current.Condition.OverallPermille / 10.0, candidate.Condition.OverallPermille / 10.0, "%", false, "0.0"),
            CompareNumber("Mass", currentDefinition.MassGrams / 1000.0, candidateDefinition.MassGrams / 1000.0, " kg", true, "0.00"),
            CompareNumber("Value", currentDefinition.BaseValueMinorUnits, candidateDefinition.BaseValueMinorUnits, " scrip", false, "0")
        };

        if (currentDefinition.Kind == ItemKind.Weapon && candidateDefinition.Kind == ItemKind.Weapon
            && Content.WeaponFamilies.ContainsKey(current.DefinitionId) && Content.WeaponFamilies.ContainsKey(candidate.DefinitionId))
        {
            WeaponCapabilitySnapshot a = CompileWeapon(currentItemId);
            WeaponCapabilitySnapshot b = CompileWeapon(candidateItemId);
            comparison.Add(CompareNumber("Damage", a.Damage.Sum(static value => value.Amount), b.Damage.Sum(static value => value.Amount), string.Empty, false, "0"));
            comparison.Add(CompareNumber("Range", a.MaximumRangeMillimeters / 1000.0, b.MaximumRangeMillimeters / 1000.0, " m", false, "0.0"));
            comparison.Add(CompareNumber("Magazine", a.MagazineCapacity, b.MagazineCapacity, string.Empty, false, "0"));
            comparison.Add(CompareNumber("Reliability", a.ReliabilityPermille / 10.0, b.ReliabilityPermille / 10.0, "%", false, "0.0"));
            comparison.Add(CompareNumber("Recoil", a.RecoilPermille / 10.0, b.RecoilPermille / 10.0, "%", true, "0.0"));
            comparison.Add(CompareNumber("Spread", a.SpreadPermille / 10.0, b.SpreadPermille / 10.0, "%", true, "0.0"));
        }
        else if (Content.Armor.TryGetValue(current.DefinitionId, out ArmorItemDefinition? armorA)
            && Content.Armor.TryGetValue(candidate.DefinitionId, out ArmorItemDefinition? armorB))
        {
            comparison.Add(CompareNumber("Protection threshold", armorA.Protection.Sum(static value => value.Threshold), armorB.Protection.Sum(static value => value.Threshold), string.Empty, false, "0"));
            comparison.Add(CompareNumber("Resistance", ArmorResistanceAverage(armorA), ArmorResistanceAverage(armorB), "%", false, "0.0"));
            comparison.Add(CompareNumber("Environmental", armorA.EnvironmentalProtectionPermille / 10.0, armorB.EnvironmentalProtectionPermille / 10.0, "%", false, "0.0"));
            comparison.Add(CompareNumber("Mobility penalty", armorA.MobilityPenaltyPermille / 10.0, armorB.MobilityPenaltyPermille / 10.0, "%", true, "0.0"));
            comparison.Add(CompareNumber("Noise", armorA.NoisePermille / 10.0, armorB.NoisePermille / 10.0, "%", true, "0.0"));
        }
        return new EquipmentComparisonModel(title, comparison);
    }

    public EquipmentComparisonModel? BuildEquipmentComparisonForCandidate(StableId candidateItemId)
    {
        if (!World.TryGetItem(candidateItemId, out ItemInstanceState? item) || item is null) return null;
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        EquipmentSlot? slot = PreferredEquipmentSlot(definition, item.DefinitionId);
        return slot.HasValue && EquipmentRules.CanEquip(definition, slot.Value)
            ? BuildEquipmentComparison(slot.Value, candidateItemId)
            : null;
    }

    public CraftingRecipeUiModel BuildCraftingRecipeUi(CraftingRecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ItemInstanceState[] inventory = World.GetInventory(PlayerId).ItemIds.Select(World.GetItem).OrderBy(static item => item.Id).ToArray();
        CraftingIngredientUiModel[] ingredients = recipe.Inputs.Select(requirement =>
        {
            int have = inventory.Where(item => IngredientMatches(item, requirement)).Sum(static item => item.Quantity);
            string label = requirement.ExactDefinitionId is not null
                ? Content.Items.Get(requirement.ExactDefinitionId).DisplayName
                : HumanizeId(requirement.RequiredTag ?? "Material");
            return new CraftingIngredientUiModel(label, have, requirement.Quantity, have >= requirement.Quantity);
        }).ToArray();
        string[] outputs = recipe.Outputs.Select(output =>
        {
            ItemDefinition definition = Content.Items.Get(output.DefinitionId);
            return $"{definition.DisplayName} x{output.Quantity}";
        }).ToArray();
        int skill = skills.GetEffectiveSkill(PlayerId, recipe.Skill);
        int margin = skill - recipe.Difficulty;
        string risk = margin >= 10 ? "LOW RISK" : margin >= 0 ? "FAVORABLE" : margin >= -5 ? "RISKY" : "SEVERE RISK";
        int max = GetMaximumCraftCount(recipe);
        return new CraftingRecipeUiModel(recipe.Id, HumanizeRecipeName(recipe), CraftingCategoryFor(recipe), recipe.Operation, recipe.Station,
            recipe.Skill, skill, recipe.Difficulty, ingredients, outputs, max, max > 0, risk);
    }

    public int GetMaximumCraftCount(string recipeId)
    {
        if (!Content.Recipes.TryGetValue(recipeId, out CraftingRecipeDefinition? recipe) || !RecipeKnowledge.Knows(recipeId)) return 0;
        return GetMaximumCraftCount(recipe);
    }

    public CraftingResult CraftKnownRecipeMultiple(string recipeId, int requestedCount, ulong tick)
    {
        if (!Content.Recipes.TryGetValue(recipeId, out CraftingRecipeDefinition? recipe)) return SetCraftResult(new CraftingResult(false, false, "unknown recipe", []));
        if (!RecipeKnowledge.Knows(recipeId)) return SetCraftResult(new CraftingResult(false, false, "recipe not learned", []));
        int maximum = GetMaximumCraftCount(recipe);
        if (maximum <= 0) return SetCraftResult(new CraftingResult(false, false, "missing required materials", []));
        int attempts = requestedCount <= 0 ? maximum : Math.Min(requestedCount, maximum);
        var outputIds = new List<StableId>();
        int successes = 0;
        bool costly = false;
        string? stopped = null;
        for (int index = 0; index < attempts; index++)
        {
            CraftingResult result = CraftKnownRecipe(recipeId, tick + (ulong)index);
            if (!result.Success)
            {
                stopped = result.Message;
                break;
            }
            successes++;
            costly |= result.CostlySuccess;
            outputIds.AddRange(result.OutputItemIds);
        }
        if (successes == 0) return SetCraftResult(new CraftingResult(false, false, stopped ?? "crafting failed", []));
        string message = stopped is null
            ? $"Crafted {HumanizeRecipeName(recipe)} x{successes}."
            : $"Crafted {HumanizeRecipeName(recipe)} x{successes}; stopped: {stopped}.";
        return SetCraftResult(new CraftingResult(true, costly, message, outputIds));
    }

    private CraftingResult SetCraftResult(CraftingResult result)
    {
        LastStatus = result.Message;
        return result;
    }

    private int GetMaximumCraftCount(CraftingRecipeDefinition recipe)
    {
        if (recipe.Inputs.Count == 0) return 1;
        ItemInstanceState[] candidates = World.GetInventory(PlayerId).ItemIds.Select(World.GetItem).OrderBy(static item => item.Id).ToArray();
        var available = candidates.ToDictionary(static item => item.Id, static item => item.Quantity);
        int crafts = 0;
        while (crafts < 999 && TryAllocateRecipe(recipe, candidates, available)) crafts++;
        return crafts;
    }

    private bool TryAllocateRecipe(CraftingRecipeDefinition recipe, IReadOnlyList<ItemInstanceState> candidates, Dictionary<StableId, int> available)
    {
        var staged = new Dictionary<StableId, int>(available);
        foreach (IngredientRequirement requirement in recipe.Inputs)
        {
            int need = requirement.Quantity;
            foreach (ItemInstanceState candidate in candidates)
            {
                if (!IngredientMatches(candidate, requirement)) continue;
                int remaining = staged.GetValueOrDefault(candidate.Id);
                int used = Math.Min(need, Math.Max(0, remaining));
                if (used <= 0) continue;
                staged[candidate.Id] = remaining - used;
                need -= used;
                if (need == 0) break;
            }
            if (need > 0) return false;
        }
        available.Clear();
        foreach ((StableId id, int quantity) in staged) available[id] = quantity;
        return true;
    }

    private bool IngredientMatches(ItemInstanceState item, IngredientRequirement requirement)
    {
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        return (requirement.ExactDefinitionId is null || StringComparer.Ordinal.Equals(requirement.ExactDefinitionId, item.DefinitionId))
            && (requirement.RequiredTag is null || definition.Tags.Contains(requirement.RequiredTag))
            && item.Condition.OverallPermille >= requirement.MinimumConditionPermille;
    }

    private CraftingCategory CraftingCategoryFor(CraftingRecipeDefinition recipe)
    {
        ItemKind? outputKind = recipe.Outputs.Count == 0 ? null : Content.Items.Get(recipe.Outputs[0].DefinitionId).Kind;
        if (recipe.Operation is CraftingOperationKind.Modify or CraftingOperationKind.Repair) return CraftingCategory.Upgrades;
        if (outputKind == ItemKind.Weapon) return CraftingCategory.Weapons;
        if (outputKind == ItemKind.Ammunition) return CraftingCategory.Ammunition;
        if (outputKind == ItemKind.Tool) return CraftingCategory.Tools;
        if (outputKind is ItemKind.Component or ItemKind.Material) return CraftingCategory.Components;
        if (outputKind == ItemKind.Consumable && recipe.Skill == SkillId.Medicine) return CraftingCategory.Medicine;
        if (outputKind == ItemKind.Consumable || recipe.Skill == SkillId.Fieldcraft || recipe.Operation == CraftingOperationKind.Cook) return CraftingCategory.Survival;
        return CraftingCategory.Special;
    }

    private string HumanizeRecipeName(CraftingRecipeDefinition recipe)
    {
        string source = recipe.Id;
        int dot = source.LastIndexOf('.');
        if (dot >= 0 && dot + 1 < source.Length) source = source[(dot + 1)..];
        return HumanizeId(source);
    }

    private static string HumanizeId(string value)
    {
        string text = value.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ');
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    private StableId StationIdFor(CraftingStationKind station) => station switch
    {
        CraftingStationKind.MedicalStation => MedicalStationId,
        CraftingStationKind.DiagnosticTable => DiagnosticTableId,
        CraftingStationKind.Workbench => WorkbenchId,
        _ => StableIdFactory.Create("station", $"prospect-clinic-{station.ToString().ToLowerInvariant()}")
    };

    private static bool IsPlayerFacingSlotCompatible(ItemDefinition definition, string definitionId, EquipmentSlot slot)
    {
        if (!EquipmentRules.CanEquip(definition, slot)) return false;
        string id = definitionId.ToLowerInvariant();
        if (definition.Kind == ItemKind.Weapon)
        {
            if (slot is EquipmentSlot.ActiveHand or EquipmentSlot.OffHand) return true;
            if (slot == EquipmentSlot.Melee) return id.Contains("melee") || id.Contains("knife") || id.Contains("club") || id.Contains("axe");
            if (slot == EquipmentSlot.Longarm) return id.Contains("rifle") || id.Contains("shotgun") || id.Contains("longarm");
            if (slot == EquipmentSlot.Throwable) return id.Contains("grenade") || id.Contains("throw") || id.Contains("charge");
            if (slot == EquipmentSlot.Sidearm)
                return !(id.Contains("rifle") || id.Contains("shotgun") || id.Contains("longarm") || id.Contains("melee") || id.Contains("knife") || id.Contains("club") || id.Contains("axe") || id.Contains("grenade") || id.Contains("throw"));
            return false;
        }
        if (definition.Kind == ItemKind.Consumable) return slot is EquipmentSlot.QuickConsumable1 or EquipmentSlot.QuickConsumable2;
        if (definition.Kind == ItemKind.Container) return slot == EquipmentSlot.BackEquipment;
        if (definition.Kind != ItemKind.Armor) return false;
        if (id.Contains("underlayer")) return slot == EquipmentSlot.Underlayer;
        if (id.Contains("clothing")) return slot == EquipmentSlot.Clothing;
        if (id.Contains("respirator") || id.Contains("mask")) return slot == EquipmentSlot.Respirator;
        if (id.Contains("head") || id.Contains("helmet")) return slot == EquipmentSlot.Head;
        if (id.Contains("powered") || id.Contains("frame")) return slot == EquipmentSlot.PoweredFrame;
        if (id.Contains("arm")) return slot is EquipmentSlot.LeftArm or EquipmentSlot.RightArm;
        if (id.Contains("leg")) return slot is EquipmentSlot.LeftLeg or EquipmentSlot.RightLeg;
        if (id.Contains("utility")) return slot == EquipmentSlot.Utility;
        // Phase 21 armor family items are currently emitted as *.torso. Unknown armor
        // also falls back to torso, matching the existing inventory quick-equip policy.
        return slot == EquipmentSlot.Torso;
    }

    private static string EquipmentSlotLabel(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Sidearm => "Sidearm",
        EquipmentSlot.Longarm => "Longarm",
        EquipmentSlot.Melee => "Melee Weapon",
        EquipmentSlot.Throwable => "Throwable",
        EquipmentSlot.BackEquipment => "Back Equipment",
        EquipmentSlot.QuickConsumable1 => "Quick Aid 1",
        EquipmentSlot.QuickConsumable2 => "Quick Aid 2",
        EquipmentSlot.Underlayer => "Underlayer",
        EquipmentSlot.Clothing => "Clothing",
        EquipmentSlot.Torso => "Torso Armor",
        EquipmentSlot.LeftArm => "Left Arm",
        EquipmentSlot.RightArm => "Right Arm",
        EquipmentSlot.LeftLeg => "Left Leg",
        EquipmentSlot.RightLeg => "Right Leg",
        EquipmentSlot.Head => "Head",
        EquipmentSlot.Respirator => "Respirator",
        EquipmentSlot.Utility => "Utility",
        EquipmentSlot.PoweredFrame => "Powered Frame",
        _ => HumanizeId(slot.ToString())
    };

    private IReadOnlyList<string> BuildSingleEquipmentSummary(StableId itemId, string heading)
    {
        ItemInstanceState item = World.GetItem(itemId);
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        var lines = new List<string>
        {
            $"{heading}: {DisplayName(itemId)}",
            $"Type: {PlayerFacingKind(definition.Kind)}",
            $"Condition: {item.Condition.OverallPermille / 10.0:F1}%",
            $"Mass: {definition.MassGrams / 1000f:F2} kg",
            $"Value: {definition.BaseValueMinorUnits} scrip"
        };
        if (definition.Kind == ItemKind.Weapon && Content.WeaponFamilies.ContainsKey(item.DefinitionId))
        {
            WeaponCapabilitySnapshot weapon = CompileWeapon(itemId);
            lines.Add($"Damage: {weapon.Damage.Sum(static value => value.Amount)} | Range: {weapon.MaximumRangeMillimeters / 1000f:F1} m | Magazine: {weapon.MagazineCapacity}");
            lines.Add($"Reliability: {weapon.ReliabilityPermille / 10.0:F0}% | Recoil: {weapon.RecoilPermille / 10.0:F0}% | Spread: {weapon.SpreadPermille / 10.0:F0}%");
        }
        else if (Content.Armor.TryGetValue(item.DefinitionId, out ArmorItemDefinition? armor))
        {
            lines.Add($"Coverage: {string.Join(", ", armor.CoveredRegions.Order(StringComparer.Ordinal))}");
            lines.Add($"Protection threshold: {armor.Protection.Sum(static value => value.Threshold)} | Resistance: {ArmorResistanceAverage(armor):F0}%");
            lines.Add($"Environmental: {armor.EnvironmentalProtectionPermille / 10.0:F0}% | Mobility penalty: {armor.MobilityPenaltyPermille / 10.0:F0}% | Noise: {armor.NoisePermille / 10.0:F0}%");
        }
        return lines;
    }

    private static double ArmorResistanceAverage(ArmorItemDefinition armor)
        => armor.Protection.Count == 0 ? 0.0 : armor.Protection.Average(static value => value.ResistancePermille) / 10.0;

    private static string CompareNumber(string label, double current, double candidate, string suffix, bool lowerIsBetter, string format)
    {
        double delta = candidate - current;
        string direction = Math.Abs(delta) < 0.0001 ? "same" : ((delta > 0) ^ lowerIsBetter) ? "better" : "worse";
        string deltaText = delta >= 0 ? $"+{delta.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}" : delta.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        return $"{label}: {current.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}{suffix} -> {candidate.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}{suffix} ({deltaText}{suffix}, {direction})";
    }

    private static string PlayerFacingKind(ItemKind kind) => kind switch
    {
        ItemKind.Consumable => "Aid",
        ItemKind.Ammunition => "Ammunition",
        ItemKind.Component => "Component",
        ItemKind.Material => "Material",
        ItemKind.Miscellaneous => "Miscellaneous",
        _ => kind.ToString()
    };

    public InventoryUsage PlayerUsage => capacity.Calculate(World.GetInventory(PlayerId), World, Content.Items);

    public WeaponCapabilitySnapshot CompileWeapon(StableId itemId)
    {
        ItemInstanceState item = World.GetItem(itemId);
        if (!Content.WeaponFamilies.TryGetValue(item.DefinitionId, out WeaponFamilyDefinition? family)) throw new InvalidOperationException($"Item '{itemId}' is not a generated weapon family.");
        return new WeaponAssemblyCompiler().Compile(item, family, Content.WeaponComponents, World);
    }

    public WeaponDefinition CompileCombatDefinition(StableId itemId) => combatAdapter.CompileWeaponDefinition(CompileWeapon(itemId));

    public WeaponRuntimeState CreateCombatWeaponState(StableId itemId, StableId ownerId)
    {
        WeaponCapabilitySnapshot capability = CompileWeapon(itemId);
        ItemInstanceState item = World.GetItem(itemId);
        WeaponRuntimeState state = combatAdapter.CreateRuntimeState(item, ownerId, capability);
        state.ReserveAmmunition = CountCompatibleAmmunition(ownerId, itemId);
        return state;
    }

    public void SynchronizeCombatWeapon(WeaponRuntimeState runtime)
    {
        if (!World.TryGetItem(runtime.Id, out ItemInstanceState? item) || item is null) return;
        int structuralLoss = Math.Max(0, item.Condition.StructuralPermille - runtime.ConditionPermille);
        if (structuralLoss > 0) item.Condition.Damage(structuralLoss, structuralLoss / 2, structuralLoss / 3);
        if (item.AmmunitionLoad is not null)
        {
            int difference = runtime.Ammunition - item.AmmunitionLoad.LoadedCount;
            if (difference < 0) item.AmmunitionLoad.Unload(-difference);
            else if (difference > 0)
            {
                string? ammunitionId = ResolveAmmunitionDefinition(item.Id);
                if (ammunitionId is not null) item.AmmunitionLoad.Load(ammunitionId, difference);
            }
        }
        item.Touch();
        string? ammo = ResolveAmmunitionDefinition(item.Id);
        if (ammo is not null)
        {
            int authoritativeReserve = CountDefinition(PlayerId, ammo);
            int spent = Math.Max(0, authoritativeReserve - runtime.ReserveAmmunition);
            if (spent > 0) ConsumeDefinition(PlayerId, ammo, spent);
            runtime.ReserveAmmunition = CountDefinition(PlayerId, ammo);
        }
    }

    public void SynchronizeCombatArmor(CombatActorState actor)
    {
        foreach (ArmorRuntimeState layer in actor.Armor)
        {
            if (!World.TryGetItem(layer.Id, out ItemInstanceState? item) || item is null) continue;
            int loss = Math.Max(0, item.Condition.StructuralPermille - layer.ConditionPermille);
            if (loss > 0) { item.Condition.Damage(loss, loss / 3, loss / 4); item.Touch(); }
        }
    }

    public IReadOnlyList<ArmorRuntimeState> CompileCombatArmor(StableId actorId)
    {
        ArmorCapabilitySnapshot capability = new ArmorCapabilityCompiler().Compile(actorId, World, Content.Armor);
        return capability.Layers.Select(combatAdapter.CompileArmorLayer).ToArray();
    }

    public bool EquipActiveWeapon(StableId itemId)
    {
        if (itemId != SidearmId && itemId != RifleId && itemId != MeleeId) return false;
        ActiveWeaponId = itemId;
        LastStatus = $"equipped {DisplayName(itemId)}";
        return true;
    }

    public int CountCompatibleAmmunition(StableId ownerId, StableId weaponId)
    {
        string? ammunitionId = ResolveAmmunitionDefinition(weaponId);
        return ammunitionId is null ? 0 : CountDefinition(ownerId, ammunitionId);
    }

    public bool TryConsumeThrowable()
    {
        if (ThrowableStackId.IsEmpty || !World.TryGetItem(ThrowableStackId, out ItemInstanceState? item) || item is null || item.Quantity <= 0) return false;
        if (item.Quantity == 1) { World.DestroyItem(item.Id); ThrowableStackId = StableId.Empty; }
        else item.SetQuantity(item.Quantity - 1);
        LastStatus = "threw explosive charge";
        return true;
    }

    public CraftingResult Craft(string recipeId, IReadOnlyList<StableId> inputs, ulong tick)
    {
        // Gameplay menus pause the simulation, so several deliberate crafts can happen on the
        // same simulation tick.  Tick-only activity IDs therefore reused the same deterministic
        // craft-output IDs and the second successful craft crashed ItemWorldState.AddItem.
        // World.Revision is persisted and advances whenever a successful craft adds its output,
        // giving each craft attempt a deterministic state-relative identity even while paused.
        StableId activity = StableIdFactory.CreateChild(PlayerId, "craft-activity", $"{recipeId}:{tick}:{World.Revision}");
        StableId stationId = Content.Recipes.TryGetValue(recipeId, out CraftingRecipeDefinition? recipe) ? StationIdFor(recipe.Station) : WorkbenchId;
        CraftingResult result = Crafting.Execute(World, RecipeKnowledge, new CraftingPlan(activity, PlayerId, recipeId, stationId, inputs, tick, activity.High ^ activity.Low));
        LastStatus = result.Message;
        return result;
    }

    public RepairResult RepairItem(StableId targetItemId, IReadOnlyList<StableId> componentItemIds, ulong tick)
    {
        StableId planId = StableIdFactory.CreateChild(PlayerId, "repair-plan", $"{targetItemId}:{tick}");
        RepairResult result = Repair.Execute(World, new RepairPlan(planId, PlayerId, targetItemId, componentItemIds, null, WorkbenchId, SkillId.Engineering, 35, 180, 950));
        LastStatus = result.Message;
        return result;
    }

    public TradeExecutionResult ExecuteTrade(TradeQuote quote, ulong tick)
    {
        TradeExecutionResult result = Merchants.Execute(World, quote, tick);
        LastStatus = result.Message;
        return result;
    }


    public bool UnequipEquipment(EquipmentSlot slot, ulong tick = 0)
    {
        EquipmentLoadoutState loadout = World.GetLoadout(PlayerId);
        if (!loadout.TryGet(slot, out StableId itemId))
        {
            LastStatus = "equipment slot is empty";
            return false;
        }
        ItemTransactionResult result = Transactions.Execute(World, new ItemTransaction(
            StableIdFactory.CreateChild(PlayerId, "unequip-transaction", $"{slot}:{itemId}:{tick}"),
            tick,
            [new UnequipItemOperation(PlayerId, slot)],
            []));
        LastStatus = result.Message;
        return result.Status == ItemTransactionStatus.Committed;
    }

    public bool ActivateInventoryItem(StableId itemId)
    {
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null || item.Location.HolderId != PlayerId)
        {
            LastStatus = "item is unavailable";
            return false;
        }
        if (itemId == SidearmId || itemId == RifleId || itemId == MeleeId) return EquipActiveWeapon(itemId);
        if (Content.Consumables.ContainsKey(item.DefinitionId))
        {
            ConsumableUseResult used = Consumables.Use(World, itemId, PlayerId);
            LastStatus = used.Success ? $"used {DisplayName(itemId)}" : used.Message;
            return used.Success;
        }
        LastStatus = "item has no quick action";
        return false;
    }

    public bool EquipInventoryItem(StableId itemId, ulong tick)
    {
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null || item.Location.Kind != ItemLocationKind.Inventory || item.Location.HolderId != PlayerId)
        {
            LastStatus = "item is unavailable";
            return false;
        }
        if (itemId == SidearmId || itemId == RifleId || itemId == MeleeId) return EquipActiveWeapon(itemId);
        ItemDefinition definition = Content.Items.Get(item.DefinitionId);
        EquipmentSlot? slot = PreferredEquipmentSlot(definition, item.DefinitionId);
        if (!slot.HasValue || !EquipmentRules.CanEquip(definition, slot.Value))
        {
            LastStatus = "item cannot be equipped";
            return false;
        }
        ItemTransactionResult result = Transactions.Execute(World, new ItemTransaction(
            StableIdFactory.CreateChild(PlayerId, "equip-transaction", $"{itemId}:{slot}:{tick}:{World.Revision}"),
            tick,
            [new EquipItemOperation(PlayerId, itemId, slot.Value)],
            []));
        LastStatus = result.Status == ItemTransactionStatus.Committed ? $"equipped {DisplayName(itemId)}" : result.Message;
        if (result.Status == ItemTransactionStatus.Committed && definition.Kind == ItemKind.Weapon) ActiveWeaponId = itemId;
        return result.Status == ItemTransactionStatus.Committed;
    }

    public bool SplitInventoryStackHalf(StableId itemId, ulong tick)
    {
        if (!CanSplitInventoryItem(itemId))
        {
            LastStatus = "item stack cannot be split";
            return false;
        }
        ItemInstanceState item = World.GetItem(itemId);
        int quantity = Math.Max(1, item.Quantity / 2);
        StableId childId = StableIdFactory.CreateChild(itemId, "manual-split", $"{tick}:{World.Revision}");
        ItemTransactionResult result = Transactions.Execute(World, new ItemTransaction(
            StableIdFactory.CreateChild(PlayerId, "split-transaction", $"{itemId}:{tick}:{World.Revision}"),
            tick, [new SplitStackOperation(itemId, childId, quantity)], []));
        LastStatus = result.Status == ItemTransactionStatus.Committed ? $"split {DisplayName(itemId)} stack" : result.Message;
        return result.Status == ItemTransactionStatus.Committed;
    }

    public bool DropInventoryItem(StableId itemId, FixedItemPosition position, ulong tick)
    {
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null || item.Location.Kind != ItemLocationKind.Inventory || item.Location.HolderId != PlayerId)
        {
            LastStatus = "item is unavailable";
            return false;
        }
        if (Content.Items.Get(item.DefinitionId).Kind == ItemKind.Quest)
        {
            LastStatus = "quest items cannot be dropped";
            return false;
        }
        StableId worldId = StableIdFactory.Create("item-world", "active-wasteland");
        ItemTransactionResult result = Transactions.Execute(World, new ItemTransaction(
            StableIdFactory.CreateChild(PlayerId, "drop-transaction", $"{itemId}:{tick}:{World.Revision}"),
            tick, [new TransferItemOperation(itemId, ItemLocation.InWorld(worldId, position))], []));
        LastStatus = result.Status == ItemTransactionStatus.Committed ? $"dropped {DisplayName(itemId)}" : result.Message;
        return result.Status == ItemTransactionStatus.Committed;
    }

    private EquipmentSlot? PreferredEquipmentSlot(ItemDefinition definition, string definitionId)
    {
        EquipmentLoadoutState loadout = World.EnsureLoadout(PlayerId);
        bool Empty(EquipmentSlot slot) => !loadout.TryGet(slot, out _);
        string id = definitionId.ToLowerInvariant();
        if (definition.Kind == ItemKind.Weapon)
        {
            if (id.Contains("rifle") || id.Contains("shotgun") || id.Contains("longarm")) return EquipmentSlot.Longarm;
            if (id.Contains("melee") || id.Contains("knife") || id.Contains("club") || id.Contains("axe")) return EquipmentSlot.Melee;
            if (id.Contains("grenade") || id.Contains("charge") || id.Contains("throw")) return EquipmentSlot.Throwable;
            return EquipmentSlot.Sidearm;
        }
        if (definition.Kind == ItemKind.Consumable) return Empty(EquipmentSlot.QuickConsumable1) ? EquipmentSlot.QuickConsumable1 : EquipmentSlot.QuickConsumable2;
        if (definition.Kind == ItemKind.Container) return EquipmentSlot.BackEquipment;
        if (definition.Kind != ItemKind.Armor) return null;
        if (id.Contains("underlayer")) return EquipmentSlot.Underlayer;
        if (id.Contains("clothing")) return EquipmentSlot.Clothing;
        if (id.Contains("respirator") || id.Contains("mask")) return EquipmentSlot.Respirator;
        if (id.Contains("head") || id.Contains("helmet")) return EquipmentSlot.Head;
        if (id.Contains("powered") || id.Contains("frame")) return EquipmentSlot.PoweredFrame;
        if (id.Contains("arm")) return Empty(EquipmentSlot.LeftArm) ? EquipmentSlot.LeftArm : EquipmentSlot.RightArm;
        if (id.Contains("leg")) return Empty(EquipmentSlot.LeftLeg) ? EquipmentSlot.LeftLeg : EquipmentSlot.RightLeg;
        return EquipmentSlot.Torso;
    }

    public CraftingResult CraftKnownRecipe(string recipeId, ulong tick)
    {
        StableId[] inputs = World.GetInventory(PlayerId).ItemIds.Order().ToArray();
        return Craft(recipeId, inputs, tick);
    }

    public RepairResult RepairWithAvailableMaterial(StableId targetItemId, ulong tick)
    {
        ItemInstanceState? material = World.GetInventory(PlayerId).ItemIds.Select(World.GetItem)
            .Where(value => value.Id != targetItemId && (Content.Items.Get(value.DefinitionId).Kind is ItemKind.Material or ItemKind.Component))
            .OrderBy(static value => value.Id).FirstOrDefault();
        if (material is null) { LastStatus = "no compatible repair material"; return new RepairResult(false, false, LastStatus, 0); }
        StableId componentId = material.Id;
        if (material.Quantity > 1)
        {
            componentId = StableIdFactory.CreateChild(material.Id, "repair-unit", tick.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ItemTransactionResult split = Transactions.Execute(World, new ItemTransaction(
                StableIdFactory.CreateChild(PlayerId, "repair-split", $"{material.Id}:{tick}"), tick,
                [new SplitStackOperation(material.Id, componentId, 1)], []));
            if (split.Status != ItemTransactionStatus.Committed) { LastStatus = split.Message; return new RepairResult(false, false, split.Message, 0); }
        }
        return RepairItem(targetItemId, [componentId], tick);
    }

    public TradeExecutionResult BuyFromMerchant(StableId itemId, ulong tick) => TradeQuantity(itemId, merchantSells: true, 1, tick);

    public TradeExecutionResult SellToMerchant(StableId itemId, ulong tick) => TradeQuantity(itemId, merchantSells: false, 1, tick);

    public TradeExecutionResult BuyFromMerchant(StableId itemId, int quantity, ulong tick) => TradeQuantity(itemId, merchantSells: true, quantity, tick);

    public TradeExecutionResult SellToMerchant(StableId itemId, int quantity, ulong tick) => TradeQuantity(itemId, merchantSells: false, quantity, tick);

    private TradeExecutionResult TradeQuantity(StableId itemId, bool merchantSells, int quantity, ulong tick)
    {
        if (!World.TryGetItem(itemId, out ItemInstanceState? item) || item is null)
            return new TradeExecutionResult(false, "trade item unavailable", 0);
        StableId expectedOwner = merchantSells ? MerchantId : PlayerId;
        if (item.Location.HolderId != expectedOwner) return new TradeExecutionResult(false, "trade item is not held by the expected party", 0);
        int available = Math.Max(0, item.Quantity - World.ReservedQuantity(item.Id));
        if (available <= 0) return new TradeExecutionResult(false, "trade item is reserved or unavailable", 0);
        MerchantTradePreviewUiModel preview = BuildMerchantTradePreview(itemId, merchantSells, Math.Clamp(quantity, 1, available));
        int tradeQuantity = Math.Min(Math.Clamp(quantity, 1, available), preview.MaximumAffordableQuantity);
        if (tradeQuantity <= 0) return new TradeExecutionResult(false, merchantSells ? "insufficient scrip" : "merchant cannot afford that item", 0);
        SettlementEconomyState economy = World.GetEconomy(StableIdFactory.Create("settlement", "prospect-clinic"));
        int payment = preview.UnitPriceMinorUnits * tradeQuantity * (merchantSells ? 1 : -1);
        StableId quoteId = StableIdFactory.CreateChild(PlayerId, "trade-quote", $"{itemId}:{merchantSells}:{tradeQuantity}:{tick}:{World.Revision}");
        var quote = new TradeQuote(quoteId, MerchantId, PlayerId, [new TradeLine(itemId, tradeQuantity, preview.UnitPriceMinorUnits, merchantSells)], economy.CurrencyPolicy.CurrencyDefinitionId, payment, 0,
            World.GetInventory(MerchantId).Revision, World.GetInventory(PlayerId).Revision, tick + 300);
        return ExecuteTrade(quote, tick);
    }

    private static EconomyCommodity ResolveCommodity(ItemDefinition definition)
    {
        if (definition.Kind == ItemKind.Weapon) return EconomyCommodity.Weapons;
        if (definition.Kind == ItemKind.Armor) return EconomyCommodity.Armor;
        if (definition.Kind == ItemKind.Ammunition) return EconomyCommodity.Ammunition;
        if (definition.Kind == ItemKind.Consumable) return EconomyCommodity.Medicine;
        if (definition.Tags.Contains("chemical")) return EconomyCommodity.Chemicals;
        if (definition.Tags.Contains("textile")) return EconomyCommodity.Textiles;
        return EconomyCommodity.Metals;
    }

    public byte[] Save() => ItemCampaignSnapshotSerializer.WriteToBytes(new ItemCampaignSnapshot(World, RecipeKnowledge, ActiveWeaponId, ThrowableStackId));

    public void Load(ReadOnlyMemory<byte> snapshot)
    {
        ItemCampaignSnapshot restored = ItemCampaignSnapshotSerializer.ReadFromBytes(snapshot);
        World = restored.World;
        RecipeKnowledge = restored.RecipeKnowledge;
        ActiveWeaponId = restored.ActiveWeaponId;
        ThrowableStackId = restored.ThrowableStackId;
        ValidateRestoredRuntimeReferences();
        LastStatus = "item campaign restored";
    }

    private void InitializeRegressionCampaign(StableId playerId, CampaignSeed seed, StartingEquipmentGrant grant, int carryCapacityGrams)
    {
        PlayerId = playerId;
        World.EnsureInventory(playerId, carryCapacityGrams, Math.Max(25_000, carryCapacityGrams / 2));
        World.EnsureLoadout(playerId);
        _ = CreateStarterEquipment(playerId, seed, grant, carryCapacityGrams);

        SidearmId = EnsureGeneratedWeapon(playerId, seed, "sidearm", "weapon.self-loading-pistol");
        RifleId = EnsureGeneratedWeapon(playerId, new CampaignSeed(seed.Value + 1), "rifle", "weapon.hunting-rifle");
        MeleeId = EnsureGeneratedWeapon(playerId, new CampaignSeed(seed.Value + 2), "melee", "weapon.improvised-melee");
        ActiveWeaponId = SidearmId;

        StableId lightAmmo = AddStack(playerId, "ammo.light-cartridge", 90, "starter-light-ammunition");
        StableId rifleAmmo = AddStack(playerId, "ammo.rifle-cartridge", 120, "starter-rifle-ammunition");
        ThrowableStackId = AddStack(playerId, "ammo.explosive-charge", 5, "starter-explosive-charges");
        weaponAmmunition[SidearmId] = "ammo.light-cartridge";
        weaponAmmunition[RifleId] = "ammo.rifle-cartridge";
        _ = Ammunition.Load(World, SidearmId, lightAmmo, 10, StableIdFactory.CreateChild(playerId, "load", "sidearm"));
        _ = Ammunition.Load(World, RifleId, rifleAmmo, 6, StableIdFactory.CreateChild(playerId, "load", "rifle"));

        Equip(SidearmId, EquipmentSlot.Sidearm);
        Equip(RifleId, EquipmentSlot.Longarm);
        Equip(MeleeId, EquipmentSlot.Melee);
        Equip(AddSimple(playerId, "armor.field-underlayer", 1, "armor-underlayer"), EquipmentSlot.Underlayer);
        Equip(AddSimple(playerId, "armor.field-clothing", 1, "armor-clothing"), EquipmentSlot.Clothing);
        Equip(AddSimple(playerId, "armor.field-torso", 1, "armor-torso"), EquipmentSlot.Torso);
        Equip(AddSimple(playerId, "armor.field-arm", 1, "armor-left-arm"), EquipmentSlot.LeftArm);
        Equip(AddSimple(playerId, "armor.field-arm", 1, "armor-right-arm"), EquipmentSlot.RightArm);
        Equip(AddSimple(playerId, "armor.field-leg", 1, "armor-left-leg"), EquipmentSlot.LeftLeg);
        Equip(AddSimple(playerId, "armor.field-leg", 1, "armor-right-leg"), EquipmentSlot.RightLeg);
        Equip(AddSimple(playerId, "armor.field-head", 1, "armor-head"), EquipmentSlot.Head);
        Equip(AddSimple(playerId, "armor.field-respirator", 1, "armor-respirator"), EquipmentSlot.Respirator);
        Equip(AddSimple(playerId, "consumable.trauma-dressing", 2, "quick-trauma"), EquipmentSlot.QuickConsumable1);
        _ = AddStack(playerId, "material.scrap-steel", 8, "starter-scrap");
        _ = AddStack(playerId, "material.chemical-salt", 5, "starter-salts");
        _ = AddStack(playerId, "material.clean-cloth", 4, "starter-cloth");
        _ = AddStack(playerId, "material.medical-alcohol", 3, "starter-alcohol");
        _ = AddStack(playerId, "currency.diagnostic-scrip", 900, "starter-scrip");
        _ = AddSimple(playerId, "tool.field-kit", 1, "starter-field-kit");

        foreach (string recipe in new[] { "assemble-light-cartridges", "assemble-rifle-cartridges", "synthesize-trauma-dressing", "repair-sidearm" })
            RecipeKnowledge.Learn(new RecipeKnowledgeRecord(recipe, RecipeKnowledgeSource.Background, "relay-technician", 0));

        WorkbenchId = StableIdFactory.Create("station", "prospect-clinic-workbench");
        MedicalStationId = StableIdFactory.Create("station", "prospect-clinic-medical");
        DiagnosticTableId = StableIdFactory.Create("station", "prospect-clinic-diagnostic");
        EnsureRegressionEconomyStocks();
        InitializeMerchant(seed);
        LastStatus = "Phase 13 authority integrated";
    }


    private void EnsureRegressionEconomyStocks()
    {
        SettlementEconomyState economy = World.GetEconomy(StableIdFactory.Create("settlement", "prospect-clinic"));
        AddStockIfMissing(economy, EconomyCommodity.Food, 70_000, 100_000, 6);
        AddStockIfMissing(economy, EconomyCommodity.Fuel, 45_000, 90_000, 12);
        AddStockIfMissing(economy, EconomyCommodity.Metals, 60_000, 100_000, 4);
        AddStockIfMissing(economy, EconomyCommodity.Chemicals, 40_000, 80_000, 10);
        AddStockIfMissing(economy, EconomyCommodity.Electronics, 25_000, 70_000, 30);
        AddStockIfMissing(economy, EconomyCommodity.Textiles, 50_000, 80_000, 7);
        AddStockIfMissing(economy, EconomyCommodity.Weapons, 25_000, 75_000, 60);
        AddStockIfMissing(economy, EconomyCommodity.Armor, 20_000, 65_000, 55);
        AddStockIfMissing(economy, EconomyCommodity.LuxuryGoods, 15_000, 50_000, 45);
        AddStockIfMissing(economy, EconomyCommodity.DiagnosticMaterials, 18_000, 60_000, 40);
    }

    private static void AddStockIfMissing(SettlementEconomyState economy, EconomyCommodity commodity, long quantity, long target, int basePrice)
    {
        if (!economy.Stocks.ContainsKey(commodity)) economy.AddStock(new CommodityStockState(commodity, quantity, target, basePrice));
    }

    private void ValidateRestoredRuntimeReferences()
    {
        if (!World.TryGetInventory(PlayerId, out _)) throw new InvalidDataException("Restored item campaign does not contain the player inventory.");
        if (!World.TryGetInventory(MerchantId, out _)) throw new InvalidDataException("Restored item campaign does not contain the merchant inventory.");
        foreach (StableId weaponId in new[] { SidearmId, RifleId, MeleeId })
            if (!World.TryGetItem(weaponId, out _)) throw new InvalidDataException($"Restored item campaign is missing required weapon {weaponId}.");
        if (!ActiveWeaponId.IsEmpty && !World.TryGetItem(ActiveWeaponId, out _)) ActiveWeaponId = SidearmId;
        if (!ThrowableStackId.IsEmpty && !World.TryGetItem(ThrowableStackId, out _)) ThrowableStackId = StableId.Empty;
    }

    private void InitializeMerchant(CampaignSeed seed)
    {
        MerchantId = StableIdFactory.Create("merchant", "prospect-clinic-quartermaster");
        World.EnsureInventory(MerchantId, int.MaxValue, int.MaxValue);
        _ = AddStack(MerchantId, "currency.diagnostic-scrip", 5_000, "merchant-currency");
        _ = AddStack(MerchantId, "ammo.light-cartridge", 80, "merchant-light-ammo");
        _ = AddStack(MerchantId, "ammo.rifle-cartridge", 60, "merchant-rifle-ammo");
        _ = AddStack(MerchantId, "consumable.trauma-dressing", 8, "merchant-dressings");
        _ = AddStack(MerchantId, "material.scrap-steel", 12, "merchant-scrap");
        _ = EnsureGeneratedWeapon(MerchantId, new CampaignSeed(seed.Value + 100), "merchant-revolver", "weapon.revolver");
    }

    private StableId EnsureGeneratedWeapon(StableId ownerId, CampaignSeed seed, string key, string familyId)
    {
        ItemInstanceState? existing = World.GetInventory(ownerId).ItemIds.Select(World.GetItem).FirstOrDefault(item => StringComparer.Ordinal.Equals(item.DefinitionId, familyId));
        if (existing is not null) return existing.Id;
        StableId id = StableIdFactory.CreateChild(ownerId, "integrated-weapon", key);
        Factory.CreateGeneratedWeapon(World, id, seed, familyId, ItemLocation.InInventory(ownerId));
        return id;
    }

    private StableId AddSimple(StableId ownerId, string definitionId, int quantity, string key)
    {
        StableId id = StableIdFactory.CreateChild(ownerId, "integrated-item", key);
        if (World.TryGetItem(id, out _)) return id;
        ItemInstanceState item = Factory.CreateSimple(id, definitionId, ItemLocation.InInventory(ownerId), quantity);
        World.AddItem(item); World.EnsureInventory(ownerId).Add(id); return id;
    }

    private StableId AddStack(StableId ownerId, string definitionId, int quantity, string key) => AddSimple(ownerId, definitionId, quantity, key);

    private void Equip(StableId itemId, EquipmentSlot slot)
    {
        StableId actorId = World.GetItem(itemId).Location.HolderId;
        ItemTransactionResult result = Transactions.Execute(World, new ItemTransaction(
            StableIdFactory.CreateChild(actorId, "equip-transaction", $"{slot}:{itemId}"),
            0,
            [new EquipItemOperation(actorId, itemId, slot)],
            []));
        if (result.Status != ItemTransactionStatus.Committed) throw new InvalidOperationException(result.Message);
    }

    private string? ResolveAmmunitionDefinition(StableId weaponId)
    {
        if (weaponAmmunition.TryGetValue(weaponId, out string? explicitId)) return explicitId;
        if (!World.TryGetItem(weaponId, out ItemInstanceState? weapon) || weapon is null) return null;
        string? accepted = weapon.RuntimeTags.FirstOrDefault(static tag => tag.StartsWith("accepts:", StringComparison.Ordinal));
        string? tag = accepted?["accepts:".Length..];
        if (tag is null) return null;
        return Content.Ammunition.Values.Where(ammo => ammo.CompatibilityTags.Contains(tag)).OrderBy(static ammo => ammo.Id, StringComparer.Ordinal).Select(static ammo => ammo.Id).FirstOrDefault();
    }

    private int CountDefinition(StableId ownerId, string definitionId) => World.GetInventory(ownerId).ItemIds.Select(World.GetItem).Where(item => StringComparer.Ordinal.Equals(item.DefinitionId, definitionId)).Sum(item => item.Quantity - World.ReservedQuantity(item.Id));

    private void ConsumeDefinition(StableId ownerId, string definitionId, int quantity)
    {
        int remaining = quantity;
        foreach (ItemInstanceState stack in World.GetInventory(ownerId).ItemIds.Select(World.GetItem).Where(item => StringComparer.Ordinal.Equals(item.DefinitionId, definitionId)).OrderBy(static item => item.Id).ToArray())
        {
            int used = Math.Min(remaining, stack.Quantity - World.ReservedQuantity(stack.Id));
            if (used <= 0) continue;
            if (used == stack.Quantity) World.DestroyItem(stack.Id); else stack.SetQuantity(stack.Quantity - used);
            remaining -= used; if (remaining == 0) break;
        }
    }

    private string DisplayName(StableId itemId)
    {
        ItemInstanceState item = World.GetItem(itemId);
        return item.CustomName ?? item.Blueprint?.DisplayIdentity ?? Content.Items.Get(item.DefinitionId).DisplayName;
    }

    private sealed class FixedItemSkillCapabilityProvider(int value) : IItemSkillCapabilityProvider
    {
        public int GetEffectiveSkill(StableId actorId, SkillId skill) => value;
    }
}
