using Roentgen.Contracts;
using Roentgen.Factions;
using FactionReputationDimension = Roentgen.Factions.ReputationDimension;
using Roentgen.Items;
using Roentgen.Narrative;

namespace Roentgen.Simulation;

/// <summary>
/// Commits narrative consequences into authoritative item and society state.  The router
/// validates the complete batch before mutation and restores both domains if a commit fails.
/// Optional handlers provide the narrow integration points owned by later presentation or
/// progression hosts without introducing Narrative-to-App dependencies.
/// </summary>
public sealed class Phase16DomainEffectRouter : INarrativeExternalEffectRouter
{
    private readonly SocietyWorldState society;
    private readonly ItemWorldState items;
    private readonly Func<NarrativeExternalEffectRequest, NarrativeEffectReceipt?>? optionalHandler;

    public Phase16DomainEffectRouter(
        SocietyWorldState society,
        ItemWorldState items,
        Func<NarrativeExternalEffectRequest, NarrativeEffectReceipt?>? optionalHandler = null)
    {
        this.society = society ?? throw new ArgumentNullException(nameof(society));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.optionalHandler = optionalHandler;
    }

    public IReadOnlyList<NarrativeEffectReceipt> PrepareAndCommit(IReadOnlyList<NarrativeExternalEffectRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        NarrativeExternalEffectRequest[] ordered = requests.OrderBy(static request => request.Id).ToArray();
        var validation = ordered.Select(Validate).ToArray();
        if (validation.Any(static receipt => !receipt.Success)) return validation;

        byte[] societyCheckpoint = society.ToCanonicalPayload();
        byte[] itemCheckpoint = items.ToCanonicalPayload();
        var receipts = new List<NarrativeEffectReceipt>(ordered.Length);
        try
        {
            foreach (NarrativeExternalEffectRequest request in ordered)
            {
                NarrativeEffectReceipt receipt = Commit(request);
                if (!receipt.Success) throw new InvalidOperationException(receipt.Message);
                receipts.Add(receipt);
            }
            return receipts;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException or OverflowException)
        {
            society.RestoreFrom(societyCheckpoint);
            items.RestoreFrom(itemCheckpoint);
            return ordered.Select(request => new NarrativeEffectReceipt(request.Id, false, $"Narrative consequence transaction rolled back: {exception.Message}", 0)).ToArray();
        }
    }

    private NarrativeEffectReceipt Validate(NarrativeExternalEffectRequest request)
    {
        try
        {
            switch (request.Kind)
            {
                case NarrativeEffectKind.ChangeReputation:
                    RequireNonEmpty(request.ResponsibleActorId, "reputation subject");
                    _ = society.GetFaction(request.TargetId);
                    _ = ParseReputationDimension(request.Argument);
                    break;
                case NarrativeEffectKind.ChangeRelationship:
                    _ = society.GetFaction(request.TargetId);
                    RequireValueId(request, "target faction");
                    _ = society.GetFaction(request.Value.StableIdValue);
                    _ = ParseRelationshipDimension(request.Argument);
                    break;
                case NarrativeEffectKind.RecordCrime:
                    _ = society.GetFaction(request.TargetId);
                    RequireNonEmpty(request.ResponsibleActorId, "suspected actor");
                    if (request.Value.Kind is not NarrativeValueKind.StableId and not NarrativeValueKind.Location) throw new InvalidOperationException("Crime consequence requires a location value.");
                    _ = ParseCrimeKind(request.Argument);
                    break;
                case NarrativeEffectKind.ChangeServiceState:
                    _ = FindService(request.TargetId);
                    if (request.Value.Kind != NarrativeValueKind.Boolean && request.Amount == 0) throw new InvalidOperationException("Service-state consequence requires a Boolean value or nonzero amount.");
                    break;
                case NarrativeEffectKind.AlterTradeRoute:
                    _ = society.GetTradeRoute(request.TargetId);
                    if (!IsRouteOperation(request.Argument)) throw new InvalidOperationException($"Unknown trade-route operation '{request.Argument}'.");
                    break;
                case NarrativeEffectKind.TransferItem:
                    _ = items.GetItem(request.TargetId);
                    RequireValueId(request, "destination inventory owner");
                    break;
                case NarrativeEffectKind.ConsumeItem:
                    ItemInstanceState item = items.GetItem(request.TargetId);
                    int quantity = Math.Max(1, request.Amount);
                    if (quantity > item.Quantity) throw new InvalidOperationException("Narrative consequence cannot consume more items than exist.");
                    break;
                default:
                    if (optionalHandler is null) return new NarrativeEffectReceipt(request.Id, false, $"No domain handler is registered for {request.Kind}.", 0);
                    break;
            }
            return new NarrativeEffectReceipt(request.Id, true, "validated", CombinedRevision());
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return new NarrativeEffectReceipt(request.Id, false, exception.Message, CombinedRevision());
        }
    }

