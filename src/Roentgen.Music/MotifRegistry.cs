using System.Security.Cryptography;
using System.Text;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Music;

public sealed class CampaignMotifRegistry : IMotifRegistry
{
    private readonly StableId _campaignId;
    private readonly SortedDictionary<StableId, MusicMotifDefinition> _motifs = new();

    public CampaignMotifRegistry(StableId campaignId) => _campaignId = campaignId;
    public IReadOnlyList<MusicMotifDefinition> Motifs => _motifs.Values.ToArray();

    public MusicMotifDefinition GetOrCreate(StableId ownerId, string role)
    {
        if (_motifs.TryGetValue(ownerId, out MusicMotifDefinition? motif)) return motif;
        StableId motifStableId = StableIdFactory.CreateChild(_campaignId, "music-motif", ownerId.ToString() + ":" + role);
        ulong seed = motifStableId.High ^ motifStableId.Low;
        int[] contour = new int[8];
        int[] rhythm = new int[8];
        ulong state = seed;
        for (int i = 0; i < contour.Length; i++)
        {
            state = Next(state);
            contour[i] = (int)(state % 7UL) - 3;
            state = Next(state);
            rhythm[i] = 1 + (int)(state % 4UL);
        }
        string color = role.Contains("hostile", StringComparison.OrdinalIgnoreCase) ? "dissonant-minor" : role.Contains("region", StringComparison.OrdinalIgnoreCase) ? "modal-open" : "diagnostic-chromatic";
        string[] families = role.Contains("companion", StringComparison.OrdinalIgnoreCase) ? ["Piano", "Reed", "Strings"] : ["SynthPad", "ChromaticPercussion", "Strings"];
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{motifStableId}:{string.Join(',', contour)}:{string.Join(',', rhythm)}:{color}"))).ToLowerInvariant();
        motif = new MusicMotifDefinition(ownerId, motifStableId.ToString(), seed, contour, rhythm, color, families, fingerprint);
        _motifs.Add(ownerId, motif);
        return motif;
    }

    public void Restore(IEnumerable<MusicMotifDefinition> motifs)
    {
        _motifs.Clear();
        foreach (MusicMotifDefinition motif in motifs.OrderBy(static value => value.OwnerId)) _motifs[motif.OwnerId] = motif;
    }

    private static ulong Next(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
