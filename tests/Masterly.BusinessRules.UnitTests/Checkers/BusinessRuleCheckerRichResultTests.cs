namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleCheckerRichResultTests
{
    private const string Payments = "Payments";
    private const string Identity = "Identity";
    private const string CustomerFacing = "customer-facing";

    private sealed class ThrowingRule : BaseBusinessRule
    {
        public override string Code => "THROWS";
        public override string Message => "Throws";
        public override string Name => "ThrowingRule";
        public override string Category => Payments;
        public override IReadOnlyList<string> Tags => new[] { CustomerFacing };

        public override bool IsBroken() => throw new InvalidOperationException("boom");
    }

    private sealed class ThrowingMetadataRule : BaseBusinessRule
    {
        public override string Code => throw new InvalidOperationException("no code");
        public override string Message => "Throws";

        public override bool IsBroken() => throw new InvalidOperationException("boom");
    }

    private sealed class NullMetadataRule : BaseBusinessRule
    {
        public override string Code => "NULL_META";
        public override string Message => "Null metadata";
        public override string Name => null!; // intentional: a nullable-oblivious rule violating the contract
        public override string Category => null!; // intentional: a nullable-oblivious rule violating the contract
        public override IReadOnlyList<string> Tags => null!; // intentional: a nullable-oblivious rule violating the contract

        public override bool IsBroken() => true;
    }

    private static IBusinessRule Rule(string code, string category, params string[] tags)
        => BusinessRuleBuilder.Create(code).WithName($"{code}-name").WithCategory(category).WithTags(tags).When(() => true).Build();

    [Fact]
    public void CheckAll_ExceptionResultsCarryMetadata()
    {
        IBusinessRule[] rules = { Rule("A", Payments, CustomerFacing), Rule("B", Identity, "internal"), RuleTestHelper.CreatePassingRule() };

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(rules));

        Assert.Equal(new[] { "A-name", "B-name" }, exception.BrokenRules.Select(result => result.Name));
        Assert.Equal(new[] { Payments, Identity }, exception.BrokenRules.Select(result => result.Category));
        Assert.Equal("A", Assert.Single(exception.ByTag(CustomerFacing)).Code);
        Assert.Equal("B", Assert.Single(exception.ByCategory(Identity)).Code);
    }

    [Fact]
    public void EvaluateAll_ResultsCarryMetadata()
    {
        IBusinessRule[] rules = { Rule("A", Payments, CustomerFacing), Rule("B", Identity, "internal") };

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules);

        Assert.Equal("A", Assert.Single(result.ByTag("CUSTOMER-FACING")).Code);
        Assert.Equal("B", Assert.Single(result.ByCategory(Identity)).Code);
        Assert.All(result.Entries, entry => Assert.Equal(entry.Rule!.Name, entry.Result!.Name));
    }

    [Fact]
    public void CheckAll_WithThrowOn_CollectedRulesCarryMetadata()
    {
        IBusinessRule warning = Rule("W", Payments, CustomerFacing).WithSeverity(RuleSeverity.Warning);
        IBusinessRule error = Rule("E", Identity);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(new[] { warning, error }, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error }));

        BusinessRuleResult collected = Assert.Single(exception.CollectedRules);
        Assert.Equal("W-name", collected.Name);
        Assert.True(collected.HasTag(CustomerFacing));
        Assert.Empty(exception.ByTag(CustomerFacing));
    }

    [Fact]
    public void EvaluateAll_TreatAsBroken_StandInResultCarriesMetadata()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(
            new IBusinessRule[] { new ThrowingRule() },
            new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken });

        BusinessRuleResult standIn = Assert.Single(result.BrokenRules);
        Assert.Equal("ThrowingRule", standIn.Name);
        Assert.Equal(Payments, standIn.Category);
        Assert.True(standIn.HasTag(CustomerFacing));
        Assert.Null(standIn.Detail);
    }

    [Fact]
    public void EvaluateAll_TreatAsBroken_ThrowingMetadata_FallsBackToTypeName()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(
            new IBusinessRule[] { new ThrowingMetadataRule() },
            new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken });

        BusinessRuleResult standIn = Assert.Single(result.BrokenRules);
        Assert.Equal(nameof(ThrowingMetadataRule), standIn.Code);
        Assert.Equal(nameof(ThrowingMetadataRule), standIn.Name);
        Assert.Equal(string.Empty, standIn.Category);
        Assert.Empty(standIn.Tags);
    }

    [Fact]
    public void CheckAll_ComposedRule_MemberResultsCarryMetadata()
    {
        IBusinessRule group = RuleGroup.All("G", "Group", Rule("A", Payments, CustomerFacing), Rule("B", Identity));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(group));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(nameof(GroupBusinessRule), result.Name);
        Assert.Equal(new[] { "A-name", "B-name" }, result.Details.Select(member => member.Name));
        Assert.Equal("A", Assert.Single(exception.Flatten(), leaf => leaf.HasTag(CustomerFacing)).Code);
    }

    [Fact]
    public void CheckByTags_ExceptionResultsCarryTags()
    {
        IBusinessRule[] rules = { Rule("A", Payments, CustomerFacing), Rule("B", Identity, "internal") };

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckByTags(rules, CustomerFacing));

        Assert.True(Assert.Single(exception.BrokenRules).HasTag(CustomerFacing));
    }

    [Fact]
    public void CheckAll_NoBrokenRules_DoesNotThrow()
    {
        BusinessRuleChecker.CheckAll(RuleTestHelper.CreatePassingRule(), RuleTestHelper.CreatePassingRule());
    }

    [Fact]
    public void EvaluateAll_RuleWithNullMetadata_ReportsCodeAsNameEmptyCategoryAndNoTags()
    {
        NullMetadataRule rule = new NullMetadataRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rule);

        BusinessRuleResult broken = Assert.Single(result.BrokenRules);
        Assert.Equal(rule.Code, broken.Name);
        Assert.Equal(string.Empty, broken.Category);
        Assert.Empty(broken.Tags);
        Assert.Empty(result.ByTag(CustomerFacing));
        Assert.Equal(broken, Assert.Single(result.ByCategory(string.Empty)));
    }

    [Fact]
    public void Evaluate_RuleWithNullMetadata_DoesNotThrow()
    {
        NullMetadataRule rule = new NullMetadataRule();

        BusinessRuleResult result = rule.Evaluate();

        Assert.Equal(rule.Code, result.Name);
        Assert.Empty(result.Tags);
        Assert.False(result.HasTag(CustomerFacing));
    }
}
