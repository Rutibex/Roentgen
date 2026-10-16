using Roentgen.Campaign;
using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Music;
using Roentgen.Simulation;

namespace Roentgen.App;

public sealed class Phase19MusicBridge
{
    private readonly StableId _regionId = StableIdFactory.Create("music-region", "broken-meridian");
    private readonly StableId _companionId;
    private MusicSituation _lastStinger = MusicSituation.Silence;
    private ulong _lastStingerTick;

    public Phase19MusicBridge(StableId companionId) => _companionId = companionId;

    public void Update(IMusicRuntime runtime, PlayerPresentationSnapshot player, CombatPresentationSnapshot combat, Phase17CampaignRuntime campaign, bool dialogueOpen, bool focusActive)
    {
        ulong tick = player.Tick;
        CombatFeedbackEntry? latest = combat.RecentFeedback.OrderByDescending(static value => value.Tick).FirstOrDefault();
        ulong age = latest is null || latest.Tick > tick ? ulong.MaxValue : tick - latest.Tick;
        float combatIntensity = age switch
        {
            <= 15 => 0.82f,
            <= 60 => 0.55f,
            <= 180 => 0.25f,
            _ => 0f
        };
        MusicThreatLevel threat = combatIntensity switch
        {
            >= 0.8f => MusicThreatLevel.Engaged,
            >= 0.5f => MusicThreatLevel.Alerted,
            >= 0.2f => MusicThreatLevel.Suspicious,
            _ => MusicThreatLevel.None
        };
        MusicCampaignMoment moment = campaign.Campaign.Completed ? MusicCampaignMoment.Ending
            : campaign.Campaign.ConvergenceResolution.HasValue ? MusicCampaignMoment.Convergence
            : campaign.Campaign.Commitments.Count > 0 ? MusicCampaignMoment.Commitment
            : MusicCampaignMoment.Ordinary;
        var context = new MusicContext(
            _regionId,
            StableId.Empty,
            StableId.Empty,
            _companionId,
            player.Player.OpenContainerId.IsEmpty ? MusicLocationKind.Exterior : MusicLocationKind.Interior,
            threat,
            combatIntensity,
            combat.MaximumHealth <= 0 ? 1f : Math.Clamp(combat.Health / (float)combat.MaximumHealth, 0f, 1f),
            player.Player.Stance == PlayerStance.Crouched && combatIntensity < 0.25f,
            combatIntensity >= 0.5f,
            dialogueOpen,
            focusActive,
            MusicDiscoveryKind.None,
            moment,
            12,
            MusicWeatherKind.Clear,
            0f,
            tick);
        runtime.UpdateContext(context);

        MusicSituation stinger = moment switch
        {
            MusicCampaignMoment.Ending => MusicSituation.Ending,
            MusicCampaignMoment.Convergence => MusicSituation.FinalConvergence,
            _ => MusicSituation.Silence
        };
        if (stinger != MusicSituation.Silence && (stinger != _lastStinger || tick - _lastStingerTick > 600))
        {
            runtime.RequestStinger(stinger, campaign.Campaign.Plan.Id, tick);
            _lastStinger = stinger;
            _lastStingerTick = tick;
        }
    }
}
