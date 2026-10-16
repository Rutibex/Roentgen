using Roentgen.Contracts;
using Roentgen.World.Structures;

namespace Roentgen.Navigation;

public sealed record TraversalValidationIssue(string Code,StableId SubjectId,string Message,bool Critical);
public sealed record TraversalValidationReport(bool IsValid,IReadOnlyList<TraversalValidationIssue> Issues,int RoutesTested,int RoutesSucceeded);

public sealed class AutomatedStructureTraversalValidator
{
    public TraversalValidationReport Validate(StructureBlueprint blueprint,InteriorNavigationData data)
    {
        ArgumentNullException.ThrowIfNull(blueprint);ArgumentNullException.ThrowIfNull(data);var issues=new List<TraversalValidationIssue>();int tested=0,succeeded=0;
        var finder=new InteriorRouteFinder();StableId exterior=finder.ExteriorId(data);
        foreach(NavigationCell cell in data.Cells.Where(static cell=>cell.QuestCritical).OrderBy(static cell=>cell.Id))
        {
            tested++;InteriorRoute route=finder.Find(data,exterior,cell.Id,TraversalProfile.Ordinary);
            if(route.Found)succeeded++;else
            {
                InteriorRoute technical=finder.Find(data,exterior,cell.Id,TraversalProfile.Technical);InteriorRoute breacher=finder.Find(data,exterior,cell.Id,TraversalProfile.Breacher);
                if(technical.Found||breacher.Found)succeeded++;else issues.Add(new TraversalValidationIssue("NAV-QUEST-001",cell.RoomId,"No supported profile can reach a quest-critical room.",true));
            }
        }
        if(blueprint.Grammar.Navigation.RequiresAlternateCriticalRoute)
        {
            int usableExits=data.Portals.Count(static portal=>portal.Exterior&&portal.DoorState is not DoorLifecycleState.Destroyed and not DoorLifecycleState.Missing);
            if(usableExits<2)issues.Add(new TraversalValidationIssue("NAV-ALT-001",blueprint.Id,"Structure requires at least two exterior route portals.",true));
        }
        foreach(CoverPoint cover in data.Cover)
        {
            tested++;if(data.Cells.Any(cell=>cell.Id==cover.CellId))succeeded++;else issues.Add(new TraversalValidationIssue("NAV-COVER-001",cover.Id,"Cover point references a missing cell.",false));
        }
        return new TraversalValidationReport(issues.All(static issue=>!issue.Critical),issues,tested,succeeded);
    }
}
