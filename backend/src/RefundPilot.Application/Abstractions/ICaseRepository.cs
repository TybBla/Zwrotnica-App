using RefundPilot.Domain;

namespace RefundPilot.Application.Abstractions;

public interface ICaseRepository
{
    Task<List<RefundCase>> ListAsync(string userId);
    Task<RefundCase?> GetAsync(int id, string userId);
    Task AddAsync(RefundCase refundCase);
    Task UpdateAsync(RefundCase refundCase);
    Task<bool> DeleteAsync(int id, string userId);
}
