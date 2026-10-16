using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record ComponentSelection(WeaponComponentSlot Slot, string ComponentDefinitionId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Slot); writer.WriteString(ComponentDefinitionId); }
    public static ComponentSelection ReadCanonical(CanonicalReader reader) => new((WeaponComponentSlot)reader.ReadInt32(), reader.ReadRequiredString());
}

public sealed record MaterialSelection(string Role, string MaterialProfileId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Role); writer.WriteString(MaterialProfileId); }
    public static MaterialSelection ReadCanonical(CanonicalReader reader) => new(reader.ReadRequiredString(), reader.ReadRequiredString());
}

public sealed record ItemAssetDescriptor(string MeshRecipeId, string MaterialRecipeId, string AttachmentProfileId, string DamageMaskDomain) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(MeshRecipeId); writer.WriteString(MaterialRecipeId); writer.WriteString(AttachmentProfileId); writer.WriteString(DamageMaskDomain); }
    public static ItemAssetDescriptor ReadCanonical(CanonicalReader reader) => new(reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadRequiredString());
}

public sealed record GeneratedItemBlueprint(
    string DefinitionFamilyId, string DisplayIdentity, CampaignSeed ItemSeed, int GenerationVersion,
    IReadOnlyList<ComponentSelection> Components, IReadOnlyList<MaterialSelection> Materials,
    ItemProvenance Provenance, ItemAssetDescriptor Assets, StateChecksum Fingerprint) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(DefinitionFamilyId); writer.WriteString(DisplayIdentity); writer.WriteUInt64(ItemSeed.Value); writer.WriteInt32(GenerationVersion);
        writer.WriteCount(Components.Count); foreach (ComponentSelection component in Components.OrderBy(static value => value.Slot)) component.WriteCanonical(writer);
        writer.WriteCount(Materials.Count); foreach (MaterialSelection material in Materials.OrderBy(static value => value.Role, StringComparer.Ordinal)) material.WriteCanonical(writer);
        Provenance.WriteCanonical(writer); Assets.WriteCanonical(writer); writer.WriteChecksum(Fingerprint);
    }

    public static GeneratedItemBlueprint ReadCanonical(CanonicalReader reader)
    {
        string family = reader.ReadRequiredString();
        string identity = reader.ReadRequiredString();
        var seed = new CampaignSeed(reader.ReadUInt64());
        int version = reader.ReadInt32();
        int componentCount = reader.ReadCount();
        var components = new ComponentSelection[componentCount];
        for (int i = 0; i < componentCount; i++) components[i] = ComponentSelection.ReadCanonical(reader);
        int materialCount = reader.ReadCount();
        var materials = new MaterialSelection[materialCount];
        for (int i = 0; i < materialCount; i++) materials[i] = MaterialSelection.ReadCanonical(reader);
        ItemProvenance provenance = ItemProvenance.ReadCanonical(reader);
        ItemAssetDescriptor assets = ItemAssetDescriptor.ReadCanonical(reader);
        StateChecksum fingerprint = reader.ReadChecksum();
        return new GeneratedItemBlueprint(family, identity, seed, version, components, materials, provenance, assets, fingerprint);
    }
}

public sealed class ItemBlueprintGenerator
{
    public const int SchemaVersion = 1;
    public GeneratedItemBlueprint GenerateWeapon(CampaignSeed seed, WeaponFamilyDefinition family, IReadOnlyCollection<WeaponComponentDefinition> catalog, string manufacturerHistory = "field manufacture", string factionAlteration = "none")
    {
        ArgumentNullException.ThrowIfNull(family); ArgumentNullException.ThrowIfNull(catalog);
        var selected = new List<ComponentSelection>();
        foreach (WeaponComponentSlot slot in family.RequiredSlots.Order())
        {
            WeaponComponentDefinition[] candidates = catalog.Where(value => value.Slot == slot && (value.CompatibleFamilyIds.Count == 0 || value.CompatibleFamilyIds.Contains(family.Id))).OrderBy(static value => value.Id, StringComparer.Ordinal).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException($"Weapon family '{family.Id}' has no component candidate for '{slot}'.");
            int index = Select(seed, $"item.components.{family.Id}.{slot}", candidates.Length);
            selected.Add(new ComponentSelection(slot, candidates[index].Id));
        }
        string material = Select(seed, "item.materials.primary", 3) switch { 0 => "material.blued-steel", 1 => "material.salvaged-alloy", _ => "material.oxidized-industrial" };
        string finish = Select(seed, "item.materials.finish", 3) switch { 0 => "finish.bare", 1 => "finish.chipped-enamel", _ => "finish.faction-marked" };
        var materials = new[] { new MaterialSelection("primary", material), new MaterialSelection("finish", finish) };
        var provenance = new ItemProvenance("roentgen.item.weapon", SchemaVersion, seed, $"item.{family.Id}", manufacturerHistory, factionAlteration);
        var assets = new ItemAssetDescriptor($"weapon/{family.Id}", $"{material}/{finish}", family.Id.Contains("rifle", StringComparison.Ordinal) ? "two-hand-longarm" : "one-hand-sidearm", "wear");
        string identity = $"{FinishName(finish)} {family.DisplayName}";
        var writer = new CanonicalWriter(); writer.WriteString(family.Id); writer.WriteUInt64(seed.Value); writer.WriteInt32(SchemaVersion); writer.WriteCount(selected.Count); foreach (ComponentSelection item in selected) item.WriteCanonical(writer); foreach (MaterialSelection item in materials) item.WriteCanonical(writer); provenance.WriteCanonical(writer); assets.WriteCanonical(writer);
        return new GeneratedItemBlueprint(family.Id, identity, seed, SchemaVersion, selected, materials, provenance, assets, writer.ComputeChecksum());
    }
    private static int Select(CampaignSeed seed, string domain, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var writer = new CanonicalWriter(); writer.WriteString("roentgen.item.select"); writer.WriteInt32(SchemaVersion); writer.WriteUInt64(seed.Value); writer.WriteString(domain); StateChecksum hash = writer.ComputeChecksum(); return (int)(hash.A % (ulong)count);
    }
    private static string FinishName(string finish) => finish switch { "finish.bare" => "Bare", "finish.chipped-enamel" => "Enamel-Worn", _ => "Marked" };
}
