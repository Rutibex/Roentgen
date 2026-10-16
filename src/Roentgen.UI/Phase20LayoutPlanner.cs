using System.Text;

namespace Roentgen.UI;

public sealed record InterfaceViewportProfile(int Width, int Height, float Scale, string Name)
{
    public InterfaceViewportProfile Validate()
    {
        if (Width < 640 || Height < 360) throw new ArgumentOutOfRangeException(nameof(Width));
        if (!float.IsFinite(Scale) || Scale is < 0.75f or > 2f) throw new ArgumentOutOfRangeException(nameof(Scale));
        return this;
    }
}

public sealed record InterfaceLayoutReport(
    InterfaceViewportProfile Profile,
    int SafeWidth,
    int SafeHeight,
    int VisibleEntryCount,
    bool UsesTwoColumns,
    bool RequiresScrolling,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Warnings.Count == 0;
}

/// <summary>Pure CPU layout budget model used by InterfaceLab and automated tests.</summary>
public static class Phase20LayoutPlanner
{
    public static IReadOnlyList<InterfaceViewportProfile> AcceptanceMatrix { get; } =
    [
        new(1280, 720, 0.75f, "720p-small"),
        new(1280, 720, 1.00f, "720p-default"),
        new(1920, 1080, 1.00f, "1080p-default"),
        new(2560, 1440, 1.25f, "1440p-large"),
        new(3440, 1440, 1.00f, "ultrawide"),
        new(900, 900, 1.00f, "narrow-window"),
        new(1920, 1080, 2.00f, "1080p-maximum-scale")
    ];

    public static InterfaceLayoutReport Analyze(Phase20ScreenModel model, InterfaceViewportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(model);
        profile.Validate();
        float scale = profile.Scale;
        int margin = checked((int)MathF.Ceiling(28f * scale));
        int safeWidth = Math.Max(1, profile.Width - margin * 2);
        int safeHeight = Math.Max(1, profile.Height - margin * 2);
        int header = checked((int)MathF.Ceiling(116f * scale));
        int tabs = model.Tabs.Count == 0 ? 0 : checked((int)MathF.Ceiling(42f * scale));
        int footer = checked((int)MathF.Ceiling(64f * scale));
        int row = checked((int)MathF.Ceiling(40f * scale));
        int availableRows = Math.Max(1, (safeHeight - header - tabs - footer) / Math.Max(1, row));
        bool twoColumns = model.Detail is not null && safeWidth >= checked((int)MathF.Ceiling(980f * scale));
        var warnings = new List<string>();
        if (safeWidth < checked((int)MathF.Ceiling(560f * scale))) warnings.Add("safe-width-below-interface-minimum");
        if (safeHeight < checked((int)MathF.Ceiling(320f * scale))) warnings.Add("safe-height-below-interface-minimum");
        if (EstimateTextWidth(model.Title, scale) > safeWidth) warnings.Add("title-overflow");
        if (EstimateTextWidth(model.Footer, scale) > safeWidth) warnings.Add("footer-overflow");
        return new InterfaceLayoutReport(profile, safeWidth, safeHeight, availableRows, twoColumns, model.Entries.Count > availableRows, warnings);
    }

    public static int EstimateTextWidth(string? text, float scale)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int glyphs = text.EnumerateRunes().Count();
        return checked((int)MathF.Ceiling(glyphs * 8f * Math.Clamp(scale, 0.75f, 2f)));
    }
}
