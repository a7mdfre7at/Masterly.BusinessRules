namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleMetadataTests
{
    private const string Code = "META.CODE";
    private const string Message = "Metadata message";

    // ---------------------------------------------------------------------
    // Hierarchy
    // ---------------------------------------------------------------------

    [Fact]
    public void IBusinessRule_ExtendsIBusinessRuleMetadata()
    {
        IBusinessRule rule = RuleTestHelper.CreateBrokenRule(Code, Message);

        IBusinessRuleMetadata metadata = rule;

        Assert.Equal(Code, metadata.Code);
        Assert.Equal(Message, metadata.Message);
    }

    [Fact]
    public void IAsyncBusinessRule_ExtendsIBusinessRuleMetadata()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync(_ => Task.FromResult(true)).Build();

        IBusinessRuleMetadata metadata = rule;

        Assert.Equal(Code, metadata.Code);
        Assert.Equal(Message, metadata.Message);
    }

    [Fact]
    public void TypedIBusinessRule_ExtendsIBusinessRuleMetadata()
    {
        IBusinessRule<int> rule = BusinessRuleBuilder<int>.Create(Code).WithMessage(Message).When(value => value < 0).Build();

        IBusinessRuleMetadata metadata = rule;

        Assert.Equal(Code, metadata.Code);
        Assert.Equal(Message, metadata.Message);
    }

    [Fact]
    public void TypedIAsyncBusinessRule_ExtendsIBusinessRuleMetadata()
    {
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage(Message).WhenAsync(value => Task.FromResult(value < 0)).Build();

        IBusinessRuleMetadata metadata = rule;

        Assert.Equal(Code, metadata.Code);
        Assert.Equal(Message, metadata.Message);
    }

    [Fact]
    public void TypedIBusinessRule_DoesNotDeriveFromIBusinessRule()
    {
        Assert.False(typeof(IBusinessRule).IsAssignableFrom(typeof(IBusinessRule<int>)));
        Assert.False(typeof(IAsyncBusinessRule).IsAssignableFrom(typeof(IAsyncBusinessRule<int>)));
    }

    // ---------------------------------------------------------------------
    // Default interface members
    // ---------------------------------------------------------------------

    [Fact]
    public void MinimalMetadata_UsesDefaultInterfaceMembers()
    {
        IBusinessRuleMetadata metadata = new MinimalMetadata();

        Assert.Equal(RuleSeverity.Error, metadata.Severity);
        Assert.Equal(nameof(MinimalMetadata), metadata.Name);
        Assert.Equal(string.Empty, metadata.Description);
        Assert.Equal(string.Empty, metadata.Category);
        Assert.Empty(metadata.Tags);
    }

    [Fact]
    public void MinimalTypedRule_UsesDefaultInterfaceMembers()
    {
        IBusinessRule<int> rule = new MinimalTypedRule();

        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.Equal(nameof(MinimalTypedRule), rule.Name);
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
    }

    [Fact]
    public void MinimalTypedRule_EvaluateWithContext_FallsBackToEvaluateWithoutContext()
    {
        IBusinessRule<int> rule = new MinimalTypedRule();

        BusinessRuleResult result = rule.Evaluate(5, new BusinessRuleContext());

        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
    }

    [Fact]
    public void MinimalTypedAsyncRule_UsesDefaultInterfaceMembers()
    {
        IAsyncBusinessRule<int> rule = new MinimalTypedAsyncRule();

        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.Equal(nameof(MinimalTypedAsyncRule), rule.Name);
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
    }

    [Fact]
    public void BoundRule_OverMinimalTypedRule_ForwardsDefaultedMetadata()
    {
        IBusinessRule bound = new MinimalTypedRule().For(-1);

        Assert.Equal(nameof(MinimalTypedRule), bound.Name);
        Assert.Equal(RuleSeverity.Error, bound.Severity);
        Assert.True(bound.IsBroken());
    }

    private sealed class MinimalMetadata : IBusinessRuleMetadata
    {
        public string Code => BusinessRuleMetadataTests.Code;
        public string Message => BusinessRuleMetadataTests.Message;
    }

    private sealed class MinimalTypedRule : IBusinessRule<int>
    {
        public string Code => BusinessRuleMetadataTests.Code;
        public string Message => BusinessRuleMetadataTests.Message;
        public bool IsBroken(int subject) => subject < 0;
        public bool IsBroken(int subject, BusinessRuleContext context) => IsBroken(subject);
        public BusinessRuleResult Evaluate(int subject) => new BusinessRuleResult(Code, Message, RuleSeverity.Error);
    }

    private sealed class MinimalTypedAsyncRule : IAsyncBusinessRule<int>
    {
        public string Code => BusinessRuleMetadataTests.Code;
        public string Message => BusinessRuleMetadataTests.Message;
        public Task<bool> IsBrokenAsync(int subject, BusinessRuleContext context, CancellationToken cancellationToken = default) => Task.FromResult(subject < 0);
        public Task<BusinessRuleResult?> EvaluateAsync(int subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<BusinessRuleResult?>(subject < 0 ? new BusinessRuleResult(Code, Message, RuleSeverity.Error) : null);
    }
}
