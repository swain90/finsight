using FinSight.Domain.Entities;

namespace FinSight.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the user who owns a given refresh token, eagerly loading all tokens.
    /// Required for rotation and reuse detection.
    /// </summary>
    Task<User?> GetByRefreshTokenAsync(string token, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
