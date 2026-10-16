using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Navigation;
using Roentgen.World;

namespace Roentgen.AI;

public enum ScheduleActivityKind
{
    Sleep = 0,
    Eat = 1,
    Work = 2,
    Patrol = 3,
    Guard = 4,
    Trade = 5,
    Socialize = 6,
    Repair = 7,
    Travel = 8,
    Rest = 9,
    MedicalTreatment = 10,
    CompanionDuty = 11
}

public sealed record AgentScheduleEntry(
    string Id,
    int StartMinute,
    int DurationMinutes,
    ScheduleActivityKind Activity,
    StableId DestinationId,
    NavigationLocation Destination,
    int Priority,
    IReadOnlySet<string> Conditions) : ICanonicalWritable
{
    public int EndMinute => (StartMinute + DurationMinutes) % 1440;
    public bool IsActive(int minute)
    {
        minute = ((minute % 1440) + 1440) % 1440;
        if (DurationMinutes >= 1440) return true;
        int end = StartMinute + DurationMinutes;
        return end <= 1440 ? minute >= StartMinute && minute < end : minute >= StartMinute || minute < end - 1440;
    }
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id); writer.WriteInt32(StartMinute); writer.WriteInt32(DurationMinutes); writer.WriteInt32((int)Activity);
        writer.WriteStableId(DestinationId); writer.WriteInt32((int)Destination.Space); writer.WriteInt32(Destination.WorldPosition.XCentimeters);
        writer.WriteInt32(Destination.WorldPosition.ZCentimeters); writer.WriteInt32(Destination.ElevationCentimeters);
        writer.WriteStableId(Destination.StructureId); writer.WriteStableId(Destination.CellId); writer.WriteInt32(Priority);
        writer.WriteCount(Conditions.Count); foreach (string condition in Conditions.Order(StringComparer.Ordinal)) writer.WriteString(condition);
    }
}

public sealed class AgentSchedule : ICanonicalWritable
{
    public AgentSchedule(string id, IEnumerable<AgentScheduleEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); Id = id;
        Entries = entries.OrderBy(static value => value.StartMinute).ThenByDescending(static value => value.Priority).ThenBy(static value => value.Id, StringComparer.Ordinal).ToArray();
        if (Entries.Count == 0) throw new ArgumentException("A schedule requires at least one entry.", nameof(entries));
    }
    public string Id { get; }
    public IReadOnlyList<AgentScheduleEntry> Entries { get; }
    public AgentScheduleEntry Resolve(int minute, IReadOnlySet<string>? facts = null)
    {
        IReadOnlySet<string> available = facts ?? new HashSet<string>(StringComparer.Ordinal);
        return Entries.Where(value => value.IsActive(minute) && value.Conditions.All(available.Contains))
            .OrderByDescending(static value => value.Priority).ThenBy(static value => value.Id, StringComparer.Ordinal)
            .FirstOrDefault() ?? Entries.OrderBy(value => CircularDistance(value.StartMinute, minute)).ThenByDescending(static value => value.Priority).ThenBy(static value => value.Id, StringComparer.Ordinal).First();
    }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteCount(Entries.Count); foreach (AgentScheduleEntry entry in Entries) entry.WriteCanonical(writer); }
    private static int CircularDistance(int left, int right) { int difference = Math.Abs(left - right); return Math.Min(difference, 1440 - difference); }
}

