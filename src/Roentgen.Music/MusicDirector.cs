namespace Roentgen.Music;

public sealed class MusicDirector
{
    private readonly MusicSettings _settings;
    private MusicSituation _current = MusicSituation.Silence;
    private ulong _enteredTick;
    private ulong _lastTransitionTick;
    private float _combatMemory;

    public MusicDirector(MusicSettings settings) => _settings = settings.Validate();

    public MusicSituation Current => _current;

    public MusicSituation Evaluate(MusicContext context)
    {
        _combatMemory = Math.Max(context.CombatIntensity, _combatMemory * 0.965f);
        MusicSituation candidate = Score(context);
        if (_current == MusicSituation.Silence)
        {
            Accept(candidate, context.SimulationTick);
            return _current;
        }

        if (candidate == _current) return _current;
        ulong dwell = context.SimulationTick - _enteredTick;
        ulong cooldown = context.SimulationTick - _lastTransitionTick;
        bool urgent = candidate is MusicSituation.EliteCombat or MusicSituation.FinalConvergence or MusicSituation.Revelation or MusicSituation.Tragedy or MusicSituation.Ending;
        if (!urgent && (dwell < (ulong)_settings.MinimumSituationDwellTicks || cooldown < (ulong)_settings.TransitionCooldownTicks)) return _current;
        Accept(candidate, context.SimulationTick);
        return _current;
    }

    public MusicTransitionQuantization SelectQuantization(MusicSituation from, MusicSituation to) => to switch
    {
        MusicSituation.EliteCombat or MusicSituation.FinalConvergence => MusicTransitionQuantization.NextBeat,
        MusicSituation.LowCombat or MusicSituation.FullCombat or MusicSituation.Detected => MusicTransitionQuantization.NextBar,
        MusicSituation.Revelation or MusicSituation.Tragedy or MusicSituation.Victory => MusicTransitionQuantization.NextBeat,
        MusicSituation.Ending or MusicSituation.Credits => MusicTransitionQuantization.ExplicitMarker,
        MusicSituation.Dialogue => MusicTransitionQuantization.Immediate,
        _ => MusicTransitionQuantization.NextPhrase
    };

    private MusicSituation Score(MusicContext context)
    {
        if (context.CampaignMoment == MusicCampaignMoment.Ending) return MusicSituation.Ending;
        if (context.CampaignMoment == MusicCampaignMoment.Convergence) return MusicSituation.FinalConvergence;
        if (context.CampaignMoment == MusicCampaignMoment.Revelation || context.Discovery == MusicDiscoveryKind.Revelation) return MusicSituation.Revelation;
        if (context.IsInDialogue) return MusicSituation.Dialogue;
        if (_combatMemory >= _settings.EliteCombatThreshold || context.Threat == MusicThreatLevel.Overwhelming) return MusicSituation.EliteCombat;
        if (_combatMemory >= _settings.FullCombatThreshold || context.Threat == MusicThreatLevel.Engaged) return MusicSituation.FullCombat;
        if (_combatMemory >= _settings.CombatEntryThreshold) return MusicSituation.LowCombat;
        if (context.IsDetected) return MusicSituation.Detected;
        if (context.IsStealthy) return MusicSituation.Stealth;
        if (context.Discovery == MusicDiscoveryKind.Major) return MusicSituation.Discovery;
        if (context.LocationKind == MusicLocationKind.TradeHub) return MusicSituation.TradeHub;
        if (context.LocationKind == MusicLocationKind.Settlement) return context.Threat >= MusicThreatLevel.Suspicious ? MusicSituation.UneasySettlement : MusicSituation.SafeSettlement;
        if (context.LocationKind is MusicLocationKind.Interior or MusicLocationKind.Ruin or MusicLocationKind.Bunker or MusicLocationKind.Medical or MusicLocationKind.Industrial) return MusicSituation.InteriorExploration;
        return MusicSituation.WastelandExploration;
    }

    private void Accept(MusicSituation situation, ulong tick)
    {
        _current = situation;
        _enteredTick = tick;
        _lastTransitionTick = tick;
        if (situation is not (MusicSituation.LowCombat or MusicSituation.FullCombat or MusicSituation.EliteCombat)) _combatMemory = Math.Min(_combatMemory, _settings.CombatExitThreshold);
    }
}
