using Microsoft.Xna.Framework;

namespace Roentgen.UI;

public enum UiDetailPlacement { None, Right, Below }

/// <summary>Shared layout result used by model-driven screens while they migrate to explicit widgets.</summary>
public sealed record UiLayoutMetrics(
    Rectangle Frame,
    Rectangle HeaderArea,
    Rectangle TabArea,
    Rectangle ContentArea,
    Rectangle EntriesArea,
    Rectangle DetailArea,
    Rectangle StatusArea,
    Rectangle ActionArea,
    UiDetailPlacement DetailPlacement,
    int Margin,
    int Gap);

public static class UiResponsiveLayout
{
    /// <summary>
    /// Computes a bounded safe-area layout. No rectangle is intentionally larger than the available
    /// viewport, and narrow windows reflow detail content below the primary pane instead of dropping it.
    /// tabCount is optional so existing callers/tests remain source-compatible.
    /// </summary>
    public static UiLayoutMetrics Compute(int width, int height, float interfaceScale, bool hasTabs, bool hasDetail, int tabCount = 0)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        float scale = Math.Clamp(interfaceScale, 0.75f, 2f);
        int maximumMargin = Math.Max(2, Math.Min(width, height) / 8);
        int margin = Math.Clamp((int)MathF.Round(24 * MathF.Min(scale, 1.5f)), 8, Math.Max(8, maximumMargin));
        int gap = Math.Clamp((int)MathF.Round(12 * MathF.Min(scale, 1.5f)), 6, 24);
        int frameWidth = Math.Max(1, width - margin * 2);
        int frameHeight = Math.Max(1, height - margin * 2);
        Rectangle frame = new(margin, margin, frameWidth, frameHeight);

        int desiredHeader = Math.Clamp((int)MathF.Round(78 * scale), 58, 138);
        int footerHeight = Math.Clamp((int)MathF.Round(64 * scale), 48, 106);
        int rowHeight = Math.Clamp((int)MathF.Round(32 * scale), 28, 58);
        int estimatedTabsPerRow = Math.Max(1, (frame.Width - 36) / Math.Max(90, (int)MathF.Round(118 * MathF.Min(scale, 1.3f))));
        int tabRows = !hasTabs ? 0 : Math.Max(1, Math.Min(4, (int)Math.Ceiling(Math.Max(1, tabCount) / (double)estimatedTabsPerRow)));
        if (hasTabs && tabCount <= 0) tabRows = frame.Width < 1050 * scale ? 2 : 1;
        int tabHeight = hasTabs ? rowHeight * tabRows + Math.Max(0, tabRows - 1) * 4 : 0;

        // Reserve a usable content band even at unusually small window sizes.
        int minimumContent = Math.Min(120, Math.Max(40, frame.Height / 3));
        int chrome = desiredHeader + tabHeight + (hasTabs ? gap : 0) + footerHeight;
        if (chrome + minimumContent > frame.Height)
        {
            int excess = chrome + minimumContent - frame.Height;
            int headerReduction = Math.Min(excess, Math.Max(0, desiredHeader - 48));
            desiredHeader -= headerReduction;
            excess -= headerReduction;
            int footerReduction = Math.Min(excess, Math.Max(0, footerHeight - 42));
            footerHeight -= footerReduction;
        }

        int innerWidth = Math.Max(1, frame.Width - 36);
        Rectangle header = new(frame.X + Math.Min(18, frame.Width / 8), frame.Y + Math.Min(12, frame.Height / 10), innerWidth, Math.Max(1, desiredHeader - 10));
        Rectangle tabs = new(header.X, frame.Y + desiredHeader, innerWidth, Math.Max(0, Math.Min(tabHeight, frame.Bottom - (frame.Y + desiredHeader))));
        int contentY = Math.Min(frame.Bottom, frame.Y + desiredHeader + tabHeight + (hasTabs ? gap : 0));
        int contentBottom = Math.Max(contentY, frame.Bottom - footerHeight);
        Rectangle content = new(header.X, contentY, innerWidth, Math.Max(1, contentBottom - contentY));
        Rectangle status = new(frame.X + Math.Min(20, frame.Width / 8), contentBottom + 4, Math.Max(1, frame.Width - Math.Min(40, frame.Width / 4)), Math.Max(16, footerHeight / 2 - 4));
        Rectangle actions = new(status.X, Math.Min(frame.Bottom - 1, contentBottom + footerHeight / 2), status.Width, Math.Max(16, footerHeight / 2 - 4));

        if (!hasDetail)
            return new(frame, header, tabs, content, content, Rectangle.Empty, status, actions, UiDetailPlacement.None, margin, gap);

        // Wide: familiar left-list/right-detail layout. Narrow/tall: preserve detail below.
        bool useRight = frame.Width >= Math.Max(860, (int)MathF.Round(900 * MathF.Min(scale, 1.35f))) && content.Width >= 620;
        if (useRight)
        {
            int preferred = (int)(content.Width * (content.Width > 1800 ? 0.36f : 0.42f));
            int minimumDetail = Math.Min(300, Math.Max(180, content.Width / 3));
            int maximumDetail = Math.Max(minimumDetail, content.Width - Math.Min(320, Math.Max(220, content.Width / 2)) - gap);
            int detailWidth = Math.Clamp(preferred, minimumDetail, maximumDetail);
            int entryWidth = Math.Max(1, content.Width - detailWidth - gap);
            Rectangle entries = new(content.X, content.Y, entryWidth, content.Height);
            Rectangle detail = new(Math.Min(content.Right, entries.Right + gap), content.Y, Math.Max(1, content.Right - entries.Right - gap), content.Height);
            return new(frame, header, tabs, content, entries, detail, status, actions, UiDetailPlacement.Right, margin, gap);
        }

        int entriesHeight = Math.Clamp((int)(content.Height * 0.52f), Math.Min(80, content.Height), Math.Max(1, content.Height - gap - Math.Min(60, content.Height / 3)));
        int detailY = Math.Min(content.Bottom, content.Y + entriesHeight + gap);
        Rectangle belowEntries = new(content.X, content.Y, content.Width, Math.Max(1, entriesHeight));
        Rectangle belowDetail = new(content.X, detailY, content.Width, Math.Max(1, content.Bottom - detailY));
        return new(frame, header, tabs, content, belowEntries, belowDetail, status, actions, UiDetailPlacement.Below, margin, gap);
    }
}