public sealed class AgentSimulationRecord : ICanonicalWritable
{
    public AgentSimulationRecord(StableId agentId, AgentSchedule schedule, NavigationLocation location)
    {
        AgentId = agentId; Schedule = schedule; Location = location;
    }
    public StableId AgentId { get; }
    public AgentSchedule Schedule { get; }
    public NavigationLocation Location { get; set; }
    public NavigationLocation TravelOrigin { get; set; }
    public NavigationLocation TravelDestination { get; set; }
    public ulong TravelStartedTick { get; set; }
    public ulong TravelArrivalTick { get; set; }
    public string CurrentScheduleEntryId { get; set; } = string.Empty;
    public ScheduleActivityKind CurrentActivity { get; set; }
    public AgentSimulationTier Tier { get; set; } = AgentSimulationTier.Full;
    public int HealthPermille { get; set; } = 1000;
    public int HungerPermille { get; set; }
    public int FatiguePermille { get; set; }
    public ulong LastNeedsTick { get; set; }
    public ulong LastStrategicTick { get; set; }
    public bool InAbstractCombat { get; set; }
    public ulong Revision { get; set; }
    public bool IsTravelling(ulong tick) => TravelArrivalTick > tick;
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(AgentId); Schedule.WriteCanonical(writer); WriteLocation(writer, Location); WriteLocation(writer, TravelOrigin); WriteLocation(writer, TravelDestination);
        writer.WriteUInt64(TravelStartedTick); writer.WriteUInt64(TravelArrivalTick); writer.WriteString(CurrentScheduleEntryId); writer.WriteInt32((int)CurrentActivity);
        writer.WriteInt32((int)Tier); writer.WriteInt32(HealthPermille); writer.WriteInt32(HungerPermille); writer.WriteInt32(FatiguePermille); writer.WriteUInt64(LastNeedsTick); writer.WriteUInt64(LastStrategicTick); writer.WriteBoolean(InAbstractCombat); writer.WriteUInt64(Revision);
    }
    private static void WriteLocation(CanonicalWriter writer, NavigationLocation value)
    {
        writer.WriteInt32((int)value.Space); writer.WriteInt32(value.WorldPosition.XCentimeters); writer.WriteInt32(value.WorldPosition.ZCentimeters);
        writer.WriteInt32(value.ElevationCentimeters); writer.WriteStableId(value.StructureId); writer.WriteStableId(value.CellId);
    }
}

public sealed class AgentSimulationRegistry : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, AgentSimulationRecord> records = [];
    public IReadOnlyCollection<AgentSimulationRecord> Records => records.Values;
    public ulong Revision { get; private set; }
    public void Add(AgentSimulationRecord record) { if (!records.TryAdd(record.AgentId, record)) throw new InvalidOperationException($"Simulation record '{record.AgentId}' already exists."); Revision++; }
    public AgentSimulationRecord Get(StableId id) => records.TryGetValue(id, out AgentSimulationRecord? value) ? value : throw new KeyNotFoundException($"Unknown simulation record '{id}'.");
    public bool TryGet(StableId id, out AgentSimulationRecord? value) => records.TryGetValue(id, out value);
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteUInt64(Revision); writer.WriteCount(records.Count); foreach (AgentSimulationRecord record in records.Values) record.WriteCanonical(writer); }
}

public sealed record SimulationTierThresholds(int FullRadiusMillimeters = 90_000, int ReducedRadiusMillimeters = 400_000, int HysteresisMillimeters = 15_000);

public sealed class AgentSimulationTierService
{
    private readonly SimulationTierThresholds thresholds;
    public AgentSimulationTierService(SimulationTierThresholds? thresholds = null) => this.thresholds = thresholds ?? new SimulationTierThresholds();
    public AgentSimulationTier Resolve(AgentSimulationRecord record, FixedVector3 playerPosition)
    {
        long x = (long)record.Location.WorldPosition.XCentimeters * 10 - playerPosition.XMillimeters;
        long z = (long)record.Location.WorldPosition.ZCentimeters * 10 - playerPosition.ZMillimeters;
        double distance = Math.Sqrt((double)x * x + (double)z * z);
        return record.Tier switch
        {
            AgentSimulationTier.Full when distance <= thresholds.FullRadiusMillimeters + thresholds.HysteresisMillimeters => AgentSimulationTier.Full,
            AgentSimulationTier.Reduced when distance < thresholds.FullRadiusMillimeters - thresholds.HysteresisMillimeters => AgentSimulationTier.Full,
            AgentSimulationTier.Reduced when distance <= thresholds.ReducedRadiusMillimeters + thresholds.HysteresisMillimeters => AgentSimulationTier.Reduced,
            AgentSimulationTier.Strategic when distance < thresholds.ReducedRadiusMillimeters - thresholds.HysteresisMillimeters => AgentSimulationTier.Reduced,
            _ when distance <= thresholds.FullRadiusMillimeters => AgentSimulationTier.Full,
            _ when distance <= thresholds.ReducedRadiusMillimeters => AgentSimulationTier.Reduced,
            _ => AgentSimulationTier.Strategic
        };
    }

