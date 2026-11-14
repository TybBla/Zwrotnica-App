using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RefundPilot.Application;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;

namespace RefundPilot.Infrastructure.Pdf;

/// <summary>
/// Renders the letter as an A4 PDF. QuestPDF's built-in Lato covers Polish diacritics
/// (the tests render every Polish letter in strict mode).
/// </summary>
public sealed class ComplaintPdfRenderer : IComplaintPdfRenderer
{
    static ComplaintPdfRenderer()
    {
        // QuestPDF Community license: free for individuals, non-profits, FOSS and organisations under USD 1M annual revenue.
        QuestPDF.Settings.License = LicenseType.Community;
        // Off: an emoji or rare script in a name/address must never turn into a 500; such glyphs just render as fallback.
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public byte[] Render(ComplaintDto letter, string placeAndDate)
    {
        var paragraphs = letter.Body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(2.2f, Unit.Centimetre);
            page.MarginHorizontal(2.5f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontFamily("Lato").FontSize(11).LineHeight(1.35f));

            page.Header().Column(header =>
            {
                header.Item().AlignRight().Text(placeAndDate).FontSize(10.5f);
                header.Item().PaddingTop(14).Text(letter.Subject).FontSize(15).Bold();
            });

            page.Content().PaddingTop(18).Column(column =>
            {
                column.Spacing(10);
                foreach (var paragraph in paragraphs)
                    column.Item().Text(paragraph.Trim('\n'));
            });
        })).GeneratePdf();
    }
}
