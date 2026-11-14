using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public interface IReferenceDataService
{
    ReferenceDataDto Get();
}

/// <summary>Exposes the domain carrier catalogue (the single source) and the minimum payout as a DTO.</summary>
public sealed class ReferenceDataService(CompensationOptions compensation) : IReferenceDataService
{
    public ReferenceDataDto Get() => new(
        CarrierCatalog.Carriers
            .Select(c => new CarrierOption(c.Name, c.Coverage, c.Categories.Select(x => x.Code).ToList()))
            .ToList(),
        CarrierCatalog.Categories.Select(c => new CategoryOption(c.Code, c.Label)).ToList(),
        compensation.MinimumPayout);
}
