namespace Masterly.BusinessRules.UnitTests;

public class ContextKeyTests
{
    private const string OrderKeyName = "order";
    private const string LimitKeyName = "limit";

    [Fact]
    public void Constructor_WithName_ExposesName()
    {
        ContextKey<string> key = new ContextKey<string>(OrderKeyName);

        Assert.Equal(OrderKeyName, key.Name);
    }

    [Fact]
    public void Constructor_NullName_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ContextKey<string>(null!));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhiteSpaceName_ThrowsArgumentException(string name)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new ContextKey<string>(name));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void ValueType_ReportsTheGenericArgument()
    {
        ContextKey<decimal> key = new ContextKey<decimal>(LimitKeyName);

        Assert.Equal(typeof(decimal), key.ValueType);
    }

    [Fact]
    public void Equals_SameName_ReturnsTrue()
    {
        ContextKey<int> first = new ContextKey<int>(OrderKeyName);
        ContextKey<int> second = new ContextKey<int>(OrderKeyName);

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void Equals_DifferentName_ReturnsFalse()
    {
        ContextKey<int> first = new ContextKey<int>(OrderKeyName);
        ContextKey<int> second = new ContextKey<int>(LimitKeyName);

        Assert.False(first.Equals(second));
        Assert.False(first == second);
        Assert.True(first != second);
    }

    [Fact]
    public void Equals_IsCaseSensitive()
    {
        ContextKey<int> lower = new ContextKey<int>("order");
        ContextKey<int> upper = new ContextKey<int>("ORDER");

        Assert.False(lower.Equals(upper));
    }

    [Fact]
    public void Equals_OtherObjectType_ReturnsFalse()
    {
        ContextKey<int> key = new ContextKey<int>(OrderKeyName);

        Assert.False(key.Equals(OrderKeyName));
        Assert.False(key.Equals(null));
    }

    [Fact]
    public void GetHashCode_SameName_ReturnsSameHash()
    {
        ContextKey<int> first = new ContextKey<int>(OrderKeyName);
        ContextKey<int> second = new ContextKey<int>(OrderKeyName);

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DefaultInstance_DoesNotThrow()
    {
        ContextKey<int> key = default;

        Assert.Equal(0, key.GetHashCode());
        Assert.Null(key.Name);
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        ContextKey<int> key = new ContextKey<int>(OrderKeyName);

        Assert.Equal(OrderKeyName, key.ToString());
    }

    [Fact]
    public void ToString_DefaultInstance_ReturnsEmptyString()
    {
        ContextKey<int> key = default;

        Assert.Equal(string.Empty, key.ToString());
    }

    [Fact]
    public void Key_UsableAsDictionaryKey()
    {
        Dictionary<ContextKey<int>, string> map = new Dictionary<ContextKey<int>, string>
        {
            [new ContextKey<int>(OrderKeyName)] = "first",
        };

        Assert.True(map.ContainsKey(new ContextKey<int>(OrderKeyName)));
        Assert.False(map.ContainsKey(new ContextKey<int>(LimitKeyName)));
    }
}
