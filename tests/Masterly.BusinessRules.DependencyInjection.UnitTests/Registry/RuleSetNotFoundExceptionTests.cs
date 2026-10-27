namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class RuleSetNotFoundExceptionTests
{
    private const string Missing = "checkuot";

    [Fact]
    public void Constructor_NullSetName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleSetNotFoundException(null!, Array.Empty<string>()));
    }

    [Fact]
    public void Constructor_NullAvailableSets_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleSetNotFoundException(Missing, null!));
    }

    [Fact]
    public void Constructor_ExposesTheSetNameAndTheSortedAvailableSets()
    {
        RuleSetNotFoundException exception = new RuleSetNotFoundException(Missing, new[] { "refund", "checkout" });

        Assert.Equal(Missing, exception.SetName);
        Assert.Equal(new[] { "checkout", "refund" }, exception.AvailableSets);
    }

    [Fact]
    public void Message_WithAvailableSets_NamesTheMissingSetAndListsTheOthers()
    {
        RuleSetNotFoundException exception = new RuleSetNotFoundException(Missing, new[] { "refund", "checkout" });

        Assert.Equal("Rule registry has no set 'checkuot'. Available sets: checkout, refund", exception.Message);
    }

    [Fact]
    public void Message_WithoutAvailableSets_SaysTheRegistryIsEmpty()
    {
        RuleSetNotFoundException exception = new RuleSetNotFoundException(Missing, Array.Empty<string>());

        Assert.Equal("Rule registry has no set 'checkuot'. The registry is empty.", exception.Message);
    }

    [Fact]
    public void Exception_IsAKeyNotFoundException()
    {
        RuleSetNotFoundException exception = new RuleSetNotFoundException(Missing, Array.Empty<string>());

        Assert.IsAssignableFrom<KeyNotFoundException>(exception);
    }
}
