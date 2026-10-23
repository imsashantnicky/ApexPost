using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tests.Branches;

public class BranchTests
{
    [Fact]
    public void ConstructorWithValidValuesCreatesAnActiveBranch()
    {
        var branch = CreateBranch();

        Assert.Equal("BLR-001", branch.Code.Value);
        Assert.Equal("Bengaluru Central", branch.Name);
        Assert.Equal(3, branch.CounterCount);
        Assert.Equal(BranchStatus.Active, branch.Status);
        Assert.Equal("IN", branch.Address.CountryCode);
    }

    [Fact]
    public void BranchCodeNormalizesCaseAndWhitespace()
    {
        var code = BranchCode.From(" blr-001 ");

        Assert.Equal("BLR-001", code.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BRANCH-1")]
    [InlineData("B-001")]
    [InlineData("BLR-12A")]
    public void BranchCodeWithInvalidFormatThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => BranchCode.From(value));
    }

    [Fact]
    public void ConstructorWithBlankNameThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Branch(
            EntityId.New(),
            BranchCode.From("BLR-001"),
            "  ",
            CreateAddress(),
            3));
    }

    [Fact]
    public void ConstructorWithNoCountersThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Branch(
            EntityId.New(),
            BranchCode.From("BLR-001"),
            "Bengaluru Central",
            CreateAddress(),
            0));
    }

    [Fact]
    public void AddressWithInvalidCountryCodeThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new BranchAddress(
            "1 Market Road",
            null,
            "Bengaluru",
            "560001",
            "India"));
    }

    [Fact]
    public void SuspendThenActivateChangesBranchStatus()
    {
        var branch = CreateBranch();

        branch.Suspend();
        branch.Activate();

        Assert.Equal(BranchStatus.Active, branch.Status);
    }

    [Fact]
    public void ActivateAnActiveBranchThrowsDomainException()
    {
        var branch = CreateBranch();

        Assert.Throws<DomainException>(branch.Activate);
    }

    [Fact]
    public void ClosedBranchCannotBeChangedOrReactivated()
    {
        var branch = CreateBranch();
        branch.Close();

        Assert.Equal(BranchStatus.Closed, branch.Status);
        Assert.Throws<DomainException>(() => branch.SetCounterCount(4));
        Assert.Throws<DomainException>(branch.Activate);
    }

    private static Branch CreateBranch()
    {
        return new Branch(
            EntityId.New(),
            BranchCode.From("BLR-001"),
            "Bengaluru Central",
            CreateAddress(),
            3);
    }

    private static BranchAddress CreateAddress()
    {
        return new BranchAddress("1 Market Road", null, "Bengaluru", "560001", "IN");
    }
}
