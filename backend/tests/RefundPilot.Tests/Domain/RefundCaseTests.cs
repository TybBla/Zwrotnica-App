using RefundPilot.Domain;

namespace RefundPilot.Tests.Domain;

public sealed class RefundCaseTests
{
    [Fact]
    public void MarkSent_StartsANewMonth_AndForgetsTheReminderOfAnEarlierFiling()
    {
        var filedAt = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
        var c = new RefundCase { Status = CaseStatus.New, ReminderSentAt = filedAt.AddDays(-2) };

        c.MarkSent(filedAt);

        Assert.Equal(CaseStatus.Sent, c.Status);
        Assert.Equal(filedAt, c.FiledAt);
        Assert.Equal(new DateTime(2026, 12, 13, 10, 0, 0, DateTimeKind.Utc), c.ResponseDueDate);
        Assert.Null(c.ReminderSentAt);
    }
}
