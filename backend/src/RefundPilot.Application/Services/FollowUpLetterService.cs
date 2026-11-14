using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public enum FollowUpStatus { Generated, NotFound, NotAvailable }

/// <summary>Letter (generate) or Pdf (render) when Generated.</summary>
public sealed record FollowUpResult(FollowUpStatus Status, ComplaintDto? Letter = null, byte[]? Pdf = null);

public interface IFollowUpLetterService
{
    /// <summary>NotFound for a case that is not the current user's; NotAvailable when the case status does not allow this letter.</summary>
    Task<FollowUpResult> GenerateAsync(int caseId, FollowUpKind kind);

    /// <summary>Renders the posted (possibly edited) letter as PDF under the same ownership and availability rules. No writes.</summary>
    Task<FollowUpResult> RenderPdfAsync(int caseId, FollowUpKind kind, ComplaintPdfRequest request);
}

/// <summary>
/// Ponaglenie and skarga do Prezesa UTK: deterministic templates (no AI), the same token substitution and missing-field
/// list as the first letter, the case's payout preference and nothing written to the case.
/// </summary>
public sealed class FollowUpLetterService(
    ICaseRepository cases, IUserProfileStore profiles, ICurrentUser currentUser, IComplaintPdfRenderer pdf, TimeProvider clock)
    : IFollowUpLetterService
{
    /// <summary>The 409 explanation, in Polish, for a letter the case does not allow yet.</summary>
    public static string UnavailableMessage(FollowUpKind kind) => kind == FollowUpKind.Reminder
        ? "Ponaglenie przygotujemy dla sprawy o statusie „Wysłana”."
        : "Skargę do UTK przygotujemy, gdy przewoźnik odrzuci pismo (status „Odrzucona”) albo minie termin jego odpowiedzi w sprawie o statusie „Wysłana”.";

    public async Task<FollowUpResult> GenerateAsync(int caseId, FollowUpKind kind)
    {
        if (await LoadAsync(caseId, kind) is not ({ } userId, { } refundCase, var status))
            return new(FollowUpStatus.NotFound);
        if (status != FollowUpStatus.Generated)
            return new(status);

        var letterKind = ComplaintTemplate.KindOf(refundCase);
        var facts = new FollowUpFacts(ComplaintService.ToFacts(refundCase), letterKind,
            refundCase.FiledAt is { } filedAt ? WarsawClock.DateOf(filedAt) : null,
            FollowUpRules.PastDeadline(refundCase, WarsawClock.Today(clock)),
            refundCase.Status == CaseStatus.Rejected);
        var (subject, body) = kind == FollowUpKind.Reminder
            ? FollowUpTemplate.BuildReminder(facts)
            : FollowUpTemplate.BuildUtkComplaint(facts);

        var profile = await profiles.GetAsync(userId);
        // The regulator pays nothing, so only the reminder carries a payout sentence (as chosen for the case).
        var payout = kind == FollowUpKind.Reminder
            ? ComplaintTemplate.PayoutSentence(letterKind, refundCase.PayoutPreference, refundCase.IncidentType, refundCase.Verdict)
            : "";
        var (finalBody, filled) = ComplaintService.Fill(body, payout,
            ComplaintService.Sanitize(profile?.FullName, ComplaintService.MaxName),
            ComplaintService.Sanitize(profile?.Address, ComplaintService.MaxAddress),
            ComplaintService.Sanitize(profile?.Iban, ComplaintService.MaxIban), expenseDescription: null);

        var missing = filled.ToList();
        if (facts.FiledDate is null)
            missing.Add("data wysłania pisma");
        if (kind == FollowUpKind.UtkComplaint)
            missing.Add(facts.Rejected ? "załączniki – kopie pisma, biletu i odpowiedzi przewoźnika" : "załączniki – kopie pisma i biletu");

        return new(FollowUpStatus.Generated, new ComplaintDto(subject, finalBody, UsedAi: false, missing));
    }

    public async Task<FollowUpResult> RenderPdfAsync(int caseId, FollowUpKind kind, ComplaintPdfRequest request)
    {
        if (await LoadAsync(caseId, kind) is not ({ } userId, { } refundCase, var status))
            return new(FollowUpStatus.NotFound);
        if (status != FollowUpStatus.Generated)
            return new(status);

        return new(FollowUpStatus.Generated, Pdf: await LetterPdf.RenderAsync(profiles, pdf, clock, userId, refundCase, request));
    }

    /// <summary>The user's case and whether the letter is available for it; null when the case is not theirs.</summary>
    private async Task<(string UserId, RefundCase Case, FollowUpStatus Status)?> LoadAsync(int caseId, FollowUpKind kind)
    {
        if (currentUser.UserId is not { } userId || await cases.GetAsync(caseId, userId) is not { } refundCase)
            return null;
        var available = FollowUpRules.IsAvailable(kind, refundCase, WarsawClock.Today(clock));
        return (userId, refundCase, available ? FollowUpStatus.Generated : FollowUpStatus.NotAvailable);
    }
}
