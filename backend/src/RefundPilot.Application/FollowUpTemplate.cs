using System.Globalization;
using RefundPilot.Domain;

namespace RefundPilot.Application;

/// <summary>
/// The case facts plus what happened after the first letter: when it was sent (null when unknown), whether the carrier's
/// deadline has passed and whether the carrier rejected it.
/// </summary>
public sealed record FollowUpFacts(ComplaintFacts Case, LetterKind Kind, DateOnly? FiledDate, bool PastDeadline, bool Rejected)
{
    public string FiledText => FiledDate?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? FollowUpTemplate.FiledPlaceholder;
}

/// <summary>
/// Deterministic letters after the first one (no AI): a reminder to the carrier and a complaint to the rail regulator.
/// They repeat what the first letter claimed (the stored amount, never a new calculation) and use the same tokens
/// ({{FULL_NAME}}, {{ADDRESS}}, {{PAYOUT}}) as <see cref="ComplaintTemplate"/>.
/// </summary>
public static class FollowUpTemplate
{
    public const string FiledPlaceholder = "[data wysłania pisma]";
    public const string ReplyAttachment = "[kopia odpowiedzi przewoźnika]";

    private const string Regulation =
        "rozporządzenia (UE) 2021/782 Parlamentu Europejskiego i Rady z dnia 29 kwietnia 2021 r. w sprawie praw i obowiązków pasażerów kolejowych";

    /// <summary>How the first letter is named: nominative (subject), accusative ("złożyłem …") and genitive ("rozpatrzenie …").</summary>
    private sealed record Document(string Nominative, string Accusative, string Genitive);

    private static Document DocumentOf(FollowUpFacts f) => f.Kind switch
    {
        LetterKind.Compensation when f.Case.Incident == IncidentType.Cancellation => new(
            "wniosek o zwrot należności za bilet", "wniosek o zwrot należności za bilet", "mojego wniosku"),
        LetterKind.Compensation => new(
            "wniosek o wypłatę odszkodowania", "wniosek o wypłatę odszkodowania", "mojego wniosku"),
        LetterKind.DocumentedCosts => new(
            "wniosek o zwrot kosztów", "wniosek o zwrot udokumentowanych kosztów", "mojego wniosku"),
        _ => new("reklamacja", "reklamację", "mojej reklamacji"),
    };

    /// <summary>What the first letter asked for, with the stored amount; null for a complaint under the carrier's rules.</summary>
    private static string? ClaimOf(FollowUpFacts f)
    {
        var c = f.Case;
        return f.Kind switch
        {
            LetterKind.Compensation when c.Incident == IncidentType.Cancellation =>
                $"zwrot należności za bilet w wysokości {c.AmountText} na podstawie art. 18 rozporządzenia (UE) 2021/782",
            LetterKind.Compensation =>
                $"wypłatę odszkodowania w wysokości {c.AmountText} na podstawie art. 19 rozporządzenia (UE) 2021/782",
            LetterKind.DocumentedCosts => $"zwrot udokumentowanych kosztów w kwocie {c.ExpenseAmountText}",
            _ => null,
        };
    }

    /// <summary>", bilet nr IC-1" for the "Dotyczy" line, or nothing when the number is unknown.</summary>
    private static string TicketPart(ComplaintFacts c) => string.IsNullOrWhiteSpace(c.TicketNumber) ? "" : $", bilet{c.TicketRef}";

