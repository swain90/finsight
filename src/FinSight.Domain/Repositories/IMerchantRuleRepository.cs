using FinSight.Domain.Entities;

namespace FinSight.Domain.Repositories;

public interface IMerchantRuleRepository
{
    Task<IReadOnlyList<MerchantRule>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(MerchantRule rule, CancellationToken cancellationToken = default);
}
