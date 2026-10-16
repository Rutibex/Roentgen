using System.Globalization;
using System.Text;

namespace Roentgen.UI;

public interface ILocalizationService
{
    string Locale { get; }
    string Get(string key);
    string Format(string key, params object[] arguments);
    IReadOnlyCollection<string> MissingKeys { get; }
}

public sealed class Phase20LocalizationService : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, string> strings;
    private readonly SortedSet<string> missing = new(StringComparer.Ordinal);

    public Phase20LocalizationService(string locale = "en-US", IReadOnlyDictionary<string, string>? overrides = null)
    {
        Locale = string.IsNullOrWhiteSpace(locale) ? "en-US" : locale;
        var merged = new Dictionary<string, string>(EnglishStrings, StringComparer.Ordinal);
        if (overrides is not null)
        {
            foreach ((string key, string value) in overrides) merged[key] = value;
        }
        strings = merged;
    }

    public string Locale { get; }
    public IReadOnlyCollection<string> MissingKeys => missing;

    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (strings.TryGetValue(key, out string? value)) return value;
        missing.Add(key);
        return $"[{key}]";
    }

    public string Format(string key, params object[] arguments) => string.Format(CultureInfo.GetCultureInfo(Locale), Get(key), arguments);

    public static IReadOnlyDictionary<string, string> CreatePseudoLocalization()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in EnglishStrings) result[key] = Pseudo(value);
        return result;
    }

    private static string Pseudo(string value)
    {
        var builder = new StringBuilder(value.Length * 2);
        builder.Append('⟦');
        foreach (char character in value)
        {
            builder.Append(character switch
            {
                'a' => 'á', 'A' => 'Á', 'e' => 'ë', 'E' => 'Ë', 'i' => 'ï', 'I' => 'Ï',
                'o' => 'ö', 'O' => 'Ö', 'u' => 'ü', 'U' => 'Ü', 'y' => 'ÿ', 'Y' => 'Ÿ',
                _ => character
            });
        }
        builder.Append(" ···⟧");
        return builder.ToString();
    }

    private static readonly IReadOnlyDictionary<string, string> EnglishStrings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["app.title"] = "ROENTGEN",
        ["app.subtitle"] = "A deterministic post-apocalyptic role-playing game",
        ["menu.continue"] = "Continue",
        ["menu.newCampaign"] = "New Campaign",
        ["menu.loadGame"] = "Load Game",
        ["menu.settings"] = "Settings",
        ["menu.accessibility"] = "Accessibility",
        ["menu.tutorials"] = "Tutorials",
        ["menu.credits"] = "Credits and Notices",
        ["menu.quit"] = "Quit",
        ["menu.resume"] = "Resume",
        ["menu.saveGame"] = "Save Game",
        ["menu.returnMain"] = "Return to Main Menu",
        ["menu.confirm"] = "Confirm",
        ["menu.cancel"] = "Cancel",
        ["screen.newCampaign"] = "New Campaign",
        ["screen.attributes"] = "Attributes",
        ["screen.skills"] = "Specialized Skills",
        ["screen.background"] = "Background",
        ["screen.traits"] = "Traits",
        ["screen.review"] = "Review Character",
        ["screen.inventory"] = "Inventory",
        ["screen.equipment"] = "Equipment",
        ["screen.character"] = "Character",
        ["screen.perks"] = "Perks",
        ["screen.map"] = "Regional Map",
        ["screen.journal"] = "Quest Journal",
        ["screen.factions"] = "Factions and Settlements",
        ["screen.crafting"] = "Crafting",
        ["screen.companion"] = "Companion",
        ["screen.settings"] = "Settings",
        ["screen.accessibility"] = "Accessibility",
        ["screen.bindings"] = "Input Bindings",
        ["screen.load"] = "Load Game",
        ["screen.save"] = "Save Game",
        ["screen.focus"] = "Roentgen Focus",
        ["status.noSave"] = "No compatible save is available.",
        ["status.saved"] = "Game saved.",
        ["status.loaded"] = "Game loaded.",
        ["status.backupRecovered"] = "The primary save was damaged. A valid backup was recovered.",
        ["status.focusEmpty"] = "Queue at least one action before committing.",
        ["footer.navigate"] = "Navigate  •  Confirm  •  Cancel",
        ["footer.focus"] = "Change target/part  •  Queue action  •  Remove action  •  Commit  •  Cancel",
        ["confirm.quit"] = "Quit Roentgen? Unsaved progress will be lost.",
        ["confirm.mainMenu"] = "Return to the main menu? Unsaved progress will be lost.",
        ["confirm.deleteSave"] = "Delete this save and its backup?",
        ["credits.body"] = "Roentgen created by Michael Robinson. MonoGame, Playroma Sound, Playroma Music, MeltySynth, and the bundled GeneralUser GS SoundFont are used under their respective licenses and notices."
    };
}
