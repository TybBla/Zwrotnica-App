using System.Globalization;
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

        // The amount is our calculation from the rules, not a figure the carrier confirmed.
        var amount = $"{ComplaintFacts.FormatMoney(c.CompensationAmount)} (według naszego wyliczenia)";

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
                      ? $"Mamy nadzieję, że przewoźnik zwrócił już należność za bilet w wysokości {amount}. "
                      : $"Mamy nadzieję, że przewoźnik wypłacił już odszkodowanie w wysokości {amount}. ") +
                  "Jeśli tak, prosimy oznaczyć sprawę w Zwrotnicy jako wypłaconą."
                : "Mamy nadzieję, że odpowiedź na reklamację już nadeszła i jest dla Państwa satysfakcjonująca. " +
                  "Jeśli tak, prosimy zaktualizować status sprawy w Zwrotnicy.",
            moneyDue
                ? cancelled
                    ? "Jeżeli przewoźnik nie zwrócił należności za bilet albo nie odpowiedział, warto:"
                    : "Jeżeli przewoźnik nie wypłacił kwoty, o którą Państwo wnioskowali, albo nie odpowiedział, warto:"
                : "Jeżeli odpowiedź na reklamację nie nadeszła albo jej Państwo nie akceptują, warto:",
        ];
        (string Text, string? Url)[] steps =
        [
            ($"wysłać przewoźnikowi ponaglenie – krótkie pismo z prośbą o odpowiedź, które powołuje się na {document} i datę wysłania,", null),
            ("złożyć skargę do Prezesa Urzędu Transportu Kolejowego:", UtkUrl),
            ("zwrócić się do Rzecznika Praw Pasażera Kolei z prośbą o mediację.", null),
        ];
        const string signature = "Z wyrazami szacunku\nZespół Zwrotnicy";
        const string footer = "Wiadomość ma charakter informacyjny i nie stanowi porady prawnej. To jednorazowe przypomnienie dotyczące tej sprawy. Przypomnienia e-mail można wyłączyć w profilu:";

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
        // Only the markup characters: Polish letters stay readable in the UTF-8 source (WebUtility would turn "ó" into &#243;).
        static string E(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&#39;");
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
