using RefundPilot.Application.Dtos;

namespace RefundPilot.Application.Abstractions;

public interface IComplaintPdfRenderer
{
    /// <summary>placeAndDate is the right-aligned header line, e.g. "Gdańsk, 14 listopada 2026 r.".</summary>
    byte[] Render(ComplaintDto letter, string placeAndDate);
}
