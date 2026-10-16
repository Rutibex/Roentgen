using Roentgen.Campaign;
using Roentgen.Contracts;
using Roentgen.Factions;
using Roentgen.Narrative;

namespace Roentgen.Simulation;

/// <summary>
/// Simulation-facing Phase 17 facade. It keeps the campaign director presentation-neutral and
/// coordinates campaign transitions only at fixed simulation boundaries.
/// </summary>
public sealed class Phase17CampaignRuntime
{
    private readonly CampaignDirector director = new();

    public Phase17CampaignRuntime(
        CampaignWorldState campaign,
        NarrativeWorldState narrative,
        SocietyWorldState society,
        StableId playerId)
    {
        Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        Narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
        Society = society ?? throw new ArgumentNullException(nameof(society));
        if (playerId.IsEmpty) throw new ArgumentException("Campaign runtime requires a player ID.", nameof(playerId));
        PlayerId = playerId;
    }

    public CampaignWorldState Campaign { get; private set; }
    public NarrativeWorldState Narrative { get; }
    public SocietyWorldState Society { get; }
    public StableId PlayerId { get; }
    public CampaignUpdateResult LastUpdate { get; private set; } = new([], [], [], [], false, []);

    public CampaignUpdateResult Initialize(ulong tick, ulong day)
    {
        LastUpdate = director.Initialize(Campaign, Narrative, Society, PlayerId, tick, day);
        return LastUpdate;
    }

    public CampaignUpdateResult Update(ulong tick, ulong day)
    {
        LastUpdate = director.Update(Campaign, Narrative, Society, PlayerId, tick, day);
        return LastUpdate;
    }

    public CampaignCommitmentRecord CommitChoice(
        StableId choiceId,
        string optionId,
        IEnumerable<StableId> knownFactIds,
        ulong tick,
        ulong day) =>
        director.CommitChoice(Campaign, Society, choiceId, optionId, PlayerId, knownFactIds, tick, day);

    public RevelationRuntimeState CollectEvidence(StableId revelationId, StableId evidenceId, ulong tick) =>
        director.CollectEvidence(Campaign, revelationId, evidenceId, PlayerId, tick);

    public void ResolveConvergence(ConvergenceForm form, ulong tick) =>
        director.ResolveConvergence(Campaign, form, PlayerId, tick);

    public IReadOnlyList<CriticalRoleReplacementRecord> RepairCriticalRoles(CriticalRoleRepairContext context, ulong tick) =>
        director.RepairCriticalRoles(Campaign, context, tick);

    public byte[] SaveCampaignState() => Campaign.ToCanonicalPayload();

    public void LoadCampaignState(ReadOnlyMemory<byte> payload)
    {
        CampaignWorldState restored = CampaignWorldState.ReadCanonical(payload);
        if (restored.Plan.Id != Campaign.Plan.Id)
            throw new InvalidDataException("Loaded campaign state belongs to a different generated campaign.");
        Campaign = restored;
    }
}
