namespace Roentgen.Simulation;

public sealed class CommandEnvelopeCanonicalComparer : IComparer<CommandEnvelope>
{
    public static CommandEnvelopeCanonicalComparer Instance { get; } = new();

    private CommandEnvelopeCanonicalComparer() { }

    public int Compare(CommandEnvelope? left, CommandEnvelope? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        int result = left.TargetTick.CompareTo(right.TargetTick);
        if (result != 0) return result;
        result = left.OriginPriority.CompareTo(right.OriginPriority);
        if (result != 0) return result;
        result = left.Sequence.CompareTo(right.Sequence);
        if (result != 0) return result;
        return left.CommandId.CompareTo(right.CommandId);
    }
}
