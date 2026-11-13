using System.Text.Json;
using System.Text.RegularExpressions;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public enum ComplaintStatus { Generated, NotFound, NotEligible }

public sealed record ComplaintResult(ComplaintStatus Status, ComplaintDto? Letter);

public interface IComplaintService
{
    /// <summary>NotFound when the case does not exist or belongs to someone else; NotEligible when no compensation is due.</summary>
    Task<ComplaintResult> GenerateAsync(int caseId, ComplaintRequest request, CancellationToken ct);

    /// <summary>True when the case exists and belongs to the current user.</summary>
    Task<bool> CaseExistsAsync(int caseId);
}

/// <summary>
/// Writes the complaint letter. The AI only sees case facts; the passenger's name, address and account number
/// are tokens that the server substitutes (sanitized) afterwards. Anything unusable from the AI falls back to the template.
/// </summary>
public sealed partial class ComplaintService(
    ICaseRepository cases, IUserProfileStore profiles, ICurrentUser currentUser, IAiClient ai, IUnitOfWork unitOfWork)
    : IComplaintService
{
    internal const string Schema = """
        {
          "type": "object",
          "properties": {
            "subject": { "type": "string" },
            "body": { "type": "string" }
          },
          "required": ["subject", "body"]
        }
        """;

    private const int MaxName = 200, MaxAddress = 300, MaxIban = 34, MaxSubject = 200, MaxBody = 6000;

    private const string PayoutOriginal = "Proszę o wypłatę odszkodowania na rachunek bankowy lub kartę, z których opłacono bilet.";
    private const string PayoutTransfer = "Proszę o wypłatę odszkodowania przelewem na rachunek nr {{IBAN}}.";
    private const string PayoutVoucher = "Proszę o wypłatę odszkodowania w formie bonu.";

    public async Task<bool> CaseExistsAsync(int caseId) =>
        currentUser.UserId is { } id && await cases.GetAsync(caseId, id) is not null;

    public async Task<ComplaintResult> GenerateAsync(int caseId, ComplaintRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId || await cases.GetAsync(caseId, userId) is not { } refundCase)
            return new(ComplaintStatus.NotFound, null);

        // A formal demand makes no sense without compensation due; nothing is written in that case.
        if (refundCase.Verdict != Verdict.Eligible || refundCase.CompensationAmount <= 0)
            return new(ComplaintStatus.NotEligible, null);

        var profile = await profiles.GetAsync(userId) ?? new UserProfile(null, null, null, request.Payout);
        var oneTimeIban = Sanitize(request.OneTimeIban, MaxIban);
        var facts = ToFacts(refundCase);

        var draft = ai.IsConfigured ? await TryAiDraftAsync(facts, request.Payout, ct) : null;
        var (subject, body) = draft ?? ComplaintTemplate.Build(facts);

        var iban = oneTimeIban ?? Sanitize(profile.Iban, MaxIban);
        var (finalBody, missing) = Fill(body, request.Payout, Sanitize(profile.FullName, MaxName),
            Sanitize(profile.Address, MaxAddress), iban);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            refundCase.PayoutPreference = request.Payout;
            await cases.UpdateAsync(refundCase);

            var savedIban = request.SaveIbanToProfile && oneTimeIban is not null ? oneTimeIban : profile.Iban;
            await profiles.UpdateAsync(userId, profile with { Iban = savedIban, PreferredPayout = request.Payout });
        }, ct);

        return new(ComplaintStatus.Generated, new ComplaintDto(subject, finalBody, UsedAi: draft is not null, missing));
    }

    private async Task<(string Subject, string Body)?> TryAiDraftAsync(
        ComplaintFacts facts, PayoutPreference payout, CancellationToken ct)
    {
        try
        {
            var raw = await ai.GenerateJsonAsync(BuildPrompt(facts), [], Schema, ct);
            return Validate(raw, facts, payout);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null; // AI down, throttled, malformed or unusable: the template takes over
        }
    }

    internal static string BuildPrompt(ComplaintFacts f)
    {
        var incident = f.Incident == IncidentType.Cancellation
            ? "The train was cancelled."
            : $"The train arrived at the destination {f.DelayMinutes} minutes late.";
        var cause = f.CauseText is { } c ? $"\n- Cause of the delay (if relevant): {c}" : "";
        return $"""
            You write a formal complaint letter in Polish from a rail passenger to the railway carrier,
            requesting compensation under art. 19 of Regulation (EU) 2021/782.

            Case facts:
            - Carrier: {f.Carrier}
            - Train category: {f.TrainCategory ?? "not given"}
            - Route: {f.From} - {f.To}
            - Travel date: {f.DateText}
            - Ticket number: {f.TicketNumber ?? "not given"}
            - Ticket price: {f.PriceText}
            - Incident: {incident}{cause}
            - Compensation requested: {f.AmountText}

            Rules:
            - Output JSON with "subject" and "body". Write in formal Polish, 150-250 words in the body.
            - Quote the compensation amount exactly as "{f.AmountText}". Do not compute or change any numbers.
            - Cite art. 19 rozporządzenia (UE) 2021/782.
            - Use these literal tokens, each at least once, and do not invent any other tokens:
              {ComplaintTemplate.FullName} (passenger name, in the sender block and the signature),
              {ComplaintTemplate.Address} (passenger address, in the sender block),
              {ComplaintTemplate.Payout} (alone on its own line, where the passenger states how the payout should be made).
            - Never write a name, address or bank account number yourself.
            - Do not add legal claims beyond the facts given.
            """;
    }

    private static (string Subject, string Body)? Validate(string raw, ComplaintFacts facts, PayoutPreference payout)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("subject", out var s) || s.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("body", out var b) || b.ValueKind != JsonValueKind.String)
            return null;

        var subject = s.GetString()!.Trim();
        var body = b.GetString()!.Trim();
        if (subject.Length is 0 or > MaxSubject || body.Length is 0 or > MaxBody || subject.Contains("{{"))
            return null;

        if (!body.Contains(facts.AmountText, StringComparison.Ordinal) || !body.Contains("2021/782", StringComparison.Ordinal))
            return null;

        // The AI is told to use only the supplied numbers; anything else (other sums, account numbers,
        // exclusive payout wording) means it improvised or was steered by case text, so the template is used.
        var allowedMoney = new HashSet<string>
            { NormalizeMoney(facts.AmountText), NormalizeMoney(facts.PriceText) };
        if (MoneyPattern().Matches(body).Any(m => !allowedMoney.Contains(NormalizeMoney(m.Value)))
            || IbanLike().IsMatch(body)
            || body.Contains("wyłącznie", StringComparison.OrdinalIgnoreCase)
            || body.Contains("gotówk", StringComparison.OrdinalIgnoreCase))
            return null;

        var tokens = TokenPattern().Matches(body).Select(m => m.Value).ToHashSet();
        var allowed = new HashSet<string> { ComplaintTemplate.FullName, ComplaintTemplate.Address, ComplaintTemplate.Payout };
        if (payout == PayoutPreference.BankTransfer)
            allowed.Add(ComplaintTemplate.Iban);
        if (!tokens.IsSubsetOf(allowed)
            || !tokens.Contains(ComplaintTemplate.FullName) || !tokens.Contains(ComplaintTemplate.Address)
            || !tokens.Contains(ComplaintTemplate.Payout))
            return null;
        // Any "{{" that is not a well-formed known token (e.g. "{{ X }}") is also rejected.
        if (body.Split("{{").Length - 1 != TokenPattern().Matches(body).Count)
            return null;

        return (subject, body);
    }

    private static (string Body, IReadOnlyList<string> Missing) Fill(
        string body, PayoutPreference payout, string? name, string? address, string? iban)
    {
        // Payout wording is chosen by the server so it is always exactly as agreed, whatever the AI wrote.
        var sentence = payout switch
        {
            PayoutPreference.BankTransfer => PayoutTransfer,
            PayoutPreference.Voucher => PayoutVoucher,
            _ => PayoutOriginal,
        };
        body = body.Replace(ComplaintTemplate.Payout, sentence);

        var missing = new List<string>();
        if (body.Contains(ComplaintTemplate.FullName) && name is null) missing.Add("imię i nazwisko");
        if (body.Contains(ComplaintTemplate.Address) && address is null) missing.Add("adres");
        if (body.Contains(ComplaintTemplate.Iban) && iban is null) missing.Add("numer rachunku");

        // Values are sanitized (no braces), so substituting one after another cannot create new tokens.
        body = body
            .Replace(ComplaintTemplate.FullName, name ?? "[imię i nazwisko]")
            .Replace(ComplaintTemplate.Address, address ?? "[adres]")
            .Replace(ComplaintTemplate.Iban, iban ?? "[numer rachunku]");
        return (body, missing);
    }

    /// <summary>Single line, trimmed, no template braces, capped at <paramref name="max"/> characters; null when empty.</summary>
    internal static string? Sanitize(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = ControlChars().Replace(value, " ");
        text = Whitespace().Replace(text, " ");
        while (text.Contains("{{") || text.Contains("}}"))
            text = text.Replace("{{", "").Replace("}}", "");
        text = text.Trim();
        if (text.Length > max)
            text = text[..max].TrimEnd();
        return text.Length == 0 ? null : text;
    }

    private static ComplaintFacts ToFacts(RefundCase c) => new(
        c.Carrier, c.TrainCategory, c.FromStation, c.ToStation, c.TravelDate, c.TicketNumber, c.TicketPrice,
        c.CompensationAmount, c.IncidentType, c.DelayMinutes, c.DelayCause);

    private static string NormalizeMoney(string value) => Whitespace().Replace(value, "");

    [GeneratedRegex(@"\d+(?:[\s.]\d{3})*(?:,\d{2})?\s*(?:zł|PLN)", RegexOptions.IgnoreCase)]
    private static partial Regex MoneyPattern();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[\d ]{10,}")]
    private static partial Regex IbanLike();

    [GeneratedRegex(@"\{\{[A-Z_]+\}\}")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"[\p{Cc}\p{Cf}]")]
    private static partial Regex ControlChars();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
