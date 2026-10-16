using System.Globalization;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Audio;

public readonly record struct AudioIdentity(string AssetId, ulong RootSeed, ulong FamilySeed, int VariationIndex, AudioSceneDomain Domain, string BusId);

public static class AudioIdentityFactory
{
    public static AudioIdentity Resolve(SemanticAudioEvent audioEvent)
    {
        string asset = ResolveAsset(audioEvent);
        StableId identityId = !audioEvent.ItemId.IsEmpty ? audioEvent.ItemId : !audioEvent.SourceId.IsEmpty ? audioEvent.SourceId : audioEvent.EventId;
        ulong family = Mix(identityId.High, identityId.Low, StableText(asset));
        ulong root = Mix(audioEvent.EventId.High, audioEvent.EventId.Low, family);
        int variation = unchecked((int)(Mix(root, family, audioEvent.SimulationTick) % 8UL));
        AudioSceneDomain domain = ResolveDomain(audioEvent.Kind);
        string bus = ResolveBus(audioEvent.Kind);
        return new AudioIdentity(asset, root, family, variation, domain, bus);
    }

    private static string ResolveAsset(SemanticAudioEvent e)
    {
        string variant = GetText(e.Parameters, "variant", string.Empty);
        AudioSurface surface = (AudioSurface)GetInt(e.Parameters, "surface", (long)AudioSurface.Unknown);
        AudioGait gait = (AudioGait)GetInt(e.Parameters, "gait", (long)AudioGait.Walk);
        return e.Kind switch
        {
            AudioEventKind.WeaponFire => "weapon." + Normalize(variant, "service-sidearm") + ".fire",
            AudioEventKind.WeaponDryFire => "weapon." + Normalize(variant, "service-sidearm") + ".dry",
            AudioEventKind.WeaponJam => "weapon." + Normalize(variant, "service-sidearm") + ".jam",
            AudioEventKind.ReloadStart or AudioEventKind.ReloadMechanism or AudioEventKind.ReloadComplete => "weapon." + Normalize(variant, "service-sidearm") + ".reload",
            AudioEventKind.Explosion => "combat.fragmentation.explosion",
            AudioEventKind.MeleeSwing => "combat.melee.swing",
            AudioEventKind.MeleeContact => "combat.melee.contact",
            AudioEventKind.BiologicalImpact => "impact.biological",
            AudioEventKind.ArmorImpact => "impact.armor",
            AudioEventKind.MechanicalImpact => "impact.mechanical",
            AudioEventKind.Impact => "impact." + SurfaceId(surface),
            AudioEventKind.FootContact or AudioEventKind.Landing => "footstep." + SurfaceId(surface) + "." + gait.ToString().ToLowerInvariant(),
            AudioEventKind.DoorOpen => "interaction.door.open",
            AudioEventKind.DoorClose => "interaction.door.close",
            AudioEventKind.ContainerOpen => "interaction.container.open",
            AudioEventKind.ContainerClose => "interaction.container.close",
            AudioEventKind.Pickup => "interaction.pickup",
            AudioEventKind.TerminalUse => "interaction.terminal",
            AudioEventKind.Craft => "interaction.craft",
            AudioEventKind.Repair => "interaction.repair",
            AudioEventKind.MachineStart => "machine.generator.start",
            AudioEventKind.MachineStop => "machine.generator.stop",
            AudioEventKind.MachineMalfunction => "machine.fault",
            AudioEventKind.Injury => "creature.pain",
            AudioEventKind.Death => "creature.death",
            AudioEventKind.InterfaceConfirm => "ui.confirm",
            AudioEventKind.InterfaceCancel => "ui.cancel",
            AudioEventKind.InterfaceError => "ui.error",
            AudioEventKind.InterfaceWarning => "ui.warning",
            AudioEventKind.InterfaceNavigate => "ui.navigate",
            AudioEventKind.InterfaceTab => "ui.tab",
            AudioEventKind.InterfaceTransaction => "ui.transaction",
            AudioEventKind.InterfaceCraft => "ui.craft",
            AudioEventKind.JournalUpdate or AudioEventKind.ObjectiveAdvanced => "ui.journal",
            AudioEventKind.QuestAccepted => "ui.quest",
            AudioEventKind.Revelation => "ui.revelation",
            AudioEventKind.IrreversibleChoice => "ui.commitment",
            AudioEventKind.FocusEnter => "focus.enter",
            AudioEventKind.FocusTarget => "focus.target",
            AudioEventKind.FocusPartSelected => "focus.part",
            AudioEventKind.FocusActionQueued => "focus.queue",
            AudioEventKind.FocusInsufficient => "focus.insufficient",
            AudioEventKind.FocusCommit => "focus.commit",
            AudioEventKind.FocusExit => "focus.exit",
            _ => "ui.confirm"
        };
    }

