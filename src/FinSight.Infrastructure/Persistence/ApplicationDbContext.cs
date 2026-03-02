using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IMediator mediator)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(builder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch domain events before saving
        await DispatchDomainEventsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in events)
            await mediator.Publish(domainEvent, cancellationToken);
    }
}