    public void Apply(AgentSimulationRecord record, AgentBrainState brain, CharacterState? character, AgentSimulationTier tier, Func<FixedVector3, NavigationLocation>? resolveLocation = null)
    {
        if (record.Tier == tier && brain.Tier == tier) return;
        if (character is not null && record.Tier == AgentSimulationTier.Full)
            record.Location = resolveLocation?.Invoke(character.FeetPosition)
                ?? NavigationLocation.Outdoor(new WorldPoint(checked((int)(character.FeetPosition.XMillimeters / 10)), checked((int)(character.FeetPosition.ZMillimeters / 10))), checked((int)(character.FeetPosition.YMillimeters / 10)));
        record.Tier = tier; brain.Tier = tier; record.Revision++; brain.Revision++;
        if (character is not null)
        {
            character.Lod = tier switch { AgentSimulationTier.Full => CharacterLodLevel.Full, AgentSimulationTier.Reduced => CharacterLodLevel.Reduced, _ => CharacterLodLevel.Distant };
            if (tier == AgentSimulationTier.Full)
                character.FeetPosition = new FixedVector3((long)record.Location.WorldPosition.XCentimeters * 10, (long)record.Location.ElevationCentimeters * 10, (long)record.Location.WorldPosition.ZCentimeters * 10);
            character.Revision++;
        }
    }
}

public sealed class ReducedAgentSimulation
{
    public void Advance(AgentSimulationRecord record, AgentBrainState brain, ulong tick, int minuteOfDay)
    {
        if (record.Tier == AgentSimulationTier.Full) return;
        AgentScheduleEntry entry = record.Schedule.Resolve(minuteOfDay);
        if (!string.Equals(record.CurrentScheduleEntryId, entry.Id, StringComparison.Ordinal))
        {
            record.CurrentScheduleEntryId = entry.Id; record.CurrentActivity = entry.Activity;
            BeginTravel(record, entry.Destination, tick, record.Tier == AgentSimulationTier.Strategic ? 4800 : 3200);
            record.Revision++;
        }
        else if (record.TravelArrivalTick == 0 && record.Location != entry.Destination)
        {
            BeginTravel(record, entry.Destination, tick, record.Tier == AgentSimulationTier.Strategic ? 4800 : 3200);
            record.Revision++;
        }
        if (record.IsTravelling(tick))
        {
            int progress = checked((int)((tick - record.TravelStartedTick) * 1000 / Math.Max(1UL, record.TravelArrivalTick - record.TravelStartedTick)));
            record.Location = Interpolate(record.TravelOrigin, record.TravelDestination, progress);
        }
        else if (record.TravelArrivalTick > 0)
        {
            record.Location = record.TravelDestination; record.TravelArrivalTick = 0; record.Revision++;
        }
        AdvanceNeeds(record, tick);
        brain.HungerPermille = record.HungerPermille; brain.FatiguePermille = record.FatiguePermille; brain.Revision++;
    }

    private static void AdvanceNeeds(AgentSimulationRecord record, ulong tick)
    {
        if (record.LastNeedsTick == 0) record.LastNeedsTick = tick;
        if (tick <= record.LastNeedsTick) return;
        ulong steps = (tick - record.LastNeedsTick) / 300UL;
        if (steps == 0) return;
        int boundedSteps = checked((int)Math.Min(1000UL, steps));
        int hungerDelta = record.CurrentActivity == ScheduleActivityKind.Eat ? -20 * boundedSteps : boundedSteps;
        int fatigueDelta = record.CurrentActivity is ScheduleActivityKind.Sleep or ScheduleActivityKind.Rest ? -25 * boundedSteps : boundedSteps;
        record.HungerPermille = Math.Clamp(record.HungerPermille + hungerDelta, 0, 1000);
        record.FatiguePermille = Math.Clamp(record.FatiguePermille + fatigueDelta, 0, 1000);
        record.LastNeedsTick = checked(record.LastNeedsTick + steps * 300UL);
        record.Revision++;
    }

