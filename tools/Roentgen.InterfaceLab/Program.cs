using System.Text.Json;
using Roentgen.UI;

var pseudo = new Phase20LocalizationService("en-US", Phase20LocalizationService.CreatePseudoLocalization());
var screens = new[]
{
    new Phase20ScreenModel(Phase20ScreenKind.MainMenu, pseudo.Get("app.title"), pseudo.Get("app.subtitle"),
        Enumerable.Range(1, 8).Select(i => new Phase20MenuEntry($"menu-{i}", pseudo.Get(i == 1 ? "menu.continue" : "menu.newCampaign"), "VALUE", "A deliberately expanded description used to test wrapping and clipping.")).ToArray(),
        0, [], 0, new Phase20Panel("Diagnostic", [pseudo.Get("credits.body")]), pseudo.Get("footer.navigate"), string.Empty),
    new Phase20ScreenModel(Phase20ScreenKind.Inventory, "Inventory — Pseudo-localized long heading", "Mass, equipment condition, ammunition, and generated provenance",
        Enumerable.Range(1, 60).Select(i => new Phase20MenuEntry($"item-{i}", $"Generated diagnostic component {i:00}", $"{i}.25 kg", "Long procedural item description")).ToArray(),
        0, ["Inventory", "Equipment", "Character", "Skills", "Perks", "Map", "Journal", "Factions", "Crafting", "Companion"], 0,
        new Phase20Panel("Selected Item", ["Condition 91.5%", "Generated provenance preserved", "Repair compatibility available"]), "ENTER SELECT • ESC CLOSE", string.Empty)
};

var reports = new List<InterfaceLayoutReport>();
foreach (Phase20ScreenModel screen in screens)
foreach (InterfaceViewportProfile profile in Phase20LayoutPlanner.AcceptanceMatrix)
    reports.Add(Phase20LayoutPlanner.Analyze(screen, profile));

var output = new
{
    phase = 20,
    profiles = Phase20LayoutPlanner.AcceptanceMatrix.Count,
    screens = screens.Length,
    valid = reports.All(static report => report.IsValid),
    reports
};
Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
return output.valid ? 0 : 1;
