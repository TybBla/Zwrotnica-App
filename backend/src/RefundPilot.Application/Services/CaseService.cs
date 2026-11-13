using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public interface ICaseService
{
    Task<IReadOnlyList<CaseDto>> ListAsync();
    Task<CaseDto?> GetAsync(int id);
    Task<CaseDto> CreateAsync(CreateCaseRequest request);
    Task<CaseDto?> UpdateAsync(int id, UpdateCaseRequest request);
    Task<CaseDto?> SetStatusAsync(int id, CaseStatus status);
    Task<bool> DeleteAsync(int id);
    Task<StatsDto> GetStatsAsync();
}

public sealed class CaseService(
    ICaseRepository repository, ICurrentUser currentUser, IAssessmentService assessments, TimeProvider clock)
    : ICaseService
{
    public async Task<IReadOnlyList<CaseDto>> ListAsync() =>
        currentUser.UserId is { } userId ? (await repository.ListAsync(userId)).Select(ToDto).ToList() : [];

    public async Task<CaseDto?> GetAsync(int id) =>
        await LoadOwnedAsync(id) is { } found ? ToDto(found) : null;

    public async Task<CaseDto> CreateAsync(CreateCaseRequest request)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("Authenticated user required.");
        var refundCase = new RefundCase
        {
            UserId = userId,
            Status = CaseStatus.New,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        Apply(refundCase, request.Carrier, request.TrainCategory, request.FromStation, request.ToStation,
            request.TicketNumber, request.TravelDate, request.TicketPrice, request.IncidentType,
            request.DelayMinutes, request.DelayCause, request.IsSeasonTicket, request.PayoutPreference,
            request.Notes);

        await repository.AddAsync(refundCase);
        return ToDto(refundCase);
    }

    public async Task<CaseDto?> UpdateAsync(int id, UpdateCaseRequest request)
    {
        if (await LoadOwnedAsync(id) is not { } refundCase)
            return null;

        Apply(refundCase, request.Carrier, request.TrainCategory, request.FromStation, request.ToStation,
            request.TicketNumber, request.TravelDate, request.TicketPrice, request.IncidentType,
            request.DelayMinutes, request.DelayCause, request.IsSeasonTicket, request.PayoutPreference,
            request.Notes);

        await repository.UpdateAsync(refundCase);
        return ToDto(refundCase);
    }

    public async Task<CaseDto?> SetStatusAsync(int id, CaseStatus status)
    {
        if (await LoadOwnedAsync(id) is not { } refundCase)
            return null;

        // Leaving Sent keeps FiledAt/ResponseDueDate as history; re-saving Sent does not restart the clock.
        if (status == CaseStatus.Sent && refundCase.Status != CaseStatus.Sent)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            refundCase.FiledAt = now;
            refundCase.ResponseDueDate = now.AddMonths(1);
        }
        refundCase.Status = status;

        await repository.UpdateAsync(refundCase);
        return ToDto(refundCase);
    }

    public async Task<bool> DeleteAsync(int id) =>
        currentUser.UserId is { } userId && await repository.DeleteAsync(id, userId);

    public async Task<StatsDto> GetStatsAsync()
    {
        var cases = currentUser.UserId is { } userId ? await repository.ListAsync(userId) : [];
        // Summed in memory: SQLite stores decimals as TEXT, so server-side Sum is unreliable.
        var recovered = cases.Where(c => c.Status == CaseStatus.Paid).Sum(c => c.CompensationAmount);
        var pending = cases.Where(c => c.Status is CaseStatus.New or CaseStatus.Sent).Sum(c => c.CompensationAmount);
        return new(recovered, pending, cases.Count);
    }

    private Task<RefundCase?> LoadOwnedAsync(int id) =>
        currentUser.UserId is { } userId ? repository.GetAsync(id, userId) : Task.FromResult<RefundCase?>(null);

    private void Apply(
        RefundCase target, string carrier, string? trainCategory, string? fromStation, string? toStation,
        string? ticketNumber, DateOnly travelDate, decimal ticketPrice, IncidentType incidentType,
        int delayMinutes, DelayCause? delayCause, bool isSeasonTicket, PayoutPreference payoutPreference,
        string? notes)
    {
        var assessment = assessments.Assess(new AssessmentRequest(
            carrier, trainCategory, incidentType, delayMinutes, ticketPrice, delayCause, isSeasonTicket));

        target.Carrier = carrier.Trim();
        target.TrainCategory = Clean(trainCategory);
        target.FromStation = Clean(fromStation);
        target.ToStation = Clean(toStation);
        target.TicketNumber = Clean(ticketNumber);
        target.TravelDate = travelDate;
        target.TicketPrice = ticketPrice;
        target.IncidentType = incidentType;
        target.DelayMinutes = delayMinutes;
        target.DelayCause = delayCause;
        target.IsSeasonTicket = isSeasonTicket;
        target.PayoutPreference = payoutPreference;
        target.Notes = Clean(notes);
        target.Verdict = assessment.Verdict;
        target.RefusalRisk = assessment.RefusalRisk;
        target.CompensationAmount = assessment.CompensationAmount;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CaseDto ToDto(RefundCase c) => new(
        c.Id, c.Carrier, c.TrainCategory, c.FromStation, c.ToStation, c.TicketNumber, c.TravelDate,
        c.TicketPrice, c.IncidentType, c.DelayMinutes, c.DelayCause, c.IsSeasonTicket, c.Verdict,
        c.RefusalRisk, c.CompensationAmount, c.PayoutPreference, c.Status, c.FiledAt, c.ResponseDueDate,
        c.Notes, c.CreatedAt);
}
