using RefundPilot.Application;
using RefundPilot.Domain;

namespace RefundPilot.Tests.Application;

public sealed class FollowUpRulesTests
{
    // 10:00 UTC = 11:00 in Warsaw (CET): the deadline is the Warsaw date 13.12.2026.
    private static readonly DateTime DueMidday = new(2026, 12, 13, 10, 0, 0, DateTimeKind.Utc);

    private static RefundCase Case(CaseStatus status, DateTime? due) =>
        new() { Status = status, ResponseDueDate = due, FiledAt = due?.AddMonths(-1) };

    [Fact]
    public void PastDeadline_OnlyFromTheDayAfterTheWarsawDueDate()
    {
        var sent = Case(CaseStatus.Sent, DueMidday);

        Assert.False(FollowUpRules.PastDeadline(sent, new DateOnly(2026, 12, 12)));
        Assert.False(FollowUpRules.PastDeadline(sent, new DateOnly(2026, 12, 13))); // the carrier has the whole last day
        Assert.True(FollowUpRules.PastDeadline(sent, new DateOnly(2026, 12, 14)));
    }

    [Fact]
    public void PastDeadline_UsesTheWarsawDateOfTheDeadline_NotTheUtcOne()
    {
        // 23:30 UTC on 13.12 is already 00:30 on 14.12 in Warsaw.
        var sent = Case(CaseStatus.Sent, new DateTime(2026, 12, 13, 23, 30, 0, DateTimeKind.Utc));

        Assert.False(FollowUpRules.PastDeadline(sent, new DateOnly(2026, 12, 14)));
        Assert.True(FollowUpRules.PastDeadline(sent, new DateOnly(2026, 12, 15)));
    }

    [Theory]
    [InlineData(CaseStatus.New)]
    [InlineData(CaseStatus.Paid)]
    [InlineData(CaseStatus.Rejected)]
    public void PastDeadline_IsOnlyForSentCases(CaseStatus status) =>
        Assert.False(FollowUpRules.PastDeadline(Case(status, DueMidday), new DateOnly(2027, 1, 30)));

    [Fact]
    public void PastDeadline_FalseWithoutADeadline() =>
        Assert.False(FollowUpRules.PastDeadline(Case(CaseStatus.Sent, null), new DateOnly(2027, 1, 30)));

    [Theory]
    // status, today, reminder, utk
    [InlineData(CaseStatus.New, "2026-12-20", false, false)]
    [InlineData(CaseStatus.Sent, "2026-12-01", true, false)]  // before the deadline: a reminder only (with a note in the UI)
    [InlineData(CaseStatus.Sent, "2026-12-13", true, false)]  // the due day itself
    [InlineData(CaseStatus.Sent, "2026-12-14", true, true)]   // past the deadline
    [InlineData(CaseStatus.Rejected, "2026-12-01", false, true)]
    [InlineData(CaseStatus.Paid, "2026-12-20", false, false)]
    public void Availability_PerStatusAndDate(CaseStatus status, string today, bool reminder, bool utk)
    {
        var refundCase = Case(status, DueMidday);
        var date = DateOnly.Parse(today);

        Assert.Equal(reminder, FollowUpRules.IsAvailable(FollowUpKind.Reminder, refundCase, date));
        Assert.Equal(utk, FollowUpRules.IsAvailable(FollowUpKind.UtkComplaint, refundCase, date));
        var dto = FollowUpRules.Of(refundCase, date);
        Assert.Equal(reminder, dto.Reminder);
        Assert.Equal(utk, dto.UtkComplaint);
    }

    [Fact]
    public void RejectedWithoutAnyFiling_StillAllowsTheUtkComplaint() =>
        Assert.True(FollowUpRules.IsAvailable(FollowUpKind.UtkComplaint, Case(CaseStatus.Rejected, null), new DateOnly(2026, 12, 1)));

    [Theory]
    [InlineData("reminder", FollowUpKind.Reminder)]
    [InlineData("utk-complaint", FollowUpKind.UtkComplaint)]
    public void Slugs_RoundTrip(string slug, FollowUpKind kind)
    {
        Assert.True(FollowUpRules.TryParseKind(slug, out var parsed));
        Assert.Equal(kind, parsed);
        Assert.Equal(slug, FollowUpRules.Slug(kind));
    }

    [Theory]
    [InlineData("Reminder")]
    [InlineData("utk")]
    [InlineData("")]
    [InlineData("0")]
    public void Slugs_UnknownAreRejected(string slug) => Assert.False(FollowUpRules.TryParseKind(slug, out _));
}
