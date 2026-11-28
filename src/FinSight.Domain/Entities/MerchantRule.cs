using FinSight.Domain.Common;
using FinSight.Domain.Enums;

namespace FinSight.Domain.Entities;

public sealed class MerchantRule : BaseEntity
{
    public Guid UserId { get; private set; }
    public string DescriptionKey { get; private set; } = string.Empty;  // max 200
    public TransactionCategory Category { get; private set; }

    private MerchantRule() { }  // EF Core

    public static MerchantRule Create(Guid userId, string descriptionKey, TransactionCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionKey);
        var key = descriptionKey.Trim();
        return new MerchantRule
        {
            UserId = userId,
            DescriptionKey = key.Length > 200 ? key[..200] : key,
            Category = category
        };
    }

    public void Update(TransactionCategory category)
    {
        Category = category;
        SetUpdatedAt();
    }
}
