namespace Roentgen.Audio;

public enum AudioEventKind
{
    None,
    WeaponFire,
    WeaponDryFire,
    WeaponJam,
    ReloadStart,
    ReloadMechanism,
    ReloadComplete,
    ProjectileFlight,
    Impact,
    ArmorImpact,
    BiologicalImpact,
    MechanicalImpact,
    MeleeSwing,
    MeleeContact,
    Explosion,
    Injury,
    Death,
    FootContact,
    Landing,
    DoorOpen,
    DoorClose,
    DoorLock,
    DoorUnlock,
    DoorBreach,
    ContainerOpen,
    ContainerClose,
    Pickup,
    Drop,
    Equip,
    Unequip,
    TerminalUse,
    Craft,
    Repair,
    MedicalAction,
    MachineStart,
    MachineStop,
    MachineLoad,
    MachineMalfunction,
    WeatherChanged,
    RegionChanged,
    AcousticZoneChanged,
    QuestAccepted,
    ObjectiveAdvanced,
    Revelation,
    IrreversibleChoice,
    CompanionCue,
    EndingTransition,
    InterfaceConfirm,
    InterfaceCancel,
    InterfaceError,
    InterfaceWarning,
    JournalUpdate,
    FocusEnter,
    FocusTarget,
    FocusPartSelected,
    FocusActionQueued,
    FocusInsufficient,
    FocusCommit,
    FocusExit,
    InterfaceNavigate,
    InterfaceTab,
    InterfaceTransaction,
    InterfaceCraft
}

public enum AudioEmitterKind { Transient, Character, Creature, Weapon, Machine, Environment, Interface, Focus }
public enum AudioRuntimeState { Stopped, Starting, Running, Paused, SilentFallback, Faulted, Disposed }
public enum AudioSceneDomain { Global, Region, Resident, Focus, Music }
public enum AudioMixSnapshotKind { Gameplay, PauseMenu, Dialogue, FocusPlanning, FocusExecution, Loading, Muted }
public enum AudioQuality { RealtimeLow, RealtimeHigh }
public enum AudioDynamicRange { Wide, Standard, Night, ReducedSuddenSounds }
public enum AudioSurface { Unknown, FusedGlass, Asphalt, IndustrialMud, ContaminatedSediment, PaleScrub, Rock, Concrete, Metal, Wood, ShallowWater, Armor, Biological, Mechanical }
public enum AudioFootwear { Bare, Boot, Armored, Claw, Hoof }
public enum AudioGait { Sneak, Walk, Run, Sprint, Land }
public enum AudioRegion { GlassBasin, OssuaryFields, BlackwaterWorks, BrokenMeridian, RedQuarryCountry, QuietRange }
public enum AudioLifecycleKind { Started, Rejected, Completed, Stolen, Stopped, Faulted }
