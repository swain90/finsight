using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.FileName).IsRequired().HasMaxLength(255);
        builder.Property(b => b.ReconciliationWarning).HasMaxLength(2000);
        builder.Property(b => b.StatementClosingBalance).HasPrecision(18, 2);
        builder.Property(b => b.Institution).HasConversion<string>().HasMaxLength(30);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(30);

        // Rows is a read-only view over the _rows backing field; EF fixes up ImportRow.ImportBatchId.
        builder.HasMany(b => b.Rows)
            .WithOne()
            .HasForeignKey(r => r.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(b => b.Rows).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(b => b.DomainEvents);
        builder.HasIndex(b => new { b.UserId, b.CreatedAt });
    }
}