    private static AudioSceneDomain ResolveDomain(AudioEventKind kind) => kind switch
    {
        AudioEventKind.FocusEnter or AudioEventKind.FocusTarget or AudioEventKind.FocusPartSelected or AudioEventKind.FocusActionQueued or AudioEventKind.FocusInsufficient or AudioEventKind.FocusCommit or AudioEventKind.FocusExit => AudioSceneDomain.Focus,
        AudioEventKind.InterfaceConfirm or AudioEventKind.InterfaceCancel or AudioEventKind.InterfaceError or AudioEventKind.InterfaceWarning or AudioEventKind.InterfaceNavigate or AudioEventKind.InterfaceTab or AudioEventKind.InterfaceTransaction or AudioEventKind.InterfaceCraft or AudioEventKind.JournalUpdate or AudioEventKind.QuestAccepted or AudioEventKind.ObjectiveAdvanced or AudioEventKind.Revelation or AudioEventKind.IrreversibleChoice => AudioSceneDomain.Global,
        AudioEventKind.MachineStart or AudioEventKind.MachineStop or AudioEventKind.MachineMalfunction => AudioSceneDomain.Resident,
        _ => AudioSceneDomain.Global
    };

    private static string ResolveBus(AudioEventKind kind) => kind switch
    {
        AudioEventKind.WeaponFire or AudioEventKind.WeaponDryFire or AudioEventKind.WeaponJam or AudioEventKind.ReloadStart or AudioEventKind.ReloadMechanism or AudioEventKind.ReloadComplete => "weapons.player",
        AudioEventKind.Explosion or AudioEventKind.Impact or AudioEventKind.ArmorImpact or AudioEventKind.BiologicalImpact or AudioEventKind.MechanicalImpact or AudioEventKind.MeleeContact => "impacts",
        AudioEventKind.FootContact or AudioEventKind.Landing or AudioEventKind.MeleeSwing
            or AudioEventKind.DoorOpen or AudioEventKind.DoorClose or AudioEventKind.DoorLock or AudioEventKind.DoorUnlock or AudioEventKind.DoorBreach
            or AudioEventKind.ContainerOpen or AudioEventKind.ContainerClose or AudioEventKind.Pickup or AudioEventKind.Drop or AudioEventKind.Equip or AudioEventKind.Unequip
            or AudioEventKind.Craft or AudioEventKind.Repair or AudioEventKind.MedicalAction => "foley",
        AudioEventKind.TerminalUse => "interface",
        AudioEventKind.Injury or AudioEventKind.Death => "creatures",
        AudioEventKind.MachineStart or AudioEventKind.MachineStop or AudioEventKind.MachineMalfunction => "machinery",
        AudioEventKind.FocusEnter or AudioEventKind.FocusTarget or AudioEventKind.FocusPartSelected or AudioEventKind.FocusActionQueued or AudioEventKind.FocusInsufficient or AudioEventKind.FocusCommit or AudioEventKind.FocusExit => "focus",
        _ => "interface"
    };

    private static long GetInt(IReadOnlyDictionary<string, AudioValue> parameters, string key, long fallback) => parameters.TryGetValue(key, out AudioValue value) ? value.Integer : fallback;
    private static string GetText(IReadOnlyDictionary<string, AudioValue> parameters, string key, string fallback) => parameters.TryGetValue(key, out AudioValue value) && !string.IsNullOrWhiteSpace(value.Text) ? value.Text : fallback;
    private static string Normalize(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant().Replace(' ', '-');
    private static string SurfaceId(AudioSurface surface) => surface.ToString().ToLowerInvariant();

    private static ulong StableText(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in text) { hash ^= c; hash = unchecked(hash * 1099511628211UL); }
        return hash;
    }

    private static ulong Mix(ulong a, ulong b, ulong c)
    {
        ulong x = a ^ 0x9E3779B97F4A7C15UL;
        x = unchecked((x ^ b) * 0xBF58476D1CE4E5B9UL);
        x = unchecked((x ^ c) * 0x94D049BB133111EBUL);
        return x ^ (x >> 31);
    }
}
