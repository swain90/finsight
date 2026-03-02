using FinSight.Domain.Enums;

namespace FinSight.Application.Common.Interfaces;

public interface ITransactionCategorizationService
{
    TransactionCategory Categorize(string merchantName, string description);
}
