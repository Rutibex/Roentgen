namespace Roentgen.World.Structures;

public enum SettlementScale { Outpost = 0, Hamlet = 1, Village = 2, Town = 3 }
public enum FoundingPurpose { MedicalRefuge = 0, TradeStop = 1, IndustrialRecovery = 2, AgriculturalCollective = 3, DefensivePost = 4, ResearchEnclave = 5 }
public enum ResourceAnchorKind { Water = 0, Food = 1, Power = 2, Salvage = 3, Medicine = 4 }
public enum DefensiveStrategy { OpenTrade = 0, Walled = 1, Chokepoint = 2, Elevated = 3, Concealed = 4 }
public enum DistrictKind { Civic = 0, Market = 1, Residential = 2, Workshop = 3, Medical = 4, Agriculture = 5, Defensive = 6, Industrial = 7, Waste = 8, Ideological = 9 }
public enum BuildingPurpose { Clinic = 0, Residence = 1, BoardingHouse = 2, Workshop = 3, Warehouse = 4, MarketShelter = 5, GuardPost = 6, IndustrialIntake = 7, UtilityRelay = 8, GenericShelter = 9 }
public enum StructuralSystemKind { ScrapFrame = 0, ReinforcedConcrete = 1, IndustrialSteel = 2, MasonryBlock = 3 }
public enum RoomArchetype { Entrance = 0, Corridor = 1, Reception = 2, Treatment = 3, Storage = 4, Staff = 5, SecureStorage = 6, Living = 7, Sleeping = 8, Kitchen = 9, Workshop = 10, Warehouse = 11, Market = 12, Guard = 13, Utility = 14, Control = 15, Machinery = 16, Maintenance = 17, Sanitation = 18, Stairwell = 19, RoofAccess = 20, ExteriorYard = 21 }
public enum AccessClass { Public = 0, Staff = 1, Secure = 2, Service = 3, Hazardous = 4 }
public enum PrivacyClass { Open = 0, SemiPrivate = 1, Private = 2, Restricted = 3 }
public enum ConnectionRequirement { Required = 0, Preferred = 1, Permitted = 2, Forbidden = 3, ServiceOnly = 4, EmergencyOnly = 5, Secret = 6 }
public enum ConnectionKind { Door = 0, OpenArch = 1, Stair = 2, Ladder = 3, Breach = 4, Exterior = 5 }
public enum LockPolicy { None = 0, Optional = 1, LockedWithAlternative = 2, SkillGateWithAlternative = 3, NarrativeReserved = 4 }
public enum DoorLifecycleState { Open = 0, Closed = 1, Locked = 2, Jammed = 3, Barricaded = 4, Destroyed = 5, Missing = 6 }
public enum VerticalLinkKind { Stair = 0, Ladder = 1, Ramp = 2, CollapsedDrop = 3 }
public enum DamageEventType { Fire = 0, Blast = 1, Flooding = 2, ChemicalExposure = 3, RadiographicAlteration = 4, FoundationSubsidence = 5, RoofCollapse = 6, WallBreach = 7, StructuralFatigue = 8, Looting = 9, Barricading = 10, PartialRepair = 11, ImprovisedExpansion = 12 }
public enum ReservationKind { CriticalItem = 0, NpcConversation = 1, ObjectiveVolume = 2, ObservationPoint = 3, CombatZone = 4, AmbushZone = 5, Evidence = 6, Terminal = 7, LockedRouteGate = 8, TechnicalBypass = 9, MedicalSolution = 10, SecretEntrance = 11, EscapeRoute = 12, LootContainer = 13, OccupantAnchor = 14 }
public enum StructureMaterialKind { ScrapMetal = 0, CorrugatedMetal = 1, ReinforcedConcrete = 2, IndustrialSteel = 3, Masonry = 4, Timber = 5, Glass = 6, Roofing = 7, Rubble = 8, Hazard = 9 }
public enum StructurePrimitiveKind { Foundation = 0, Floor = 1, Wall = 2, Ceiling = 3, Roof = 4, Column = 5, Beam = 6, Stair = 7, Railing = 8, Ramp = 9, Debris = 10 }
public enum StructureLifecycleState { Unrequested = 0, BlueprintResident = 1, ShellRequested = 2, GeneratingShell = 3, ShellResident = 4, CriticalInteriorRequested = 5, CriticalInteriorResident = 6, DecorationRequested = 7, FullyResident = 8, Dormant = 9, Unloading = 10, Failed = 11, Cancelled = 12 }
public enum StructureValidationSeverity { Information = 0, Warning = 1, Error = 2, Critical = 3 }
public enum StructureRepairKind { SnapOpening = 0, ResizeRoom = 1, WidenCorridor = 2, ReconnectRoom = 3, MoveDoorway = 4, AddPassage = 5, AddVerticalLink = 6, RemoveDebris = 7, RelocateReservation = 8, AddSupport = 9, ReduceDamage = 10, RegenerateCluster = 11, RegenerateFloor = 12, SafeFallback = 13 }
public enum StructureCellLayer { ExteriorShell = 0, CriticalInterior = 1, Decoration = 2 }
