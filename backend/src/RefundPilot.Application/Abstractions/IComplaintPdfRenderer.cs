using RefundPilot.Application.Dtos;

namespace RefundPilot.Application.Abstractions;

public interface IComplaintPdfRenderer
{
    byte[] Render(ComplaintDto letter);
}
