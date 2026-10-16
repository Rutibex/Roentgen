using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Generation;

namespace Roentgen.World.Structures;

public sealed class SettlementPlanCompiler
{
    public const string GeneratorId = "roentgen.world.structure.settlement-plan";
    public const int SchemaVersion = 1;
    private readonly RegionalTopology _topology;

    public SettlementPlanCompiler(RegionalTopology topology) => _topology = topology ?? throw new ArgumentNullException(nameof(topology));

    public SettlementPlan Compile(LandmarkReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.Purpose != LandmarkPurpose.Settlement) throw new ArgumentException("A settlement plan requires a settlement reservation.", nameof(reservation));
        StableId id = StableIdFactory.CreateChild(reservation.Id, "settlement-plan", SchemaVersion);
        DomainSeed domain = GenerationDomain.Derive(_topology.CampaignSeed, $"settlement.{reservation.Id}.program");
        SettlementPhysicalProgram program = CreateProgram(id, reservation);
        var decisions = new List<GenerationDecision>();
        var districts = CreateDistricts(id, reservation, decisions);
        var streets = CreateStreets(id, reservation);
        var parcels = CreateParcels(id, reservation, districts, program, decisions);
        var provenance = new GenerationProvenance(id, reservation.Id, _topology.CampaignSeed, domain, GeneratorId, SchemaVersion, new[] { "physical-program-valid", "districts-reserved", "road-interface-valid" }, Array.Empty<string>(), Decisions: decisions);
        return new SettlementPlan(id, reservation, program, districts, streets, parcels, provenance);
    }

    private SettlementPhysicalProgram CreateProgram(StableId planId, LandmarkReservation reservation)
    {
        StableId sourceRoot = StableIdFactory.CreateChild(planId, "resources", 0);
        return new SettlementPhysicalProgram(
            StableIdFactory.CreateChild(planId, "program", 0), SettlementScale.Village, FoundingPurpose.MedicalRefuge,
            new[] { new ResourceAnchor(ResourceAnchorKind.Water, StableIdFactory.CreateChild(sourceRoot,"water",0), "condensers and filtered cistern"), new ResourceAnchor(ResourceAnchorKind.Food, StableIdFactory.CreateChild(sourceRoot,"food",0), "covered hydroponic beds"), new ResourceAnchor(ResourceAnchorKind.Power, StableIdFactory.CreateChild(sourceRoot,"power",0), "salvaged diagnostic generator"), new ResourceAnchor(ResourceAnchorKind.Medicine, reservation.Id, "Prospect Clinic") },
            DefensiveStrategy.Chokepoint,
            new[] { new DistrictRequirement(DistrictKind.Medical,1,true), new DistrictRequirement(DistrictKind.Market,1,true), new DistrictRequirement(DistrictKind.Residential,3,true), new DistrictRequirement(DistrictKind.Workshop,1,true), new DistrictRequirement(DistrictKind.Defensive,1,true), new DistrictRequirement(DistrictKind.Industrial,1,false) },
            new[] { new ServiceRequirement(BuildingPurpose.Clinic,1,true), new ServiceRequirement(BuildingPurpose.MarketShelter,1,true), new ServiceRequirement(BuildingPurpose.Workshop,1,true), new ServiceRequirement(BuildingPurpose.Residence,4,true), new ServiceRequirement(BuildingPurpose.GuardPost,1,true), new ServiceRequirement(BuildingPurpose.Warehouse,1,false) },
            new ArchitecturalPalette(StructuralSystemKind.ScrapFrame, StructureMaterialKind.CorrugatedMetal, StructureMaterialKind.ReinforcedConcrete, "prospect-clinic-recovery"),
            new HistoricalDamageProfile(180, new[] { DamageEventType.Fire, DamageEventType.Looting, DamageEventType.PartialRepair }));
    }

    private static IReadOnlyList<DistrictPlan> CreateDistricts(StableId planId, LandmarkReservation reservation, ICollection<GenerationDecision> decisions)
    {
        int w = reservation.HalfWidthCentimeters * 2; int d = reservation.HalfDepthCentimeters * 2;
        int minX = -w/2; int minZ = -d/2; int maxX = w/2; int maxZ = d/2;
        var result = new[]
        {
            new DistrictPlan(StableIdFactory.CreateChild(planId,"district","medical"), DistrictKind.Medical, new StructureRect(minX, minZ, 0, 0), 100),
            new DistrictPlan(StableIdFactory.CreateChild(planId,"district","market"), DistrictKind.Market, new StructureRect(0, minZ, maxX, 0), 90),
            new DistrictPlan(StableIdFactory.CreateChild(planId,"district","residential"), DistrictKind.Residential, new StructureRect(minX, 0, 0, maxZ), 70),
            new DistrictPlan(StableIdFactory.CreateChild(planId,"district","workshop"), DistrictKind.Workshop, new StructureRect(0, 0, maxX, maxZ), 80),
            new DistrictPlan(StableIdFactory.CreateChild(planId,"district","defensive"), DistrictKind.Defensive, new StructureRect(-500, minZ, 500, minZ + 1600), 110)
        };
        decisions.Add(new GenerationDecision("district-layout", "cross-axis", $"{result.Length} physical districts"));
        return result;
    }

    private static IReadOnlyList<StreetSegment> CreateStreets(StableId planId, LandmarkReservation reservation)
    {
        int minX=-reservation.HalfWidthCentimeters, maxX=reservation.HalfWidthCentimeters, minZ=-reservation.HalfDepthCentimeters, maxZ=reservation.HalfDepthCentimeters;
        return new[]
        {
            new StreetSegment(StableIdFactory.CreateChild(planId,"street","main"), new StructurePoint(0,0,minZ), new StructurePoint(0,0,maxZ), 250, true),
            new StreetSegment(StableIdFactory.CreateChild(planId,"street","cross"), new StructurePoint(minX,0,0), new StructurePoint(maxX,0,0), 180, false),
            new StreetSegment(StableIdFactory.CreateChild(planId,"street","service-east"), new StructurePoint(1400,0,minZ/2), new StructurePoint(1400,0,maxZ), 120, false)
        };
    }

    private static IReadOnlyList<SettlementParcel> CreateParcels(StableId planId, LandmarkReservation reservation, IReadOnlyList<DistrictPlan> districts, SettlementPhysicalProgram program, ICollection<GenerationDecision> decisions)
    {
        int baseY=reservation.TargetElevationCentimeters;
        DistrictPlan medical=districts.First(static d=>d.Kind==DistrictKind.Medical); DistrictPlan market=districts.First(static d=>d.Kind==DistrictKind.Market); DistrictPlan residential=districts.First(static d=>d.Kind==DistrictKind.Residential); DistrictPlan workshop=districts.First(static d=>d.Kind==DistrictKind.Workshop); DistrictPlan defensive=districts.First(static d=>d.Kind==DistrictKind.Defensive);
        var specs = new (string,BuildingPurpose,StructureRect,StableId,bool,bool)[]
        {
            ("Prospect Clinic",BuildingPurpose.Clinic,new StructureRect(-3400,-3600,-600,-800),medical.Id,true,false),
            ("Covered Market",BuildingPurpose.MarketShelter,new StructureRect(700,-3300,3300,-900),market.Id,false,false),
            ("Salvage Workshop",BuildingPurpose.Workshop,new StructureRect(700,700,3100,3000),workshop.Id,false,false),
            ("North Residence",BuildingPurpose.Residence,new StructureRect(-3300,800,-1900,2300),residential.Id,false,false),
            ("West Residence",BuildingPurpose.Residence,new StructureRect(-1700,800,-300,2300),residential.Id,false,false),
            ("Boarding House",BuildingPurpose.BoardingHouse,new StructureRect(-3300,2500,-900,4100),residential.Id,true,false),
            ("Gatehouse",BuildingPurpose.GuardPost,new StructureRect(-600,-4600,600,-3500),defensive.Id,false,false),
            ("Utility Store",BuildingPurpose.Warehouse,new StructureRect(700,3200,3300,4700),workshop.Id,false,false),
            ("Expansion Parcel A",BuildingPurpose.GenericShelter,new StructureRect(-500,2600,700,3900),residential.Id,true,true),
            ("Expansion Parcel B",BuildingPurpose.GenericShelter,new StructureRect(1800,-200,3400,700),workshop.Id,true,true)
        };
        var parcels = specs.Select((spec,index)=>new SettlementParcel(StableIdFactory.CreateChild(planId,"parcel",index),spec.Item1,spec.Item2,spec.Item3,baseY,index%4,spec.Item4,spec.Item5,spec.Item6)).ToArray();
        decisions.Add(new GenerationDecision("parcel-allocation", "service-first", $"{parcels.Length} parcels; {parcels.Count(static p=>p.MayRemainEmpty)} expansion"));
        return parcels;
    }
}
