using System.Globalization;
using System.Text.Json;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Validation;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public interface IExtractionService
{
    Task<ExtractionDto> ExtractAsync(IReadOnlyList<AiFile> files, CancellationToken ct);
}

/// <summary>Reads ticket data with the AI client and sanity-checks it. Only ticket files are sent - no personal data.</summary>
public sealed class ExtractionService(IAiClient ai, TimeProvider clock) : IExtractionService
{
    internal const string Prompt = """
        You are reading a Polish rail ticket (images and/or PDF, possibly several pages of one ticket).
        Extract only what is clearly visible. Return null for anything that is not clearly visible; never guess.
        Fields:
        - carrier: railway operator printed on the ticket (e.g. "PKP Intercity", "Polregio").
        - trainCategory: one of EIP, EIC, IC, TLK, or the category exactly as printed.
        - fromStation / toStation: departure and destination station names.
        - ticketNumber: the ticket or reservation number.
        - ticketPrice: the total amount actually paid, in PLN, as a number (no currency symbol).
        - travelDate: date of travel in ISO format yyyy-MM-dd.
        Write station and carrier names with Polish diacritics when they clearly follow from the ticket or are the obvious official name (e.g. "Kraków Główny"); do not guess names that are not visible.
        Do not extract the passenger name or any other personal data.
        """;

    internal const string Schema = """
        {
          "type": "object",
          "properties": {
            "carrier": { "type": ["string", "null"] },
            "trainCategory": { "type": ["string", "null"] },
            "fromStation": { "type": ["string", "null"] },
            "toStation": { "type": ["string", "null"] },
            "ticketNumber": { "type": ["string", "null"] },
            "ticketPrice": { "type": ["number", "null"] },
            "travelDate": { "type": ["string", "null"] }
          },
          "required": ["carrier", "trainCategory", "fromStation", "toStation", "ticketNumber", "ticketPrice", "travelDate"]
        }
        """;

    private const string Malformed = "Nie udało się odczytać odpowiedzi AI. Spróbuj ponownie lub wpisz dane ręcznie.";

    public async Task<ExtractionDto> ExtractAsync(IReadOnlyList<AiFile> files, CancellationToken ct)
    {
        var raw = await ai.GenerateJsonAsync(Prompt, files, Schema, ct);

        using var document = Parse(raw);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new AiUnavailableException(Malformed);

        var rawCarrier = Text(root, "carrier", 200, TextField.Carrier);
        var carrier = CarrierCatalog.FindCarrier(rawCarrier)?.Name ?? rawCarrier;
        var rawCategory = Text(root, "trainCategory", 50, TextField.TrainCategory);
        var category = CarrierCatalog.FindCategory(rawCategory)?.Code ?? rawCategory;
        var from = Text(root, "fromStation", 200, TextField.Station);
        var to = Text(root, "toStation", 200, TextField.Station);
        var number = Text(root, "ticketNumber", 100, TextField.TicketNumber);
        var price = Price(root);
        var date = Date(root);

        var missing = new List<string>();
        if (carrier is null) missing.Add("carrier");
        if (category is null) missing.Add("trainCategory");
        if (from is null) missing.Add("fromStation");
        if (to is null) missing.Add("toStation");
        if (number is null) missing.Add("ticketNumber");
        if (price is null) missing.Add("ticketPrice");
        if (date is null) missing.Add("travelDate");

        return new(carrier, category, from, to, number, price, date, missing, UsedAi: true);
    }

    private static JsonDocument Parse(string raw)
    {
        try
        {
            return JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            throw new AiUnavailableException(Malformed);
        }
    }

    /// <summary>Trimmed string capped at <paramref name="max"/> (the case request limit); when a field is given, characters outside its allowlist are dropped.</summary>
    private static string? Text(JsonElement root, string name, int max, TextField? field = null)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            return null;
        var text = value.GetString()!.Trim();
        if (field is { } f)
            text = TextRules.Sanitize(f, text);
        return text is { Length: var length } && length > max ? text[..max].TrimEnd() : text;
    }

    private static decimal? Price(JsonElement root)
    {
        if (!root.TryGetProperty("ticketPrice", out var value))
            return null;
        decimal price;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDecimal(out price))
                return null;
        }
        else if (value.ValueKind == JsonValueKind.String
                 && decimal.TryParse(value.GetString()?.Replace(',', '.'), NumberStyles.Number,
                     CultureInfo.InvariantCulture, out var parsed))
            price = parsed;
        else
            return null;
        return price is > 0 and <= 2000 ? price : null;
    }

    private DateOnly? Date(JsonElement root)
    {
        if (Text(root, "travelDate", 100) is not { } text
            || !DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;
        var today = WarsawClock.Today(clock);
        return date > today || date < today.AddYears(-2) ? null : date;
    }
}
