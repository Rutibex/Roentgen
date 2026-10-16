using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Generation;

namespace Roentgen.World;

public sealed class TerrainChunkCompiler : IVersionedGenerator<TerrainChunkGenerationRequest, TerrainChunkData>
{
    public const string CurrentGeneratorId = "roentgen.world.terrain-chunk";
    public const int CurrentSchemaVersion = 1;

    private readonly RegionalTopology _topology;
    private readonly TerrainHeightEvaluator _evaluator;

    public TerrainChunkCompiler(RegionalTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        _topology = topology;
        _evaluator = new TerrainHeightEvaluator(topology);
    }

    public string GeneratorId => CurrentGeneratorId;
    public int SchemaVersion => CurrentSchemaVersion;

    public TerrainChunkData Generate(TerrainChunkGenerationRequest request, DomainSeed seed) =>
        GenerateCore(request, seed, CancellationToken.None);

    private TerrainChunkData GenerateCore(TerrainChunkGenerationRequest request, DomainSeed seed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAddressInsideWorld(request.Address))
        {
            throw new ArgumentOutOfRangeException(nameof(request), $"Chunk {request.Address} lies outside the generated world.");
        }

        StableId id = StableIdFactory.CreateChild(_topology.Id, "terrain-chunk", request.Address.ToString());
        var provenance = new GenerationProvenanceBuilder(id, _topology.Id, _topology.CampaignSeed, seed, GeneratorId, SchemaVersion);
        WorldGenerationProfile profile = _topology.Profile;
        WorldPoint origin = request.Address.Origin(profile.ChunkSizeCentimeters);
        var lods = new List<TerrainChunkLodData>();
        for (int lod = 0; lod < profile.LodSampleCounts.Count; lod++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int sampleCount = profile.LodSampleCounts[lod];
            int spacing = profile.ChunkSizeCentimeters / (sampleCount - 1);
            int[] heights = new int[sampleCount * sampleCount];
            var surfaces = new TerrainSurfaceKind[heights.Length];
            for (int z = 0; z < sampleCount; z++)
            {
                if ((z & 7) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                for (int x = 0; x < sampleCount; x++)
                {
                    int worldX = origin.XCentimeters + x * spacing;
                    int worldZ = origin.ZCentimeters + z * spacing;
                    int height = _evaluator.SampleHeightCentimeters(worldX, worldZ);
                    int left = _evaluator.SampleHeightCentimeters(worldX - spacing, worldZ);
                    int right = _evaluator.SampleHeightCentimeters(worldX + spacing, worldZ);
                    int back = _evaluator.SampleHeightCentimeters(worldX, worldZ - spacing);
                    int forward = _evaluator.SampleHeightCentimeters(worldX, worldZ + spacing);
                    int rise = Math.Max(Math.Abs(right - left), Math.Abs(forward - back));
                    int slopePermille = rise * 1000 / Math.Max(1, spacing * 2);
                    int index = z * sampleCount + x;
                    heights[index] = height;
                    surfaces[index] = _evaluator.SampleSurface(worldX, worldZ, height, slopePermille);
                }
            }

            lods.Add(new TerrainChunkLodData(lod, sampleCount, spacing, heights, surfaces));
        }

        cancellationToken.ThrowIfCancellationRequested();
        provenance.RecordDecision("chunk.address", request.Address.ToString(), $"epoch {request.Epoch}");
        provenance.RecordDecision("lods", "compiled", string.Join(",", lods.Select(static lod => lod.SampleCount)));
        provenance.RecordValidation("All LODs sample the shared global height evaluator.");
        var chunk = new TerrainChunkData(id, request.Address, lods, provenance.Build());
        ValidationReport report = new TerrainChunkValidator(_topology.Profile).Validate(chunk);
        if (!report.IsValid)
        {
            throw new InvalidDataException(string.Join("; ", report.Issues.Select(static issue => issue.Message)));
        }

        return chunk;
    }

    public TerrainChunkData Compile(TerrainChunkAddress address, long epoch = 0, CancellationToken cancellationToken = default)
    {
        DomainSeed root = GenerationDomain.Derive(_topology.CampaignSeed, "terrain.chunk");
        DomainSeed seed = GenerationDomain.Derive(root, $"x{address.X}.z{address.Z}");
        return GenerateCore(new TerrainChunkGenerationRequest(address, epoch), seed, cancellationToken);
    }

    public bool IsAddressInsideWorld(TerrainChunkAddress address)
    {
        WorldGenerationProfile profile = _topology.Profile;
        WorldPoint origin = address.Origin(profile.ChunkSizeCentimeters);
        return origin.XCentimeters >= profile.MinimumWorldXCentimeters && origin.ZCentimeters >= profile.MinimumWorldZCentimeters &&
            origin.XCentimeters + profile.ChunkSizeCentimeters <= profile.MaximumWorldXCentimeters &&
            origin.ZCentimeters + profile.ChunkSizeCentimeters <= profile.MaximumWorldZCentimeters;
    }
}
