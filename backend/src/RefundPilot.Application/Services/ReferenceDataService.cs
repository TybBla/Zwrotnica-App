using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public interface IReferenceDataService
{
    ReferenceDataDto Get();
}

/// <summary>
/// Exposes the domain carrier catalogue (the single source), the verified complaint channels and the minimum payout as a DTO.
/// </summary>
public sealed class ReferenceDataService(CompensationOptions compensation) : IReferenceDataService
{
    public ReferenceDataDto Get() => new(
        CarrierCatalog.Carriers.Select(ToOption).ToList(),
        CarrierCatalog.Categories.Select(c => new CategoryOption(c.Code, c.Label)).ToList(),
        compensation.MinimumPayout,
        new ComplaintOfficeDto(ComplaintChannels.UtkName, ComplaintChannels.Utk.Url, ComplaintChannels.Utk.Channels!));

    private static CarrierOption ToOption(CarrierInfo c)
    {
        var channel = ComplaintChannels.ForCarrier(c.Name);
        return new(c.Name, c.Coverage, c.Categories.Select(x => x.Code).ToList(),
            channel?.Url, channel?.FormUrl, channel?.Channels);
    }
}
