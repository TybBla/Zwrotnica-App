using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application;

/// <summary>Reminder: "ponaglenie" to the carrier. UtkComplaint: "skarga do Prezesa UTK".</summary>
public enum FollowUpKind { Reminder, UtkComplaint }

/// <summary>When the letters after the first one make sense. Dates are Europe/Warsaw calendar days.</summary>
public static class FollowUpRules
{
    private static readonly Dictionary<string, FollowUpKind> Slugs = new(StringComparer.Ordinal)
    {
        ["reminder"] = FollowUpKind.Reminder,
        ["utk-complaint"] = FollowUpKind.UtkComplaint,
    };

    /// <summary>
    /// A Sent case whose deadline ended before <paramref name="today"/>. The carrier has the whole last day
    /// (art. 112 KC), the same rule as the reminder e-mail.
    /// </summary>
    public static bool PastDeadline(RefundCase c, DateOnly today) =>
        c.Status == CaseStatus.Sent && c.ResponseDueDate is { } due && WarsawClock.DateOf(due) < today;

    /// <summary>
    /// A reminder for any Sent case (before the deadline the UI asks to confirm); a complaint to UTK once the carrier
    /// rejected the claim or let its deadline pass.
    /// </summary>
    public static bool IsAvailable(FollowUpKind kind, RefundCase c, DateOnly today) => kind switch
    {
        FollowUpKind.Reminder => c.Status == CaseStatus.Sent,
        _ => c.Status == CaseStatus.Rejected || PastDeadline(c, today),
    };

    public static FollowUpsDto Of(RefundCase c, DateOnly today) => new(
        IsAvailable(FollowUpKind.Reminder, c, today),
        IsAvailable(FollowUpKind.UtkComplaint, c, today),
        PastDeadline(c, today));

    /// <summary>The URL segment ("reminder", "utk-complaint"); anything else is not a letter we write.</summary>
    public static bool TryParseKind(string slug, out FollowUpKind kind) => Slugs.TryGetValue(slug, out kind);

    public static string Slug(FollowUpKind kind) => Slugs.Single(pair => pair.Value == kind).Key;
}
