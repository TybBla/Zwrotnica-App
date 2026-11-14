using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application;

/// <summary>Renders a posted (possibly edited) letter as PDF with the "place, date" header. No AI call, no writes.</summary>
internal static class LetterPdf
{
    public static async Task<byte[]> RenderAsync(IUserProfileStore profiles, IComplaintPdfRenderer pdf, TimeProvider clock,
        string userId, RefundCase refundCase, ComplaintPdfRequest request)
    {
        var address = (await profiles.GetAsync(userId))?.Address;
        var place = LetterHeader.Place(address, refundCase.ToStation, refundCase.FromStation);
        var placeAndDate = LetterHeader.PlaceAndDate(place, WarsawClock.Today(clock));
        return pdf.Render(new ComplaintDto(request.Subject, request.Body, UsedAi: false, []), placeAndDate);
    }
}
