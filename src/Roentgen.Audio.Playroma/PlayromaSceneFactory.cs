using Playroma.Sound.Core.Audio;
using Playroma.Sound.Graph.Definitions;
using Playroma.Sound.Streaming.Assets;
using Playroma.Sound.Streaming.Compilation;
using Playroma.Sound.Streaming.Definitions;
using Playroma.Sound.Streaming.Generators;
using Roentgen.Audio;

namespace Roentgen.Audio.Playroma;

public static class PlayromaSceneFactory
{
    public static PreparedSoundScene CreateGlobal(IReadOnlyDictionary<string, SoundGraphDefinition> assets, AudioSettings settings, ulong seed)
    {
        SoundSceneDefinition scene = BaseScene("roentgen.audio.global", "Roentgen Global Audio", seed, settings.MaximumVoices);
        foreach (SoundGraphDefinition graph in assets.Values.OrderBy(static graph => graph.Id, StringComparer.Ordinal))
        {
            int pool = graph.Id.StartsWith("footstep.", StringComparison.Ordinal) ? 4 : graph.Id.StartsWith("impact.", StringComparison.Ordinal) ? 6 : graph.Id.StartsWith("weapon.", StringComparison.Ordinal) ? 8 : 3;
            scene.Sounds.Add(new SoundReferenceDefinition { SoundId = graph.Id, PoolSize = pool });
        }
        return Compile(scene, assets, settings);
    }

    public static PreparedSoundScene CreateFocus(IReadOnlyDictionary<string, SoundGraphDefinition> assets, AudioSettings settings, ulong seed)
    {
        SoundGraphDefinition[] selected = assets.Values.Where(static graph => graph.Id.StartsWith("focus.", StringComparison.Ordinal)).ToArray();
        SoundSceneDefinition scene = BaseScene("roentgen.audio.focus", "Roentgen Focus Audio", seed ^ 0xF0C05UL, 24);
        var focusEffects = new List<SoundBusEffectDefinition>
        {
            new() { Kind = SoundBusEffectKind.Saturation, Amount = 0.18f, Mix = 0.2f }
        };
        if (settings.ReduceHighFrequencyDiagnosticSounds)
            focusEffects.Add(new SoundBusEffectDefinition { Kind = SoundBusEffectKind.LowPass, Amount = 0.72f, Mix = 1f });
        scene.Buses =
        [
            new SoundBusDefinition { Id = "master", Gain = 0.95f, Effects = [new SoundBusEffectDefinition { Kind = SoundBusEffectKind.Limiter, Amount = 0.12f }] },
            new SoundBusDefinition { Id = "focus", ParentId = "master", Gain = 1f, Effects = focusEffects }
        ];
        // Focus uses a dry two-bus mix. Keep listener zone IDs, but remove the
        // inherited sends to reverb buses which are not present in this scene.
        foreach (AcousticZoneDefinition zone in scene.AcousticZones) zone.EnvironmentBusId = string.Empty;
        scene.Parameters = [MixParameter("mix.focus", "Focus mix")];
        scene.ParameterBindings = [MixBinding("mix.focus", "focus")];
        foreach (SoundGraphDefinition graph in selected) scene.Sounds.Add(new SoundReferenceDefinition { SoundId = graph.Id, PoolSize = 4 });
        return Compile(scene, selected.ToDictionary(static graph => graph.Id, StringComparer.Ordinal), settings);
    }

    public static PreparedSoundScene CreateResident(IReadOnlyDictionary<string, SoundGraphDefinition> assets, AudioSettings settings, ulong seed)
    {
        SoundGraphDefinition[] selected = assets.Values.Where(static graph => graph.Id.StartsWith("machine.", StringComparison.Ordinal) || graph.Id.StartsWith("creature.", StringComparison.Ordinal)).ToArray();
        SoundSceneDefinition scene = BaseScene("roentgen.audio.resident", "Roentgen Resident Audio", seed ^ 0xA11CEUL, 32);
        foreach (SoundGraphDefinition graph in selected) scene.Sounds.Add(new SoundReferenceDefinition { SoundId = graph.Id, PoolSize = 6 });
        return Compile(scene, selected.ToDictionary(static graph => graph.Id, StringComparer.Ordinal), settings);
    }

