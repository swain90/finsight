using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class MerchantRuleRepository(ApplicationDbContext context) : IMerchantRuleRepository
{
    public async Task<IReadOnlyList<MerchantRule>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.MerchantRules
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.DescriptionKey)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MerchantRule rule, CancellationToken cancellationToken = default)
        => await context.MerchantRules.AddAsync(rule, cancellationToken);
}
