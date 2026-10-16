namespace Roentgen.Core;

public sealed class DeterministicFingerprint
{
    private readonly CanonicalWriter _writer = new();

    public void AddSection(string sectionId, Action<CanonicalWriter> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        ArgumentNullException.ThrowIfNull(write);

        _writer.WriteString(sectionId);
        var section = new CanonicalWriter();
        write(section);
        _writer.WriteBytes(section.WrittenSpan);
    }

    public StateChecksum Finish() => _writer.ComputeChecksum();
}
