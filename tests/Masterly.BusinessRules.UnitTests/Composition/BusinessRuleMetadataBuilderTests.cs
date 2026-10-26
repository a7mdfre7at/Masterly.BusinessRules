namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleMetadataBuilderTests
{
    private const string Code = "SHARED.CODE";

    [Fact]
    public void AllFourBuilders_DeriveFromTheSharedMetadataBuilder()
    {
        Assert.IsAssignableFrom<BusinessRuleMetadataBuilder<BusinessRuleBuilder>>(BusinessRuleBuilder.Create(Code));
        Assert.IsAssignableFrom<BusinessRuleMetadataBuilder<AsyncBusinessRuleBuilder>>(AsyncBusinessRuleBuilder.Create(Code));
        Assert.IsAssignableFrom<BusinessRuleMetadataBuilder<BusinessRuleBuilder<int>>>(BusinessRuleBuilder<int>.Create(Code));
        Assert.IsAssignableFrom<BusinessRuleMetadataBuilder<AsyncBusinessRuleBuilder<int>>>(AsyncBusinessRuleBuilder<int>.Create(Code));
    }

    [Fact]
    public void SyncBuilder_FluentChain_ReturnsTheSameSyncBuilder()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        BusinessRuleBuilder chained = builder
            .WithMessage("m")
            .WithSeverity(RuleSeverity.Info)
            .WithName("n")
            .WithDescription("d")
            .WithCategory("c")
            .WithTags("t")
            .WithDetail(_ => $"detail")
            .WithActual(_ => 1)
            .WithExpected(2)
            .WithRemediation("fix");

        Assert.Same(builder, chained);
    }

    [Fact]
    public void AsyncBuilder_FluentChain_ReturnsTheSameAsyncBuilder()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code);

        AsyncBusinessRuleBuilder chained = builder
            .WithMessage("m")
            .WithSeverity(RuleSeverity.Info)
            .WithName("n")
            .WithDescription("d")
            .WithCategory("c")
            .WithTags("t")
            .WithDetail(_ => $"detail")
            .WithActual(_ => 1)
            .WithExpected(_ => 2)
            .WithRemediation(_ => "fix");

        Assert.Same(builder, chained);
    }

    [Fact]
    public async Task AllFourBuilders_DefaultNameToCodeAndKeepMetadata()
    {
        IBusinessRule sync = BusinessRuleBuilder.Create(Code).WithCategory("c").WithTags("a").WithTags("b").When(() => true).Build();
        IAsyncBusinessRule asyncRule = AsyncBusinessRuleBuilder.Create(Code).WithCategory("c").WithTags("a").WithTags("b").WhenAsync(_ => Task.FromResult(true)).Build();
        IBusinessRule<int> typedSync = BusinessRuleBuilder<int>.Create(Code).WithCategory("c").WithTags("a").WithTags("b").When(_ => true).Build();
        IAsyncBusinessRule<int> typedAsync = AsyncBusinessRuleBuilder<int>.Create(Code).WithCategory("c").WithTags("a").WithTags("b").WhenAsync(_ => Task.FromResult(true)).Build();

        foreach (IBusinessRuleMetadata metadata in new IBusinessRuleMetadata[] { sync, asyncRule, typedSync, typedAsync })
        {
            Assert.Equal(Code, metadata.Name);
            Assert.Equal("c", metadata.Category);
            Assert.Equal(new[] { "a", "b" }, metadata.Tags);
            Assert.Equal(RuleSeverity.Error, metadata.Severity);
        }

        Assert.Equal("Business rule violated.", sync.Message);
        Assert.Equal("Async business rule violated.", asyncRule.Message);
        Assert.True(sync.IsBroken());
        Assert.True(await asyncRule.IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SyncBuilder_WithDetailNull_ThrowsArgumentNullException()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithDetail(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithActual(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithExpected((Func<BusinessRuleContext, object?>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithRemediation((string)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithRemediation((Func<BusinessRuleContext, string>)null!));
    }

    [Fact]
    public void SyncBuilder_ExistingDetailSurface_StillProducesDetails()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithDetail(ctx => $"flag {ctx.Get<bool>("flag")}")
            .WithExpected(5)
            .WithRemediation("fix it")
            .When(_ => true)
            .Build();

        BusinessRuleResult result = rule.Evaluate(RuleTestHelper.CreateContext(("flag", true)));

        Assert.Equal("flag True", result.Detail);
        Assert.Equal(5, result.Expected);
        Assert.Equal("fix it", result.Remediation);
    }
}