    public static PreparedSoundScene CreateRegion(AudioRegion region, ulong seed, AudioSettings settings)
    {
        string generatorId = region switch
        {
            AudioRegion.GlassBasin => "playroma.scene.wind",
            AudioRegion.OssuaryFields => "playroma.scene.forest",
            AudioRegion.BlackwaterWorks => "playroma.scene.water",
            AudioRegion.BrokenMeridian => "playroma.scene.industrial",
            AudioRegion.RedQuarryCountry => "playroma.scene.machine",
            AudioRegion.QuietRange => "playroma.scene.cave",
            _ => "playroma.scene.wind"
        };
        var description = region == AudioRegion.BrokenMeridian
            ? new StreamingSceneDescription { Intensity = 0.38f, Activity = 0.20f }
            : new StreamingSceneDescription { Intensity = 0.48f, Activity = 0.34f };
        global::Playroma.Sound.Generators.Contracts.SoundQualityMode quality = settings.Quality == AudioQuality.RealtimeLow
            ? global::Playroma.Sound.Generators.Contracts.SoundQualityMode.RealtimeLow
            : global::Playroma.Sound.Generators.Contracts.SoundQualityMode.RealtimeHigh;
        IStreamingSceneGenerator generator = BuiltInStreamingSceneCatalog.GetRequired(generatorId);
        GeneratedSoundScene generated = generator.Generate(
            description,
            new StreamingSceneGenerationContext
            {
                RootSeed = seed,
                Quality = quality,
                SceneId = "roentgen.region." + region.ToString().ToLowerInvariant(),
                DisplayName = region.ToString()
            });

        SetParameterDefault(generated.Scene, "intensity", description.Intensity);
        SetParameterDefault(generated.Scene, "activity", description.Activity);
        if (region == AudioRegion.BrokenMeridian)
        {
            TuneBrokenMeridianIndustrial(generated.Scene);
            IStreamingSceneGenerator windGenerator = BuiltInStreamingSceneCatalog.GetRequired("playroma.scene.wind");
            GeneratedSoundScene wind = windGenerator.Generate(
                new StreamingSceneDescription { Intensity = 0.32f, Activity = 0.14f },
                new StreamingSceneGenerationContext
                {
                    RootSeed = seed ^ 0x57494E4457415354UL,
                    Quality = quality,
                    SceneId = "roentgen.region.brokenmeridian.wind",
                    DisplayName = "Broken Meridian Wasteland Wind"
                });
            TuneBrokenMeridianWind(wind.Scene);
            generated = MergeAmbientOverlay(generated, wind);
        }

        generated.Scene.MaximumVoices = Math.Min(Math.Max(12, settings.MaximumVoices / 2), generated.Scene.MaximumVoices);
        return Compile(generated.Scene, generated.Assets, settings);
    }

    private static void SetParameterDefault(SoundSceneDefinition scene, string id, float value)
    {
        SceneParameterDefinition? parameter = scene.Parameters.FirstOrDefault(parameter => parameter.Id.Equals(id, StringComparison.Ordinal));
        if (parameter is not null) parameter.DefaultValue = Math.Clamp(value, parameter.Minimum, parameter.Maximum);
    }

    private static void TuneBrokenMeridianIndustrial(SoundSceneDefinition scene)
    {
        SoundLayerDefinition? machinery = scene.Layers.FirstOrDefault(layer => layer.Id == "machinery");
        if (machinery is not null) machinery.Gain = 0.24f;
        SoundEventEmitterDefinition? clanks = scene.Emitters.FirstOrDefault(emitter => emitter.Id == "clanks");
        if (clanks is not null)
        {
            clanks.MinimumIntervalSeconds = 3.5;
            clanks.MaximumIntervalSeconds = 15.0;
            clanks.MaximumBurstCount = 1;
            clanks.MaximumConcurrentVoices = 3;
            clanks.Gain = 0.24f;
            clanks.SpatialDistribution.MinimumRadius = 8f;
            clanks.SpatialDistribution.MaximumRadius = 42f;
            clanks.SpatialDistribution.MaximumDistance = 90f;
        }
        foreach (SceneParameterBindingDefinition binding in scene.ParameterBindings)
        {
            if (binding.TargetId == "machinery" && binding.TargetKind == SceneParameterTargetKind.LayerGain)
            {
                binding.OutputMinimum = 0.08f; binding.OutputMaximum = 0.34f;
            }
            else if (binding.TargetId == "clanks" && binding.TargetKind == SceneParameterTargetKind.EmitterRate)
            {
                binding.OutputMinimum = 0.08f; binding.OutputMaximum = 0.58f;
            }
            else if (binding.TargetId == "clanks" && binding.TargetKind == SceneParameterTargetKind.EmitterGain)
            {
                binding.OutputMinimum = 0.08f; binding.OutputMaximum = 0.34f;
            }
        }
    }

