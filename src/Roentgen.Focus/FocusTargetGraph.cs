using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Combat;

namespace Roentgen.Focus;

public sealed record FocusTargetNode(
    StableId NodeId, StableId ActorId, StableId RuntimeBodyPartId, StableId ParentNodeId,
    string DisplayName, FocusTargetNodeKind Kind, AnatomicalRegion Region,
    DiagnosticKnowledgeLevel Knowledge, BodyPartFunctionalState FunctionalState,
    int RadiusMillimeters, int ConsequenceWeightPermille,
    IReadOnlyList<string> Functions, IReadOnlyList<string> Consequences)
{
    public bool IsSelectable => Knowledge >= DiagnosticKnowledgeLevel.ExternalStructure && Consequences.Count > 0 && FunctionalState != BodyPartFunctionalState.Destroyed;
}

public sealed record FocusTargetGraph(StableId ActorId, IReadOnlyList<FocusTargetNode> Nodes)
{
    public FocusTargetNode GetRequired(StableId id) => Nodes.FirstOrDefault(n => n.NodeId == id) ?? throw new KeyNotFoundException($"Focus target node '{id}' is unavailable.");
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>(); var ids = new HashSet<StableId>();
        foreach (var node in Nodes) {
            if (!ids.Add(node.NodeId)) errors.Add($"Duplicate node {node.NodeId}.");
            if (node.Consequences.Count == 0) errors.Add($"Node {node.DisplayName} has no consequential behavior.");
            if (!node.ParentNodeId.IsEmpty && Nodes.All(x=>x.NodeId!=node.ParentNodeId)) errors.Add($"Node {node.DisplayName} has missing parent.");
        }
        return errors;
    }
}

public sealed class FocusTargetGraphBuilder
{
    public FocusTargetGraph Build(CombatActorState actor, DiagnosticKnowledgeLevel knowledge)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var nodes = new List<FocusTargetNode>();
        foreach (BodyPartRuntimeState part in actor.BodyParts.OrderBy(x=>x.Definition.Id))
        {
            var (kind, functions, consequences) = Describe(part.Definition.Region);
            nodes.Add(new FocusTargetNode(part.Definition.Id, actor.Id, part.Definition.Id, StableId.Empty,
                part.Definition.Name, kind, part.Definition.Region, knowledge, part.FunctionalState,
                part.Definition.RadiusMillimeters, part.Definition.ConsequenceWeightPermille, functions, consequences));
        }
        if (!actor.EquippedWeaponId.IsEmpty)
        {
            StableId nodeId = new(actor.EquippedWeaponId.High ^ 0xF0C05UL, actor.EquippedWeaponId.Low ^ 0xA11UL);
            nodes.Add(new FocusTargetNode(nodeId, actor.Id, StableId.Empty, StableId.Empty, "Equipped weapon",
                FocusTargetNodeKind.EquippedWeapon, AnatomicalRegion.EquippedWeapon, knowledge,
                BodyPartFunctionalState.Healthy, 140, 700, ["attack"], ["Disables or degrades the equipped attack"]));
        }
        return new FocusTargetGraph(actor.Id, nodes);
    }

    private static (FocusTargetNodeKind, string[], string[]) Describe(AnatomicalRegion region) => region switch
    {
        AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster => (FocusTargetNodeKind.Organ,["senses","accuracy"],["Reduces perception and may stun or kill"]),
        AnatomicalRegion.Torso => (FocusTargetNodeKind.Anatomy,["vitality","breathing"],["Causes major trauma, bleeding, incapacitation or death"]),
        AnatomicalRegion.LeftArm or AnatomicalRegion.RightArm => (FocusTargetNodeKind.Limb,["weapon-handling","melee"],["Reduces accuracy and weapon eligibility"]),
        AnatomicalRegion.LeftLeg or AnatomicalRegion.RightLeg => (FocusTargetNodeKind.Limb,["mobility","evasion"],["Reduces speed, balance and evasion"]),
        AnatomicalRegion.FrontLeftLimb or AnatomicalRegion.FrontRightLimb or AnatomicalRegion.RearLeftLimb or AnatomicalRegion.RearRightLimb => (FocusTargetNodeKind.Limb,["mobility","attack"],["Reduces creature movement and attack stability"]),
        AnatomicalRegion.Jaw => (FocusTargetNodeKind.Organ,["natural-attack"],["Disables or degrades bite attacks"]),
        AnatomicalRegion.ExposedGrowth => (FocusTargetNodeKind.HazardComponent,["special-ability","hazard"],["Disables special ability and may release a hazard"]),
        _ => (FocusTargetNodeKind.Anatomy,["structure"],["Reduces component function"])
    };
}
