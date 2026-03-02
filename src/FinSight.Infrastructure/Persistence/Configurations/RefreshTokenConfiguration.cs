using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Token).IsRequired().HasMaxLength(256);
        builder.Property(rt => rt.CreatedByIp).HasMaxLength(50);
        builder.Property(rt => rt.RevokedByIp).HasMaxLength(50);
        builder.Property(rt => rt.ReplacedByToken).HasMaxLength(256);

        builder.HasIndex(rt => rt.Token).IsUnique();
        builder.HasIndex(rt => new { rt.UserId, rt.IsRevoked });
    }
}