    private static void TuneBrokenMeridianWind(SoundSceneDefinition scene)
    {
        SoundLayerDefinition? wind = scene.Layers.FirstOrDefault(layer => layer.Id == "wind");
        if (wind is not null) wind.Gain = 0.38f;
        SoundEventEmitterDefinition? gusts = scene.Emitters.FirstOrDefault(emitter => emitter.Id == "gusts");
        if (gusts is not null)
        {
            gusts.MinimumIntervalSeconds = 6.0;
            gusts.MaximumIntervalSeconds = 24.0;
            gusts.MaximumConcurrentVoices = 2;
            gusts.Gain = 0.38f;
            gusts.SpatialDistribution.MinimumRadius = 10f;
            gusts.SpatialDistribution.MaximumRadius = 55f;
            gusts.SpatialDistribution.MaximumDistance = 120f;
        }
        foreach (SceneParameterBindingDefinition binding in scene.ParameterBindings)
        {
            if (binding.TargetId == "wind" && binding.TargetKind == SceneParameterTargetKind.LayerGain)
            {
                binding.OutputMinimum = 0.18f; binding.OutputMaximum = 0.58f;
            }
            else if (binding.TargetId == "gusts" && binding.TargetKind == SceneParameterTargetKind.EmitterRate)
            {
                binding.OutputMinimum = 0.08f; binding.OutputMaximum = 0.52f;
            }
            else if (binding.TargetId == "gusts" && binding.TargetKind == SceneParameterTargetKind.EmitterGain)
            {
                binding.OutputMinimum = 0.14f; binding.OutputMaximum = 0.48f;
            }
        }
    }

    private static GeneratedSoundScene MergeAmbientOverlay(GeneratedSoundScene primary, GeneratedSoundScene overlay)
    {
        var assets = new Dictionary<string, SoundGraphDefinition>(primary.Assets, StringComparer.Ordinal);
        foreach ((string id, SoundGraphDefinition graph) in overlay.Assets) assets.TryAdd(id, graph);
        foreach (SoundReferenceDefinition sound in overlay.Scene.Sounds)
            if (!primary.Scene.Sounds.Any(existing => existing.SoundId == sound.SoundId)) primary.Scene.Sounds.Add(sound);
        foreach (SoundLayerDefinition layer in overlay.Scene.Layers)
            if (!primary.Scene.Layers.Any(existing => existing.Id == layer.Id)) primary.Scene.Layers.Add(layer);
        foreach (SoundEventEmitterDefinition emitter in overlay.Scene.Emitters)
            if (!primary.Scene.Emitters.Any(existing => existing.Id == emitter.Id)) primary.Scene.Emitters.Add(emitter);
        foreach (SceneParameterBindingDefinition binding in overlay.Scene.ParameterBindings)
            if (!primary.Scene.ParameterBindings.Any(existing => existing.ParameterId == binding.ParameterId && existing.TargetKind == binding.TargetKind && existing.TargetId == binding.TargetId && existing.TargetParameter == binding.TargetParameter))
                primary.Scene.ParameterBindings.Add(binding);
        return new GeneratedSoundScene(primary.Scene, assets);
    }