    public static (string Subject, string Body) BuildReminder(FollowUpFacts f)
    {
        var c = f.Case;
        var document = DocumentOf(f);
        var subject = $"Ponaglenie – {document.Nominative} z dnia {f.FiledText}";

        var claim = ClaimOf(f) is { } asked
            ? $"Wniosłem(-am) o {asked}."
            : "W reklamacji wniosłem(-am) o jej rozpatrzenie na zasadach określonych w Państwa regulaminie przewozu.";
        // Only a deadline that really passed is stated; before it the passenger only says no answer has come yet.
        var noReply = f.PastDeadline
            ? "Termin na udzielenie odpowiedzi upłynął, a do dnia dzisiejszego nie otrzymałem(-am) odpowiedzi."
            : "Informuję, że do dnia dzisiejszego nie otrzymałem(-am) odpowiedzi.";

        // A shorter deadline is asked for only once the carrier's own time is up.
        var replyBy = f.PastDeadline
            ? "w terminie 14 dni od dnia otrzymania niniejszego ponaglenia"
            : "w terminie przewidzianym przepisami";

        var body = $"""
            {ComplaintTemplate.FullName}
            {ComplaintTemplate.Address}

            {c.Carrier}

            Dotyczy: {document.Nominative} z dnia {f.FiledText}{TicketPart(c)}, przejazd relacją {c.FromText} – {c.ToText} w dniu {c.DateText}

            Szanowni Państwo,

            w dniu {f.FiledText} skierowałem(-am) do Państwa {document.Accusative} w sprawie przejazdu w dniu {c.DateText} pociągiem{c.CategoryRef} relacją {c.FromText} – {c.ToText} na podstawie biletu{c.TicketRef}, za który zapłaciłem(-am) {c.PriceText}. {c.IncidentText}

            {claim} {noReply}

            Uprzejmie proszę o rozpatrzenie {document.Genitive} i udzielenie pisemnej odpowiedzi {replyBy}.

            {ComplaintTemplate.Payout}

            Z poważaniem
            {ComplaintTemplate.FullName}
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) BuildUtkComplaint(FollowUpFacts f)
    {
        var c = f.Case;
        var cancelled = c.Incident == IncidentType.Cancellation;
        var document = DocumentOf(f);
        var subject = $"Skarga na przewoźnika {c.Carrier} – {(cancelled ? "odwołanie" : "opóźnienie")} pociągu z dnia {c.DateText}";

        var trip = cancelled ? $"Skarga dotyczy przejazdu w dniu {c.DateText}" : $"W dniu {c.DateText} odbyłem(-am) podróż";
        var filed = ClaimOf(f) is { } asked
            ? $"W dniu {f.FiledText} złożyłem(-am) u przewoźnika wniosek o {asked}."
            : $"W dniu {f.FiledText} złożyłem(-am) u przewoźnika reklamację z prośbą o jej rozpatrzenie na zasadach regulaminu przewozu.";
        var answer = f.Rejected
            ? $"Przewoźnik nie uwzględnił {document.Genitive}."
            : "Termin na udzielenie odpowiedzi upłynął, a do dnia dzisiejszego nie otrzymałem(-am) odpowiedzi przewoźnika.";
        // EU articles only where the first letter relied on them (money due under 2021/782), as in ComplaintTemplate.
        var request = f.Kind == LetterKind.Compensation
            ? $"Wnoszę o zbadanie, czy przewoźnik przestrzegał praw pasażerów, w szczególności {ComplaintTemplate.Article(c.Incident)} {Regulation}" +
              (cancelled ? " (zwrot należności za bilet w przypadku odwołania pociągu)" : " (odszkodowanie za opóźnienie pociągu)") +
              ", oraz o podjęcie działań w ramach kompetencji Prezesa Urzędu Transportu Kolejowego."
            : "Wnoszę o zbadanie, czy przewoźnik przestrzegał praw pasażerów wynikających z przepisów prawa przewozowego " +
              "oraz regulaminu przewozu, oraz o podjęcie działań w ramach kompetencji Prezesa Urzędu Transportu Kolejowego.";
        var original = f.FiledDate is null ? "[kopia pisma do przewoźnika]" : $"[kopia pisma do przewoźnika z dnia {f.FiledText}]";
        var reply = f.Rejected ? $"\n3. {ReplyAttachment}" : "";

        var body = $"""
            {ComplaintTemplate.FullName}
            {ComplaintTemplate.Address}

            {ComplaintChannels.UtkName}
            {ComplaintChannels.UtkPostalAddress.Replace(", ", "\n")}

            Dotyczy: skarga na przewoźnika {c.Carrier}{TicketPart(c)}, przejazd relacją {c.FromText} – {c.ToText} w dniu {c.DateText}

            Szanowni Państwo,

            zwracam się ze skargą na postępowanie przewoźnika {c.Carrier} w sprawie mojego przejazdu w dniu {c.DateText}.

            {trip} pociągiem{c.CategoryRef} relacją {c.FromText} – {c.ToText} na podstawie biletu{c.TicketRef}, za który zapłaciłem(-am) {c.PriceText}. {c.IncidentText}

            {filed} {answer}

            {request}

            Załączniki:
            1. {original}
            2. [kopia biletu]{reply}

            Z poważaniem
            {ComplaintTemplate.FullName}
            """;
        return (subject, body);
    }
}
