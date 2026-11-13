using System.Text;
using Microsoft.Extensions.Time.Testing;
using RefundPilot.Application.Dtos;
using RefundPilot.Infrastructure.Pdf;

namespace RefundPilot.Tests.Application;

// QuestPDF settings are global: both tests live in one class so xUnit runs them sequentially.
public sealed class ComplaintPdfTests
{
    private static readonly ComplaintPdfRenderer Renderer =
        new(new FakeTimeProvider(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Render_ProducesPdfWithPolishDiacritics()
    {
        var letter = new ComplaintDto("Reklamacja ąęśżźćńół ĄĘŚŻŹĆŃÓŁ",
            "Anna Kowalska\nul. Łąkowa 5, Łódź\n\nSzanowni Państwo, żądam zwrotu: ąęśżźćńół ĄĘŚŻŹĆŃÓŁ.\n\nZ poważaniem", true, []);

        // Strict mode: any glyph Lato cannot draw would throw, so this proves full Polish coverage.
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = true;
        try
        {
            var bytes = Renderer.Render(letter);

            Assert.True(bytes.Length > 1000);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
        }
    }

    [Fact]
    public void Render_UnsupportedCharacters_DoNotBreakThePdf()
    {
        var letter = new ComplaintDto("Reklamacja 🚆", "Jan 😀 Kowalski\nul. 東京 5\n\nTreść 🎉 z emoji i pismem 漢字.", false, []);

        var bytes = Renderer.Render(letter);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Render_LongBodySpanningPages_DoesNotThrow()
    {
        var body = string.Join("\n\n", Enumerable.Repeat(string.Join(' ', Enumerable.Repeat("Zażółć gęślą jaźń.", 40)), 30));

        var bytes = Renderer.Render(new ComplaintDto("Temat", body, false, []));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
