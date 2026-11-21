using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Reports.Queries.GetPersonalIncome;

public sealed record PersonalIncomeDto(int Year, decimal PayFromCorporation, decimal OtherTaxableIncome, decimal TaxShelteredInvestmentIncome);

/// <summary>Calendar-year personal income; Year null means the current year.</summary>
public sealed record GetPersonalIncomeQuery(int? Year) : IRequest<Result<PersonalIncomeDto>>;
