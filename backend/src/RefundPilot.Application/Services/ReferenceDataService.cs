using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

/// <summary>Exposes the domain carrier catalogue (the single source) as a DTO.</summary>
public sealed class ReferenceDataService
{
    public ReferenceDataDto Get() => new(
        CarrierCatalog.Carriers
            .Select(c => new CarrierOption(c.Name, c.Coverage, c.Categories.Select(x => x.Code).ToList()))
            .ToList(),
        CarrierCatalog.Categories.Select(c => new CategoryOption(c.Code, c.Label)).ToList());
}
