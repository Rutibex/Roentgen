using Roentgen.UI;
using Roentgen.Entities;

namespace Roentgen.App;

internal readonly record struct HudFeedbackObservation(bool NotificationAdded, bool QuestAdvanced);

/// <summary>
/// Presentation-only collector that converts short-lived world feedback into a bounded toast queue.
/// It never changes player, narrative, campaign, or item state.
/// </summary>
internal sealed class Phase17HudFeedbackRuntime
{
    private readonly List<HudToast> _toasts = [];
    private string _lastNotification = string.Empty;
    private string _lastQuestSignature = string.Empty;
    private int _sequence;

    public void Reset()
    {
        _toasts.Clear();
        _lastNotification = string.Empty;
        _lastQuestSignature = string.Empty;
        _sequence = 0;
    }

    public HudFeedbackObservation Observe(int interfaceFrame, PlayerPresentationSnapshot snapshot, QuestTrackerHudModel? tracker)
    {
        bool notificationAdded = false;
        bool questAdvanced = false;
        string notification = snapshot.Notification?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(notification) && !string.Equals(notification, _lastNotification, StringComparison.Ordinal))
        {
            HudToastKind kind = Classify(notification);
            Enqueue(interfaceFrame, ToastTitle(kind), notification, kind);
            notificationAdded = true;
        }
        _lastNotification = notification;

        string signature = tracker is null ? string.Empty : $"{tracker.Title}\n{tracker.Objective}";
        if (!string.IsNullOrWhiteSpace(signature) && !string.IsNullOrWhiteSpace(_lastQuestSignature) &&
            !string.Equals(signature, _lastQuestSignature, StringComparison.Ordinal))
        {
            Enqueue(interfaceFrame, "Objective Updated", tracker!.Objective, HudToastKind.Quest, lifetimeFrames: 360);
            questAdvanced = true;
        }
        _lastQuestSignature = signature;
        RemoveExpired(interfaceFrame);
        return new HudFeedbackObservation(notificationAdded, questAdvanced);
    }

    public void Enqueue(int interfaceFrame, string title, string body, HudToastKind kind, int lifetimeFrames = 270)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        string normalized = body.Trim();
        if (_toasts.Any(value => !value.IsExpired(interfaceFrame) && string.Equals(value.Body, normalized, StringComparison.OrdinalIgnoreCase))) return;
        _toasts.Add(new HudToast($"toast-{++_sequence}", title, normalized, kind, interfaceFrame, Math.Clamp(lifetimeFrames, 90, 720)));
        if (_toasts.Count > 8) _toasts.RemoveRange(0, _toasts.Count - 8);
    }

    public HudWorldFeedbackModel Build(int interfaceFrame)
    {
        RemoveExpired(interfaceFrame);
        return new HudWorldFeedbackModel(_toasts.ToArray());
    }

    private void RemoveExpired(int interfaceFrame) => _toasts.RemoveAll(value => value.IsExpired(interfaceFrame));

    private static HudToastKind Classify(string text)
    {
        if (ContainsAny(text, "quest", "objective", "mission")) return HudToastKind.Quest;
        if (ContainsAny(text, "reputation", "faction", "standing", "trust", "respect")) return HudToastKind.Reputation;
        if (ContainsAny(text, "skill", "perk", "level", "specialization")) return HudToastKind.Progression;
        if (ContainsAny(text, "picked", "pickup", "received", "took", "taken", "loot", "added to inventory")) return HudToastKind.Loot;
        if (ContainsAny(text, "discovered", "location", "waypoint")) return HudToastKind.Location;
        if (ContainsAny(text, "failed", "cannot", "can't", "blocked", "locked", "jammed", "unavailable")) return HudToastKind.Warning;
        if (ContainsAny(text, "error", "invalid")) return HudToastKind.Error;
        return HudToastKind.Information;
    }

    private static string ToastTitle(HudToastKind kind) => kind switch
    {
        HudToastKind.Loot => "Inventory",
        HudToastKind.Quest => "Journal",
        HudToastKind.Reputation => "Reputation",
        HudToastKind.Progression => "Character",
        HudToastKind.Location => "Location",
        HudToastKind.Warning => "Notice",
        HudToastKind.Error => "Error",
        _ => "Update"
    };

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
}
