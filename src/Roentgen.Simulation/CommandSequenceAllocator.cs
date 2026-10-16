namespace Roentgen.Simulation;

public sealed class CommandSequenceAllocator
{
    public CommandSequenceAllocator(ulong initialSequence = 0)
    {
        NextSequence = initialSequence;
    }

    public ulong NextSequence { get; private set; }

    public ulong Allocate()
    {
        ulong value = NextSequence;
        NextSequence = checked(NextSequence + 1UL);
        return value;
    }

    public void Restore(ulong nextSequence) => NextSequence = nextSequence;
}
