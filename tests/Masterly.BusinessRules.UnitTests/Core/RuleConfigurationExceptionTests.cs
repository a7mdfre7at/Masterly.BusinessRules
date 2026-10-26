namespace Masterly.BusinessRules.UnitTests;

public class RuleConfigurationExceptionTests
{
    private const string Code = "CREDIT.SCORE";
    private const string Message = "Rule CREDIT.SCORE is gated by flag 'x' but no feature flag provider was supplied";

    [Fact]
    public void Constructor_StoresRuleCodeAndMessage()
    {
        RuleConfigurationException exception = new RuleConfigurationException(Code, Message);

        Assert.Equal(Code, exception.RuleCode);
        Assert.Equal(Message, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_WithInnerException_StoresIt()
    {
        InvalidOperationException inner = new InvalidOperationException("inner");

        RuleConfigurationException exception = new RuleConfigurationException(Code, Message, inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Constructor_NullRuleCode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleConfigurationException(null!, Message));
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleConfigurationException(Code, null!));
    }

    [Fact]
    public void Exception_DerivesFromInvalidOperationException()
    {
        Assert.IsAssignableFrom<InvalidOperationException>(new RuleConfigurationException(Code, Message));
    }
}
