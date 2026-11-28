using FinSight.Domain.Entities;
using FinSight.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Entities;

public class UserFiscalYearTests
{
    private static User NewUser() => User.Create("a@b.com", "hash", "A", "B");

    [Fact]
    public void SetCorporateFiscalYearEnd_Feb29_Throws()
    {
        var act = () => NewUser().SetCorporateFiscalYearEnd(2, 29);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetCorporateFiscalYearEnd_Jun30_Stores()
    {
        var u = NewUser();
        u.SetCorporateFiscalYearEnd(6, 30);
        (u.CorporateFiscalYearEndMonth, u.CorporateFiscalYearEndDay).Should().Be((6, 30));
    }

    [Fact]
    public void NewUser_FiscalYearEnd_DefaultsToDec31()
    {
        var u = NewUser();
        (u.CorporateFiscalYearEndMonth, u.CorporateFiscalYearEndDay).Should().Be((12, 31));
    }
}
