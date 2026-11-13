using RefundPilot.Domain;

namespace RefundPilot.Application.Abstractions;

public interface ICaseRepository
{
    Task<List<RefundCase>> ListAsync();
    Task<RefundCase?> GetAsync(int id);
    Task AddAsync(RefundCase refundCase);
    Task UpdateAsync(RefundCase refundCase);
    Task<bool> DeleteAsync(int id);
}
