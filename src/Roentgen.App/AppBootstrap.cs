using Roentgen.Audio.Playroma;
using Roentgen.Core;
using Roentgen.Music.Playroma;

namespace Roentgen.App;

public static class AppBootstrap
{
    public static ModuleCatalog CreateModuleCatalog()
    {
        var catalog = new ModuleCatalog();
        Register(catalog, new Roentgen.Entities.ModuleInfo());
        Register(catalog, new Roentgen.Physics.ModuleInfo());
        Register(catalog, new Roentgen.World.ModuleInfo());
        Register(catalog, new Roentgen.Navigation.ModuleInfo());
        Register(catalog, new Roentgen.Items.ModuleInfo());
        Register(catalog, new Roentgen.Rpg.ModuleInfo());
        Register(catalog, new Roentgen.Combat.ModuleInfo());
        Register(catalog, new Roentgen.Factions.ModuleInfo());
        Register(catalog, new Roentgen.Narrative.ModuleInfo());
        Register(catalog, new Roentgen.AI.ModuleInfo());
        Register(catalog, new Roentgen.Animation.ModuleInfo());
        Register(catalog, new Roentgen.Assets.ModuleInfo());
        Register(catalog, new Roentgen.Rendering.ModuleInfo());
        Register(catalog, new Roentgen.Audio.ModuleInfo());
        Register(catalog, new PlayromaSoundBridge());
        Register(catalog, new Roentgen.Music.ModuleInfo());
        Register(catalog, new PlayromaMusicBridge());
        Register(catalog, new Roentgen.Persistence.ModuleInfo());
        Register(catalog, new Roentgen.UI.ModuleInfo());
        return catalog;
    }

    private static void Register(ModuleCatalog catalog, IRoentgenModule module) => catalog.Register(module);
}
