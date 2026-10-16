using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World.Structures;

public sealed class GeneratedStructureWorld : ICanonicalWritable
{
    public GeneratedStructureWorld(StableId id, RegionalTopology topology, SettlementPlan settlement, IReadOnlyList<StructureBlueprint> structures)
    {
        if(id.IsEmpty) throw new ArgumentException("Structure-world ID cannot be empty.",nameof(id));
        Id=id; Topology=topology??throw new ArgumentNullException(nameof(topology)); Settlement=settlement??throw new ArgumentNullException(nameof(settlement)); Structures=structures.OrderBy(static structure=>structure.Id).ToArray();
        if(Structures.Count==0) throw new ArgumentException("A generated structure world must contain structures.",nameof(structures));
    }
    public StableId Id{get;}
    public RegionalTopology Topology{get;}
    public SettlementPlan Settlement{get;}
    public IReadOnlyList<StructureBlueprint> Structures{get;}
    public StateChecksum ContentChecksum{get{var writer=new CanonicalWriter();WriteCanonical(writer);return writer.ComputeChecksum();}}
    public void WriteCanonical(CanonicalWriter writer){writer.WriteStableId(Id);writer.WriteChecksum(Topology.ContentChecksum);writer.WriteChecksum(Settlement.ContentChecksum);writer.WriteCount(Structures.Count);foreach(StructureBlueprint structure in Structures)writer.WriteChecksum(structure.ContentChecksum);}
}

public sealed class MicroRegionStructureCompiler
{
    private readonly RegionalTopology _topology;
    public MicroRegionStructureCompiler(RegionalTopology topology)=>_topology=topology??throw new ArgumentNullException(nameof(topology));

    public GeneratedStructureWorld Compile()
    {
        LandmarkReservation settlementSite=Find(LandmarkPurpose.Settlement,"Prospect Clinic");
        SettlementPlan settlement=new SettlementPlanCompiler(_topology).Compile(settlementSite);
        var compiler=new StructureBlueprintCompiler(_topology); var structures=new List<StructureBlueprint>();
        foreach(SettlementParcel parcel in settlement.Parcels.Where(static parcel=>!parcel.MayRemainEmpty)) structures.Add(compiler.Compile(settlementSite,parcel,StructureGrammarCatalog.Get(parcel.Purpose)));
        LandmarkReservation ruin=Find(LandmarkPurpose.Ruin,"Blackwater Intake"); structures.Add(compiler.CompileStandalone(ruin,BuildingPurpose.IndustrialIntake));
        LandmarkReservation relay=Find(LandmarkPurpose.Infrastructure,"Meridian Relay"); structures.Add(compiler.CompileStandalone(relay,BuildingPurpose.UtilityRelay));
        StableId id=StableIdFactory.CreateChild(_topology.Id,"structure-world",1);
        return new GeneratedStructureWorld(id,_topology,settlement,structures);
    }

    private LandmarkReservation Find(LandmarkPurpose purpose,string preferredName)
    {
        LandmarkReservation? exact=_topology.Reservations.FirstOrDefault(reservation=>reservation.Purpose==purpose&&reservation.Name.Contains(preferredName,StringComparison.OrdinalIgnoreCase));
        return exact??_topology.Reservations.First(reservation=>reservation.Purpose==purpose);
    }
}

public sealed record StructureBatchValidationSummary(int StructuresGenerated,int StructuresValidAfterRepair,int StructuresUsingFallback,int CriticalFailures,int TotalRooms,int TotalPrimitives,StateChecksum AggregateChecksum)
{
    public bool Passed=>CriticalFailures==0;
}

public sealed class StructureBatchValidator
{
    public StructureBatchValidationSummary Validate(CampaignSeed startingSeed,int structureTarget)
    {
        if(structureTarget<1||structureTarget>100_000)throw new ArgumentOutOfRangeException(nameof(structureTarget));
        int generated=0,valid=0,fallback=0,failures=0,rooms=0,primitives=0; var checksumWriter=new CanonicalWriter(); int seedIndex=0;
        while(generated<structureTarget)
        {
            CampaignSeed seed=new(unchecked(startingSeed.Value+(ulong)seedIndex++));
            RegionalTopology topology=new RegionalTopologyGenerator(seed).Generate(WorldGenerationProfile.MicroRegion);
            GeneratedStructureWorld world=new MicroRegionStructureCompiler(topology).Compile();
            foreach(StructureBlueprint structure in world.Structures)
            {
                if(generated>=structureTarget)break; generated++; rooms+=structure.Rooms.Count; primitives+=structure.Primitives.Count;
                StructureValidationReport report=new StructureValidator().Validate(structure);
                if(!report.HasCriticalIssues)valid++;else failures++;
                if(structure.Provenance.RepairHistory.Any(static entry=>entry.Contains("fallback",StringComparison.OrdinalIgnoreCase)))fallback++;
                checksumWriter.WriteChecksum(structure.ContentChecksum);
            }
        }
        return new StructureBatchValidationSummary(generated,valid,fallback,failures,rooms,primitives,checksumWriter.ComputeChecksum());
    }
}