    private static SoundSceneDefinition BaseScene(string id, string displayName, ulong seed, int voices)
    {
        var scene = new SoundSceneDefinition
        {
            Id = id,
            DisplayName = displayName,
            RootSeed = seed,
            MaximumVoices = voices,
            VirtualizationThreshold = 0.001f,
            Buses =
            [
                new SoundBusDefinition { Id = "master", Gain = 0.94f, Effects = [new SoundBusEffectDefinition { Kind = SoundBusEffectKind.Limiter, Amount = 0.12f }] },
                new SoundBusDefinition { Id = "world", ParentId = "master", Gain = 1f },
                new SoundBusDefinition { Id = "interface", ParentId = "master", Gain = 1f },
                new SoundBusDefinition { Id = "focus", ParentId = "master", Gain = 1f },
                new SoundBusDefinition { Id = "weapons.player", ParentId = "world", Gain = 1f },
                new SoundBusDefinition { Id = "weapons.other", ParentId = "world", Gain = 0.92f },
                new SoundBusDefinition { Id = "creatures", ParentId = "world", Gain = 0.9f },
                new SoundBusDefinition { Id = "impacts", ParentId = "world", Gain = 0.92f },
                new SoundBusDefinition { Id = "foley", ParentId = "world", Gain = 0.82f },
                new SoundBusDefinition { Id = "machinery", ParentId = "world", Gain = 0.78f },
                new SoundBusDefinition { Id = "ambience", ParentId = "world", Gain = 0.72f },
                new SoundBusDefinition { Id = "reverb.small", ParentId = "master", Gain = 0.45f, Effects = [new SoundBusEffectDefinition { Kind = SoundBusEffectKind.Reverb, Amount = 0.3f, Secondary = 0.65f, WetOnly = true }] },
                new SoundBusDefinition { Id = "reverb.medium", ParentId = "master", Gain = 0.4f, Effects = [new SoundBusEffectDefinition { Kind = SoundBusEffectKind.Reverb, Amount = 0.58f, Secondary = 0.5f, WetOnly = true }] },
                new SoundBusDefinition { Id = "reverb.large", ParentId = "master", Gain = 0.35f, Effects = [new SoundBusEffectDefinition { Kind = SoundBusEffectKind.Reverb, Amount = 0.82f, Secondary = 0.38f, WetOnly = true }] }
            ],
            AcousticZones =
            [
                new AcousticZoneDefinition { Id = "outdoor", EnvironmentBusId = "reverb.small", AmbientGain = 0.04f },
                new AcousticZoneDefinition { Id = "small-room", EnvironmentBusId = "reverb.small", AmbientGain = 0.18f },
                new AcousticZoneDefinition { Id = "industrial", EnvironmentBusId = "reverb.medium", AmbientGain = 0.25f },
                new AcousticZoneDefinition { Id = "subterranean", EnvironmentBusId = "reverb.large", AmbientGain = 0.34f }
            ],
            AcousticPortals =
            [
                new AcousticPortalDefinition { Id = "portal.outdoor-small", ZoneAId = "outdoor", ZoneBId = "small-room", Transmission = 0.65f, LowPassAmount = 0.35f },
                new AcousticPortalDefinition { Id = "portal.small-industrial", ZoneAId = "small-room", ZoneBId = "industrial", Transmission = 0.72f, LowPassAmount = 0.24f },
                new AcousticPortalDefinition { Id = "portal.industrial-subterranean", ZoneAId = "industrial", ZoneBId = "subterranean", Transmission = 0.58f, LowPassAmount = 0.42f }
            ],
            Parameters =
            [
                MixParameter("mix.world", "World mix"),
                MixParameter("mix.interface", "Interface mix")
            ],
            ParameterBindings =
            [
                MixBinding("mix.world", "world"),
                MixBinding("mix.interface", "interface")
            ]
        };
        return scene;
    }

    private static SceneParameterDefinition MixParameter(string id, string displayName) => new()
    {
        Id = id,
        DisplayName = displayName,
        Minimum = 0f,
        Maximum = 1f,
        DefaultValue = 1f,
        SmoothingSeconds = 0.08
    };

    private static SceneParameterBindingDefinition MixBinding(string parameterId, string busId) => new()
    {
        ParameterId = parameterId,
        TargetKind = SceneParameterTargetKind.BusGain,
        TargetId = busId,
        OutputMinimum = 0f,
        OutputMaximum = 1f,
        Curve = SceneMappingCurve.SmoothStep
    };

    private static PreparedSoundScene Compile(SoundSceneDefinition scene, IReadOnlyDictionary<string, SoundGraphDefinition> assets, AudioSettings settings)
    {
        var compiler = new SoundSceneCompiler();
        return compiler.Compile(
            scene,
            new AudioFormat(48_000, 2, settings.RenderBlockFrames),
            new InMemorySoundAssetResolver(assets),
            new SoundSceneCompileOptions
            {
                CommandQueueCapacity = 2048,
                TelemetryIntervalBlocks = 8,
                VoiceFadeFrames = Math.Max(64, settings.RenderBlockFrames / 4),
                VirtualCatchUpBudgetFrames = settings.RenderBlockFrames * 16,
                StopTailSeconds = 0.8,
                EnableVirtualization = true
            });
    }
}
