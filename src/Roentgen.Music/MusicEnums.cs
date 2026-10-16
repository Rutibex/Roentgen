namespace Roentgen.Music;

public enum MusicSituation
{
    Silence,
    Title,
    CharacterCreation,
    WastelandExploration,
    InteriorExploration,
    SafeSettlement,
    UneasySettlement,
    TradeHub,
    Suspicion,
    Stealth,
    Detected,
    LowCombat,
    FullCombat,
    EliteCombat,
    FinalConvergence,
    Escape,
    Dialogue,
    Discovery,
    Revelation,
    Tragedy,
    Victory,
    Ending,
    Credits
}

public enum MusicThreatLevel { None, Suspicious, Alerted, Engaged, Overwhelming }
public enum MusicLocationKind { Unknown, Exterior, Interior, Settlement, TradeHub, Ruin, Bunker, Medical, Industrial, FinalSite }
public enum MusicWeatherKind { Clear, Wind, Rain, Storm, Ash, Contamination }
public enum MusicDiscoveryKind { None, Minor, Major, Revelation }
public enum MusicCampaignMoment { Ordinary, Opening, Crisis, Revelation, Commitment, Convergence, Ending }
public enum MusicTransitionQuantization { Immediate, NextBeat, NextBar, NextTwoBars, NextPhrase, EndOfLoop, EndOfSection, ExplicitMarker }
public enum MusicTransitionKind { Crossfade, HarmonicPivot, PercussionPickup, SustainedWash, Accelerate, Decelerate, DissonantRupture, CadentialRelease, DiagnosticPulse, SilenceAndReentry, HardCut }
public enum MusicStemKind { Foundation, Pulse, Percussion, Melody, Texture, Danger }
public enum MusicRuntimeState { Stopped, Starting, Running, Paused, SilentFallback, Faulted, Disposed }
public enum MusicDynamicRange { Wide, Standard, Night }
