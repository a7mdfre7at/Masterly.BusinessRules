namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleContextExceptionTests
{
    private const string RuleCode = "LIMIT.EXCEEDED";
    private const string KeyName = "limit";

    // ---------------------------------------------------------------------
    // ContextKeyNotFoundException
    // ---------------------------------------------------------------------

    [Fact]
    public void ContextKeyNotFoundException_Constructor_SortsAvailableKeysOrdinally()
    {
        ContextKeyNotFoundException exception = new ContextKeyNotFoundException(KeyName, new[] { "b", "C", "a" });

        Assert.Equal(new[] { "C", "a", "b" }, exception.AvailableKeys);
        Assert.Equal("Context has no key 'limit'. Available keys: C, a, b", exception.Message);
    }

    [Fact]
    public void ContextKeyNotFoundException_Constructor_SnapshotsTheKeys()
    {
        List<string> keys = new List<string> { "a" };
        ContextKeyNotFoundException exception = new ContextKeyNotFoundException(KeyName, keys);

        keys.Add("b");

        Assert.Equal(new[] { "a" }, exception.AvailableKeys);
    }

    [Fact]
    public void ContextKeyNotFoundException_NullKey_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ContextKeyNotFoundException(null!, Array.Empty<string>()));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void ContextKeyNotFoundException_NullAvailableKeys_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ContextKeyNotFoundException(KeyName, null!));

        Assert.Equal("availableKeys", exception.ParamName);
    }

    [Fact]
    public void ContextKeyNotFoundException_DerivesFromKeyNotFoundException()
    {
        ContextKeyNotFoundException exception = new ContextKeyNotFoundException(KeyName, Array.Empty<string>());

        Assert.IsAssignableFrom<KeyNotFoundException>(exception);
    }

    // ---------------------------------------------------------------------
    // ContextValueTypeMismatchException
    // ---------------------------------------------------------------------

    [Fact]
    public void ContextValueTypeMismatchException_Constructor_ExposesTypesAndMessage()
    {
        ContextValueTypeMismatchException exception = new ContextValueTypeMismatchException(KeyName, typeof(int), typeof(decimal));

        Assert.Equal(KeyName, exception.Key);
        Assert.Equal(typeof(int), exception.ActualType);
        Assert.Equal(typeof(decimal), exception.RequestedType);
        Assert.Equal("Key 'limit' holds Int32, requested Decimal", exception.Message);
    }

    [Fact]
    public void ContextValueTypeMismatchException_NullActualType_SaysNull()
    {
        ContextValueTypeMismatchException exception = new ContextValueTypeMismatchException(KeyName, null, typeof(decimal));

        Assert.Null(exception.ActualType);
        Assert.Equal("Key 'limit' holds null, requested Decimal", exception.Message);
    }

    [Fact]
    public void ContextValueTypeMismatchException_NullKey_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ContextValueTypeMismatchException(null!, typeof(int), typeof(decimal)));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void ContextValueTypeMismatchException_NullRequestedType_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ContextValueTypeMismatchException(KeyName, typeof(int), null!));

        Assert.Equal("requestedType", exception.ParamName);
    }

    [Fact]
    public void ContextValueTypeMismatchException_DerivesFromInvalidCastException()
    {
        ContextValueTypeMismatchException exception = new ContextValueTypeMismatchException(KeyName, typeof(int), typeof(decimal));

        Assert.IsAssignableFrom<InvalidCastException>(exception);
    }

    // ---------------------------------------------------------------------
    // BusinessRuleContextException (checker wrapper)
    // ---------------------------------------------------------------------

    [Fact]
    public void BusinessRuleContextException_Constructor_PrefixesRuleCodeAndKeepsInner()
    {
        ContextKeyNotFoundException inner = new ContextKeyNotFoundException(KeyName, new[] { "order" });

        BusinessRuleContextException exception = new BusinessRuleContextException(RuleCode, KeyName, inner);

        Assert.Equal(RuleCode, exception.RuleCode);
        Assert.Equal(KeyName, exception.Key);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal($"Rule {RuleCode} failed to read context: {inner.Message}", exception.Message);
    }

    [Fact]
    public void BusinessRuleContextException_NullRuleCode_ThrowsArgumentNullException()
    {
        ContextKeyNotFoundException inner = new ContextKeyNotFoundException(KeyName, Array.Empty<string>());

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new BusinessRuleContextException(null!, KeyName, inner));

        Assert.Equal("ruleCode", exception.ParamName);
    }

    [Fact]
    public void BusinessRuleContextException_NullKey_ThrowsArgumentNullException()
    {
        ContextKeyNotFoundException inner = new ContextKeyNotFoundException(KeyName, Array.Empty<string>());

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new BusinessRuleContextException(RuleCode, null!, inner));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void BusinessRuleContextException_NullInner_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new BusinessRuleContextException(RuleCode, KeyName, null!));

        Assert.Equal("innerException", exception.ParamName);
    }
}