    private static void BeginTravel(AgentSimulationRecord record, NavigationLocation destination, ulong tick, int speedMillimetersPerSecond)
    {
        long dx = ((long)destination.WorldPosition.XCentimeters - record.Location.WorldPosition.XCentimeters) * 10;
        long dz = ((long)destination.WorldPosition.ZCentimeters - record.Location.WorldPosition.ZCentimeters) * 10;
        long distance = (long)Math.Round(Math.Sqrt((double)dx * dx + (double)dz * dz), MidpointRounding.AwayFromZero);
        ulong duration = checked((ulong)Math.Max(1, distance * 60 / Math.Max(1, speedMillimetersPerSecond)));
        record.TravelOrigin = record.Location; record.TravelDestination = destination; record.TravelStartedTick = tick; record.TravelArrivalTick = checked(tick + duration);
    }

    private static NavigationLocation Interpolate(NavigationLocation a, NavigationLocation b, int progressPermille)
    {
        progressPermille = Math.Clamp(progressPermille, 0, 1000);
        int x = a.WorldPosition.XCentimeters + (b.WorldPosition.XCentimeters - a.WorldPosition.XCentimeters) * progressPermille / 1000;
        int z = a.WorldPosition.ZCentimeters + (b.WorldPosition.ZCentimeters - a.WorldPosition.ZCentimeters) * progressPermille / 1000;
        int y = a.ElevationCentimeters + (b.ElevationCentimeters - a.ElevationCentimeters) * progressPermille / 1000;
        return new NavigationLocation(progressPermille < 1000 ? NavigationSpaceKind.AbstractRoute : b.Space, new WorldPoint(x, z), y, progressPermille < 1000 ? StableId.Empty : b.StructureId, progressPermille < 1000 ? StableId.Empty : b.CellId);
    }
}

/// <summary>Coarse deterministic simulation for distant individual actors and travelling groups.</summary>
public sealed class StrategicAgentSimulation
{
    private readonly ReducedAgentSimulation reduced = new();

    public void Advance(AgentSimulationRecord record, AgentBrainState brain, ulong tick, int minuteOfDay)
    {
        if (record.Tier != AgentSimulationTier.Strategic) return;
        reduced.Advance(record, brain, tick, minuteOfDay);
        if (record.LastStrategicTick == 0) record.LastStrategicTick = tick;
        if (tick <= record.LastStrategicTick) return;
        ulong steps = (tick - record.LastStrategicTick) / 600UL;
        if (steps == 0) return;
        int boundedSteps = checked((int)Math.Min(1000UL, steps));

        if (record.InAbstractCombat)
        {
            int pressure = 2 + checked((int)((record.AgentId.High ^ record.AgentId.Low ^ (tick / 600UL)) % 7UL));
            record.HealthPermille = Math.Max(0, record.HealthPermille - pressure * boundedSteps);
            brain.FearPermille = Math.Clamp(brain.FearPermille + pressure * 3 * boundedSteps, 0, 1000);
            if (record.HealthPermille <= 150 || brain.FearPermille >= 850)
            {
                record.InAbstractCombat = false;
                brain.Disposition = AgentDisposition.Fleeing;
                brain.CommitAction("abstract-retreat", tick, 600);
            }
        }
        else if (record.CurrentActivity is ScheduleActivityKind.Sleep or ScheduleActivityKind.Rest or ScheduleActivityKind.MedicalTreatment)
        {
            int recovery = record.CurrentActivity == ScheduleActivityKind.MedicalTreatment ? 12 : 4;
            record.HealthPermille = Math.Clamp(record.HealthPermille + recovery * boundedSteps, 0, 1000);
            brain.FearPermille = Math.Max(0, brain.FearPermille - recovery * 2 * boundedSteps);
            if (brain.FearPermille < 250 && brain.Disposition == AgentDisposition.Fleeing) brain.Disposition = AgentDisposition.Guarded;
        }

        record.LastStrategicTick = checked(record.LastStrategicTick + steps * 600UL);
        record.Revision++;
        brain.Revision++;
    }
}

