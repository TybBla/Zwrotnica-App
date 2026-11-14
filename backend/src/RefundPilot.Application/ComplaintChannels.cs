namespace RefundPilot.Application;

/// <summary>Where a letter goes: an official page and, when it was verified there, how the office accepts letters.</summary>
public sealed record ComplaintChannel(string Url, string? FormUrl = null, string? Channels = null);

/// <summary>
/// Official complaint channels, keyed by the catalogue carrier name. Every address here was opened and checked on the
/// carrier's or office's own site on 2026-11-14; a carrier with no verified complaints page gets only its home page,
/// and an unlisted carrier gets nothing (the UI then shows only the generic advice). Never add an unverified link.
/// </summary>
public static class ComplaintChannels
{
    public const string UtkName = "Prezes Urzędu Transportu Kolejowego";
    public const string UtkPostalAddress = "Al. Jerozolimskie 134, 02-305 Warszawa";

    public static ComplaintChannel Utk { get; } = new(
        "https://utk.gov.pl/pl/urzad/kontakt",
        Channels: "E-mailem na adres pasazer@utk.gov.pl, przez ePUAP (skrytka /UTK/skrytka) albo pocztą na adres: " +
            $"Urząd Transportu Kolejowego, {UtkPostalAddress}.");

    private static readonly Dictionary<string, ComplaintChannel> Carriers = new()
    {
        ["PKP Intercity"] = new(
            "https://www.intercity.pl/pl/site/dla-pasazera/informacje/reklamacje-i-skargi/podstawowe-informacje-na-temat-rozpatrywania-skarg,-reklamacji-i-wnioskow.html",
            "https://www.intercity.pl/pl/dokumenty/formularze/Formularz-reklamacyjny-03-2026-pl.pdf",
            "Formularz reklamacyjny online na stronie intercity.pl, pocztą na adres: PKP Intercity S.A., " +
            "Al. Jerozolimskie 142 A, 02-305 Warszawa, albo osobiście w punkcie obsługi klienta PKP Intercity."),
        ["Polregio"] = new(
            "https://polregio.pl/pl/obsluga-klienta/reklamacje-skargi-wnioski/",
            Channels: "Formularz elektroniczny na stronie polregio.pl, w kasie biletowej sprzedającej bilety POLREGIO " +
            "albo pocztą na adres: POLREGIO S.A., ul. Kolejowa 1, 01-217 Warszawa (lub na adres właściwego oddziału)."),
        ["Koleje Mazowieckie"] = new("https://www.mazowieckie.com.pl/"),
        ["Koleje Śląskie"] = new(
            "https://www.kolejeslaskie.pl/obsluga-podroznych/pomocne-informacje/reklamacje-skargi-i-wnioski/",
            Channels: "Formularz online, w kasie biletowej Kolei Śląskich, osobiście albo pocztą na adres: " +
            "Koleje Śląskie sp. z o.o., ul. Raciborska 58, 40-074 Katowice."),
        ["Koleje Dolnośląskie"] = new(
            "https://kolejedolnoslaskie.pl/skargi-i-reklamacje/",
            Channels: "Formularz online, w kasie biletowej, osobiście w siedzibie spółki albo pocztą."),
        ["Koleje Małopolskie"] = new(
            "https://www.kolejemalopolskie.com.pl/pl/dla-pasazera/skargi-i-reklamacje",
            Channels: "Formularz online, e-mailem na adres pasazer@kolejemalopolskie.com.pl albo pocztą na adres spółki w Krakowie."),
        ["Koleje Wielkopolskie"] = new(
            "https://koleje-wielkopolskie.com.pl/dla-pasazera/skargi-i-reklamacje/",
            Channels: "E-mailem na adres reklamacje@koleje-wielkopolskie.com.pl, w kasie biletowej albo pocztą na adres: " +
            "Koleje Wielkopolskie sp. z o.o., ul. Składowa 5, 61-897 Poznań."),
        ["SKM Warszawa"] = new("https://www.skm.warszawa.pl/"),
        ["PKP SKM Trójmiasto"] = new(
            "https://www.skm.pkp.pl/dla-pasazera/skargi-reklamacje-zapytania",
            Channels: "W kasie biletowej SKM, w Dziale Obsługi Posprzedażowej, pocztą, e-mailem albo przez formularz online."),
        ["WKD"] = new("https://www.wkd.com.pl/"),
        ["Łódzka Kolej Aglomeracyjna"] = new("https://lka.lodzkie.pl/"),
        ["Arriva RP"] = new(
            "https://arriva.pl/859/skargi-i-reklamacje",
            Channels: "Pisemnie na adres biura Arriva RP w Toruniu, e-mailem albo przez formularz na stronie arriva.pl."),
    };

    /// <summary>The verified channel of a catalogue carrier, or null.</summary>
    public static ComplaintChannel? ForCarrier(string carrierName) => Carriers.GetValueOrDefault(carrierName);
}
