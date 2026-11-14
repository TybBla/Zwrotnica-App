namespace RefundPilot.Domain;

public sealed class RefundCase
{
    /// <summary>Regulation (EU) 2021/782: the carrier answers a complaint within one month.</summary>
    public const int ResponseMonths = 1;

    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string? TrainCategory { get; set; }
    public string? FromStation { get; set; }
    public string? ToStation { get; set; }
    public string? TicketNumber { get; set; }
    public DateOnly TravelDate { get; set; }
    public decimal TicketPrice { get; set; }
    public IncidentType IncidentType { get; set; }
    public int DelayMinutes { get; set; }
    public DelayCause? DelayCause { get; set; }
    public bool IsSeasonTicket { get; set; }
    public Verdict Verdict { get; set; }
    public bool RefusalRisk { get; set; }
    public decimal CompensationAmount { get; set; }
    public PayoutPreference PayoutPreference { get; set; }
    public CaseStatus Status { get; set; }
    public DateTime? FiledAt { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Marks the case as sent at <paramref name="filedAt"/> and starts the carrier's month to respond.</summary>
    public void MarkSent(DateTime filedAt)
    {
        Status = CaseStatus.Sent;
        FiledAt = filedAt;
        ResponseDueDate = filedAt.AddMonths(ResponseMonths);
    }
}
