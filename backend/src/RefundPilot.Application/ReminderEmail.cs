using System.Globalization;
using System.Net;
using System.Text;
using RefundPilot.Domain;

namespace RefundPilot.Application;

public sealed record ReminderMessage(string Subject, string Text, string Html);

/// <summary>
/// The one follow-up e-mail after the carrier's month has passed. Money due (the same rule as the letter) gets
/// "has it been paid?"; a complaint under the carrier's own rules only asks about the answer and claims no amount.
/// </summary>
public static class ReminderEmail
{
    private const string UtkUrl = "https://utk.gov.pl";

    public static ReminderMessage Build(RefundCase c, string publicBaseUrl)
    {
        var baseUrl = publicBaseUrl.TrimEnd('/');
        var caseUrl = $"{baseUrl}/sprawy/{c.Id}";
        var profileUrl = $"{baseUrl}/konto";
        var moneyDue = c.Verdict == Verdict.Eligible && c.CompensationAmount > 0;
        var cancelled = c.IncidentType == IncidentType.Cancellation;

        var route = !string.IsNullOrWhiteSpace(c.FromStation) && !string.IsNullOrWhiteSpace(c.ToStation)
            ? $" na trasie {c.FromStation} – {c.ToStation}"
            : "";
        var sent = c.FiledAt is { } filedAt ? $"wysłano {Date(filedAt)}" : "wysłano";
        var due = c.ResponseDueDate is { } dueDate ? $"upłynął {Date(dueDate)}" : "już upłynął";
        var document = moneyDue ? "pismo" : "reklamację";

        var subject = moneyDue
            ? cancelled ? "Czy przewoźnik zwrócił już należność za bilet? – Zwrotnica" : "Czy otrzymali Państwo już odszkodowanie? – Zwrotnica"
            : "Czy przewoźnik odpowiedział na reklamację? – Zwrotnica";

        string[] paragraphs =
        [
            "Dzień dobry,",
            $"dziękujemy, że skorzystali Państwo ze Zwrotnicy. Według naszych zapisów {document} do przewoźnika {c.Carrier} " +
            $"w sprawie przejazdu{route} z dnia {c.TravelDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} {sent}, " +
            $"a termin na odpowiedź przewoźnika {due}.",
            moneyDue
                ? (cancelled
                      ? $"Mamy nadzieję, że przewoźnik zwrócił już należność za bilet ({ComplaintFacts.FormatMoney(c.CompensationAmount)}). "
                      : $"Mamy nadzieję, że przewoźnik wypłacił już odszkodowanie w wysokości {ComplaintFacts.FormatMoney(c.CompensationAmount)}. ") +
                  "Jeśli tak, prosimy oznaczyć sprawę w Zwrotnicy jako wypłaconą."
                : "Mamy nadzieję, że odpowiedź na reklamację już nadeszła i jest dla Państwa satysfakcjonująca. " +
                  "Jeśli tak, prosimy zaktualizować status sprawy w Zwrotnicy.",
            moneyDue
                ? "Jeżeli przewoźnik nie wypłacił należnej kwoty albo nie odpowiedział, warto:"
                : "Jeżeli odpowiedź na reklamację nie nadeszła albo jej Państwo nie akceptują, warto:",
        ];
        (string Text, string? Url)[] steps =
        [
            ($"wysłać przewoźnikowi ponaglenie – krótkie pismo z prośbą o odpowiedź, które powołuje się na {document} i datę wysłania,", null),
            ("złożyć skargę do Prezesa Urzędu Transportu Kolejowego:", UtkUrl),
            ("zwrócić się do Rzecznika Praw Pasażera Kolei z prośbą o mediację.", null),
        ];
        const string signature = "Z wyrazami szacunku\nZespół Zwrotnicy";
        const string footer = "To jednorazowe przypomnienie dotyczące tej sprawy. Przypomnienia e-mail można wyłączyć w profilu:";

        return new(subject,
            Text(paragraphs, steps, caseUrl, signature, footer, profileUrl),
            Html(paragraphs, steps, caseUrl, signature, footer, profileUrl));
    }

    private static string Date(DateTime utc) =>
        WarsawClock.DateOf(utc).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string Text(string[] paragraphs, (string Text, string? Url)[] steps, string caseUrl,
        string signature, string footer, string profileUrl)
    {
        var text = new StringBuilder();
        foreach (var paragraph in paragraphs)
            text.Append(paragraph).Append("\n\n");
        text.Length -= 1;
        for (var i = 0; i < steps.Length; i++)
            text.Append($"{i + 1}. {steps[i].Text}{(steps[i].Url is { } url ? " " + url : "")}\n");
        text.Append($"\nSprawa w Zwrotnicy: {caseUrl}\n\n{signature}\n\n-- \n{footer} {profileUrl}\n");
        return text.ToString();
    }

    private static string Html(string[] paragraphs, (string Text, string? Url)[] steps, string caseUrl,
        string signature, string footer, string profileUrl)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        static string Link(string url, string label) => $"<a href=\"{E(url)}\">{E(label)}</a>";

        var html = new StringBuilder("<!doctype html><html lang=\"pl\"><head><meta charset=\"utf-8\"></head>")
            .Append("<body style=\"font-family:Arial,sans-serif;font-size:15px;line-height:1.5;color:#1d2433\">");
        foreach (var paragraph in paragraphs)
            html.Append($"<p>{E(paragraph)}</p>");
        html.Append("<ol>");
        foreach (var (text, url) in steps)
            html.Append($"<li>{E(text)}{(url is null ? "" : " " + Link(url, url))}</li>");
        html.Append("</ol>")
            .Append($"<p>{Link(caseUrl, "Otwórz sprawę w Zwrotnicy")}<br>{E(caseUrl)}</p>")
            .Append($"<p>{E(signature).Replace("\n", "<br>")}</p>")
            .Append($"<p style=\"font-size:13px;color:#566277\">{E(footer)} {Link(profileUrl, profileUrl)}</p>")
            .Append("</body></html>");
        return html.ToString();
    }
}
