using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Entities;

public class AccountOwnershipTests
{
    [Fact]
    public void Create_Defaults_PersonalOtherNoRef()
    {
        var a = Account.Create(Guid.NewGuid(), "Visa", AccountType.CreditCard);
        a.Owner.Should().Be(AccountOwner.Personal);
        a.Institution.Should().Be(Institution.Other);
        a.ExternalAccountRef.Should().BeNull();
    }

    [Fact]
    public void Create_CorporateRbc_StoresOwnerInstitutionRef()
    {
        var a = Account.Create(Guid.NewGuid(), "RBC Business", AccountType.Checking, "CAD", "RBC",
            Institution.Rbc, AccountOwner.Corporation, "1234");
        (a.Owner, a.Institution, a.ExternalAccountRef).Should().Be((AccountOwner.Corporation, Institution.Rbc, "1234"));
    }

    [Fact]
    public void UpdateDetails_ChangesFields()
    {
        var a = Account.Create(Guid.NewGuid(), "Old", AccountType.Checking);

        a.UpdateDetails("  New Name  ", AccountType.Tfsa, Institution.Wealthsimple, AccountOwner.Corporation, "9876");

        a.Name.Should().Be("New Name");
        a.Type.Should().Be(AccountType.Tfsa);
        a.Institution.Should().Be(Institution.Wealthsimple);
        a.Owner.Should().Be(AccountOwner.Corporation);
        a.ExternalAccountRef.Should().Be("9876");
    }

    [Theory]
    [InlineData(AccountType.CreditCard, true)]
    [InlineData(AccountType.LineOfCredit, true)]
    [InlineData(AccountType.Mortgage, true)]
    [InlineData(AccountType.Loan, true)]
    [InlineData(AccountType.Savings, false)]
    [InlineData(AccountType.Tfsa, false)]
    public void IsLiability_MatchesSpec(AccountType t, bool expected) => t.IsLiability().Should().Be(expected);

    [Theory]
    [InlineData(AccountType.Tfsa, true)]
    [InlineData(AccountType.Rrsp, true)]
    [InlineData(AccountType.Fhsa, true)]
    [InlineData(AccountType.Crypto, false)]
    [InlineData(AccountType.Investment, false)]
    public void IsRegistered_MatchesSpec(AccountType t, bool expected) => t.IsRegistered().Should().Be(expected);

    [Theory]
    [InlineData(AccountType.Crypto, true)]
    [InlineData(AccountType.Retirement, true)]
    [InlineData(AccountType.Checking, false)]
    public void IsInvestment_MatchesSpec(AccountType t, bool expected) => t.IsInvestment().Should().Be(expected);
}
