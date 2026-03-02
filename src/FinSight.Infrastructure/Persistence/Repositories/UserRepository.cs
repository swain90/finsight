using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);

    public async Task<User?> GetByRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        => await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token), cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await context.Users.AddAsync(user, cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        return Task.CompletedTask;
    }
}