    private NarrativeEffectReceipt Commit(NarrativeExternalEffectRequest request)
    {
        switch (request.Kind)
        {
            case NarrativeEffectKind.ChangeReputation:
                FactionReputationState reputation = society.GetOrCreateReputation(request.ResponsibleActorId, request.TargetId);
                reputation.Adjust(ParseReputationDimension(request.Argument), request.Amount);
                society.Touch();
                return Success(request, "Faction reputation changed.");
            case NarrativeEffectKind.ChangeRelationship:
                FactionRelationshipState relationship = society.GetRelationship(request.TargetId, request.Value.StableIdValue);
                AdjustRelationship(relationship, ParseRelationshipDimension(request.Argument), request.Amount);
                society.Touch();
                return Success(request, "Directed faction relationship changed.");
            case NarrativeEffectKind.RecordCrime:
                var crime = new CrimeRecordState(request.Id, ParseCrimeKind(request.Argument), request.TargetId, request.Value.StableIdValue, request.ResponsibleActorId, Math.Clamp(Math.Abs(request.Amount), 0, 1000), society.Day);
                crime.SetIdentityConfidence(1000);
                crime.Report();
                society.AddCrime(crime);
                return Success(request, "Jurisdictional crime recorded.");
            case NarrativeEffectKind.ChangeServiceState:
                FindService(request.TargetId).SetOperating(request.Value.Kind == NarrativeValueKind.Boolean ? request.Value.BooleanValue : request.Amount > 0);
                society.Touch();
                return Success(request, "Settlement service state changed.");
            case NarrativeEffectKind.AlterTradeRoute:
                ApplyRouteChange(society.GetTradeRoute(request.TargetId), request);
                society.Touch();
                return Success(request, "Trade route changed.");
            case NarrativeEffectKind.TransferItem:
                TransferItem(request.TargetId, request.Value.StableIdValue);
                return Success(request, "Item transferred.");
            case NarrativeEffectKind.ConsumeItem:
                ConsumeItem(request.TargetId, Math.Max(1, request.Amount));
                return Success(request, "Item consumed.");
            default:
                return optionalHandler?.Invoke(request) ?? throw new InvalidOperationException($"No domain handler is registered for {request.Kind}.");
        }
    }

    private void TransferItem(StableId itemId, StableId destinationOwnerId)
    {
        ItemInstanceState item = items.GetItem(itemId);
        if (item.Location.Kind == ItemLocationKind.Inventory && items.TryGetInventory(item.Location.HolderId, out InventoryState? source) && source is not null) source.Remove(itemId);
        InventoryState destination = items.EnsureInventory(destinationOwnerId);
        destination.Add(itemId);
        item.SetLocation(ItemLocation.InInventory(destinationOwnerId));
    }

    private void ConsumeItem(StableId itemId, int quantity)
    {
        ItemInstanceState item = items.GetItem(itemId);
        if (quantity >= item.Quantity) items.DestroyItem(itemId); else item.SetQuantity(item.Quantity - quantity);
    }

    private SettlementServiceState FindService(StableId serviceId) => society.Settlements.SelectMany(static settlement => settlement.Services).FirstOrDefault(service => service.Id == serviceId) ?? throw new KeyNotFoundException($"Unknown settlement service '{serviceId}'.");

    private static void ApplyRouteChange(TradeRouteRuntimeState route, NarrativeExternalEffectRequest request)
    {
        switch (request.Argument.Trim().ToLowerInvariant())
        {
            case "activate": route.SetActive(true); break;
            case "deactivate":
            case "suspend": route.SetActive(false); break;
            case "risk": route.AdjustRisk(request.Amount); break;
            case "escort": route.SetEscort(request.Amount); break;
            default: throw new InvalidOperationException($"Unknown trade-route operation '{request.Argument}'.");
        }
    }

    private static bool IsRouteOperation(string value) => value.Trim().ToLowerInvariant() is "activate" or "deactivate" or "suspend" or "risk" or "escort";
    private static FactionReputationDimension ParseReputationDimension(string value) => Enum.TryParse(value, true, out FactionReputationDimension result) ? result : throw new InvalidOperationException($"Unknown reputation dimension '{value}'.");
    private static CrimeKind ParseCrimeKind(string value) => Enum.TryParse(value, true, out CrimeKind result) ? result : throw new InvalidOperationException($"Unknown crime kind '{value}'.");
    private static string ParseRelationshipDimension(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        return normalized is "trust" or "fear" or "respect" or "compatibility" or "dependency" or "grievance" or "border-tension" or "trade-reliance" or "obligation" or "intelligence-confidence" ? normalized : throw new InvalidOperationException($"Unknown relationship dimension '{value}'.");
    }
    private static void AdjustRelationship(FactionRelationshipState relationship, string dimension, int amount)
    {
        switch (dimension)
        {
            case "trust": relationship.Adjust(trust: amount); break;
            case "fear": relationship.Adjust(fear: amount); break;
            case "respect": relationship.Adjust(respect: amount); break;
            case "compatibility": relationship.Adjust(compatibility: amount); break;
            case "dependency": relationship.Adjust(dependency: amount); break;
            case "grievance": relationship.Adjust(grievance: amount); break;
            case "border-tension": relationship.Adjust(borderTension: amount); break;
            case "trade-reliance": relationship.Adjust(tradeReliance: amount); break;
            case "obligation": relationship.Adjust(obligation: amount); break;
            case "intelligence-confidence": relationship.Adjust(intelligenceConfidence: amount); break;
        }
    }
    private static void RequireValueId(NarrativeExternalEffectRequest request, string name)
    {
        if (request.Value.Kind is not NarrativeValueKind.StableId and not NarrativeValueKind.Location || request.Value.StableIdValue.IsEmpty) throw new InvalidOperationException($"Narrative consequence requires a nonempty {name} stable ID.");
    }
    private static void RequireNonEmpty(StableId id, string name) { if (id.IsEmpty) throw new InvalidOperationException($"Narrative consequence requires a nonempty {name} stable ID."); }
    private NarrativeEffectReceipt Success(NarrativeExternalEffectRequest request, string message) => new(request.Id, true, message, CombinedRevision());
    private ulong CombinedRevision() => society.Revision ^ (items.Revision << 1);
}
