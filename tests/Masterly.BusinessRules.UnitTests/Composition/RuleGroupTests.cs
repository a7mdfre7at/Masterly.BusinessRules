namespace Masterly.BusinessRules.UnitTests;

public class RuleGroupTests
{
    private const string GroupCode = "GROUP.BROKEN";
    private const string GroupMessage = "The group is broken";
    private const string FlagKey = "flag";

    // ---------------------------------------------------------------------
    // Truth tables (three members; true = broken)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    public void All_IsBroken_WhenAnyMemberIsBroken(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, group.IsBroken());
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, true)]
    public void Any_IsBroken_OnlyWhenEveryMemberIsBroken(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupBusinessRule group = RuleGroup.Any(GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, group.IsBroken());
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, true)]
    public void AtLeast_IsBroken_WhenFewerThanThresholdMembersPass(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupBusinessRule group = RuleGroup.AtLeast(2, GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, group.IsBroken());
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    public void Exactly_IsBroken_UnlessExactlyThresholdMembersPass(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupBusinessRule group = RuleGroup.Exactly(1, GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, group.IsBroken());
    }

    [Fact]
    public void AtLeast_ZeroThreshold_AlwaysPasses()
    {
        GroupBusinessRule group = RuleGroup.AtLeast(0, GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule("A"), RuleTestHelper.CreateBrokenRule("B"));

        Assert.False(group.IsBroken());
    }

    [Fact]
    public void Exactly_ZeroThreshold_BrokenWhenAnyMemberPasses()
    {
        GroupBusinessRule allBroken = RuleGroup.Exactly(0, GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule("A"), RuleTestHelper.CreateBrokenRule("B"));
        GroupBusinessRule onePasses = RuleGroup.Exactly(0, GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule("A"), RuleTestHelper.CreatePassingRule("B"));

        Assert.False(allBroken.IsBroken());
        Assert.True(onePasses.IsBroken());
    }

    // ---------------------------------------------------------------------
    // Metadata and structure
    // ---------------------------------------------------------------------

    [Fact]
    public void All_ExposesMetadataOperatorAndOperands()
    {
        IBusinessRule a = RuleTestHelper.CreatePassingRule("A");
        IBusinessRule b = RuleTestHelper.CreatePassingRule("B");

        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, a, b);

        Assert.Equal(GroupCode, group.Code);
        Assert.Equal(GroupMessage, group.Message);
        Assert.Equal(RuleSeverity.Error, group.Severity);
        Assert.Equal("GroupBusinessRule", group.Name);
        Assert.Equal(RuleGroupQuantifier.All, group.Quantifier);
        Assert.Equal(RuleOperator.All, group.Operator);
        Assert.Equal(2, group.Threshold);
        Assert.False(group.ShortCircuit);
        Assert.Equal(new[] { a, b }, group.Operands);
        Assert.Contains("ALL", group.Description);
        Assert.Empty(group.Category);
        Assert.Empty(group.Tags);
    }

    [Fact]
    public void Any_ThresholdIsOne()
    {
        GroupBusinessRule group = RuleGroup.Any(GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"), RuleTestHelper.CreatePassingRule("B"));

        Assert.Equal(1, group.Threshold);
        Assert.Equal(RuleOperator.Any, group.Operator);
    }

    [Fact]
    public void AtLeast_ExposesRequestedThreshold()
    {
        GroupBusinessRule group = RuleGroup.AtLeast(2, GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"), RuleTestHelper.CreatePassingRule("B"), RuleTestHelper.CreatePassingRule("C"));

        Assert.Equal(2, group.Threshold);
        Assert.Equal(RuleOperator.AtLeast, group.Operator);
        Assert.Equal(RuleGroupQuantifier.AtLeast, group.Quantifier);
    }

    [Fact]
    public void Exactly_ExposesRequestedThreshold()
    {
        GroupBusinessRule group = RuleGroup.Exactly(1, GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"), RuleTestHelper.CreatePassingRule("B"));

        Assert.Equal(1, group.Threshold);
        Assert.Equal(RuleOperator.Exactly, group.Operator);
    }

    [Fact]
    public void All_WithEnumerableMembers_CreatesGroup()
    {
        List<IBusinessRule> members = new List<IBusinessRule> { RuleTestHelper.CreateBrokenRule("A") };

        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, members);

        Assert.True(group.IsBroken());
        Assert.Single(group.Operands);
    }

    [Fact]
    public void Group_ImplementsIComposedBusinessRule()
    {
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"));

        Assert.IsAssignableFrom<IComposedBusinessRule>(group);
    }

    // ---------------------------------------------------------------------
    // Immutability
    // ---------------------------------------------------------------------

    [Fact]
    public void WithShortCircuit_ReturnsNewInstance_OriginalUnchanged()
    {
        GroupBusinessRule original = RuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"));

        GroupBusinessRule shortCircuiting = original.WithShortCircuit();

        Assert.NotSame(original, shortCircuiting);
        Assert.False(original.ShortCircuit);
        Assert.True(shortCircuiting.ShortCircuit);
        Assert.Equal(original.Code, shortCircuiting.Code);
        Assert.Equal(original.Operands, shortCircuiting.Operands);
    }

    [Fact]
    public void WithSeverity_ReturnsNewInstance_OriginalUnchanged()
    {
        GroupBusinessRule original = RuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule("A"));

        GroupBusinessRule warning = original.WithSeverity(RuleSeverity.Warning);

        Assert.NotSame(original, warning);
        Assert.Equal(RuleSeverity.Error, original.Severity);
        Assert.Equal(RuleSeverity.Warning, warning.Severity);
        Assert.Equal(RuleSeverity.Warning, Assert.Throws<BusinessRuleValidationException>(() => warning.Check()).BrokenRules.Single().Severity);
    }

    // ---------------------------------------------------------------------
    // Short-circuit
    // ---------------------------------------------------------------------

    [Fact]
    public void All_WithoutShortCircuit_EvaluatesEveryMember()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage,
            CreateCountingRule("A", broken: true, () => evaluations++),
            CreateCountingRule("B", broken: true, () => evaluations++),
            CreateCountingRule("C", broken: true, () => evaluations++));

        group.IsBroken();

        Assert.Equal(3, evaluations);
    }

    [Fact]
    public void All_WithShortCircuit_StopsAtFirstBrokenMember()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage,
            CreateCountingRule("A", broken: true, () => evaluations++),
            CreateCountingRule("B", broken: true, () => evaluations++),
            CreateCountingRule("C", broken: true, () => evaluations++)).WithShortCircuit();

        bool isBroken = group.IsBroken();

        Assert.True(isBroken);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void Any_WithShortCircuit_StopsAtFirstPassingMember()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.Any(GroupCode, GroupMessage,
            CreateCountingRule("A", broken: true, () => evaluations++),
            CreateCountingRule("B", broken: false, () => evaluations++),
            CreateCountingRule("C", broken: false, () => evaluations++)).WithShortCircuit();

        bool isBroken = group.IsBroken();

        Assert.False(isBroken);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void AtLeast_WithShortCircuit_StopsOnceThresholdIsReached()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.AtLeast(2, GroupCode, GroupMessage,
            CreateCountingRule("A", broken: false, () => evaluations++),
            CreateCountingRule("B", broken: false, () => evaluations++),
            CreateCountingRule("C", broken: false, () => evaluations++)).WithShortCircuit();

        bool isBroken = group.IsBroken();

        Assert.False(isBroken);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void AtLeast_WithShortCircuit_StopsOnceThresholdIsUnreachable()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.AtLeast(2, GroupCode, GroupMessage,
            CreateCountingRule("A", broken: true, () => evaluations++),
            CreateCountingRule("B", broken: true, () => evaluations++),
            CreateCountingRule("C", broken: false, () => evaluations++)).WithShortCircuit();

        bool isBroken = group.IsBroken();

        Assert.True(isBroken);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void Exactly_WithShortCircuit_StopsOncePassingCountExceedsThreshold()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.Exactly(1, GroupCode, GroupMessage,
            CreateCountingRule("A", broken: false, () => evaluations++),
            CreateCountingRule("B", broken: false, () => evaluations++),
            CreateCountingRule("C", broken: false, () => evaluations++)).WithShortCircuit();

        bool isBroken = group.IsBroken();

        Assert.True(isBroken);
        Assert.Equal(2, evaluations);
    }

    // ---------------------------------------------------------------------
    // Context, results and checker integration
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void All_IsBrokenWithContext_ForwardsContextToMembers(bool flag, bool expectedBroken)
    {
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, CreateContextReadingRule("A"), CreateContextReadingRule("B"));

        bool actualBroken = group.IsBroken(RuleTestHelper.CreateContext((FlagKey, flag)));

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public void Check_WhenBroken_ThrowsSingleResultWithBrokenMembersInDetails()
    {
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreateBrokenRule("A"),
            RuleTestHelper.CreatePassingRule("B"),
            RuleTestHelper.CreateBrokenRule("C"));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => group.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(GroupCode, result.Code);
        Assert.Equal(GroupMessage, result.Message);
        Assert.Equal(new[] { "A", "C" }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public void CheckWithContext_WhenBroken_ThrowsSingleResultWithDetails()
    {
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, CreateContextReadingRule("A"));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => group.Check(RuleTestHelper.CreateContext((FlagKey, true))));

        Assert.Equal("A", Assert.Single(Assert.Single(exception.BrokenRules).Details).Code);
    }

    [Fact]
    public void Check_WhenPassing_DoesNotThrow()
    {
        GroupBusinessRule group = RuleGroup.Any(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule("A"), RuleTestHelper.CreatePassingRule("B"));

        group.Check();
        group.Check(RuleTestHelper.CreateContext());
    }

    [Fact]
    public void Exactly_WhenBrokenBecauseTooManyPass_DetailsIsEmpty()
    {
        GroupBusinessRule group = RuleGroup.Exactly(1, GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"), RuleTestHelper.CreatePassingRule("B"));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => group.Check());

        Assert.Empty(Assert.Single(exception.BrokenRules).Details);
    }

    [Fact]
    public void Evaluate_ReturnsResultWithoutEvaluatingMembers()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, CreateCountingRule("A", broken: true, () => evaluations++));

        BusinessRuleResult result = group.Evaluate();

        Assert.Equal(GroupCode, result.Code);
        Assert.Empty(result.Details);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void NestedGroups_DetailsAreNested_AndFlattenReturnsLeaves()
    {
        GroupBusinessRule inner = RuleGroup.Any("INNER", "inner", RuleTestHelper.CreateBrokenRule("A"), RuleTestHelper.CreateBrokenRule("B"));
        GroupBusinessRule outer = RuleGroup.All("OUTER", "outer", inner, RuleTestHelper.CreateBrokenRule("C"));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => outer.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(new[] { "INNER", "C" }, result.Details.Select(detail => detail.Code));
        Assert.Equal(new[] { "A", "B" }, result.Details[0].Details.Select(detail => detail.Code));
        Assert.Equal(new[] { "A", "B", "C" }, exception.Flatten().Select(leaf => leaf.Code));
    }

    [Fact]
    public void BusinessRuleChecker_CheckAll_SurfacesMemberResultsInDetails()
    {
        int evaluations = 0;
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage,
            CreateCountingRule("A", broken: true, () => evaluations++),
            CreateCountingRule("B", broken: false, () => evaluations++));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(group));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal("A", Assert.Single(result.Details).Code);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void BusinessRuleChecker_EvaluateAll_SurfacesMemberResultsInDetails()
    {
        GroupBusinessRule group = RuleGroup.Any(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule("A"), RuleTestHelper.CreateBrokenRule("B"));

        RuleEvaluationResult evaluation = BusinessRuleChecker.EvaluateAll(RuleTestHelper.CreateContext(), group);

        BusinessRuleResult result = Assert.Single(evaluation.BrokenRules);
        Assert.Equal(new[] { "A", "B" }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public void BusinessRuleChecker_CheckAll_WithObserver_ReportsGroupResultWithDetails()
    {
        GroupBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule("A"));
        RecordingObserver observer = new RecordingObserver();

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(new[] { group }, observer: observer));

        Assert.NotNull(observer.LastBrokenResult);
        Assert.Single(observer.LastBrokenResult.Details);
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void All_NullCode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleGroup.All(null!, GroupMessage, RuleTestHelper.CreatePassingRule("A")));
    }

    [Fact]
    public void All_NullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleGroup.All(GroupCode, null!, RuleTestHelper.CreatePassingRule("A")));
    }

    [Fact]
    public void All_NullMembers_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleGroup.All(GroupCode, GroupMessage, (IEnumerable<IBusinessRule>)null!));
    }

    [Fact]
    public void All_EmptyMembers_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RuleGroup.All(GroupCode, GroupMessage));
    }

    [Fact]
    public void Any_NullMember_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RuleGroup.Any(GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"), null!));
    }

    [Fact]
    public void AtLeast_NegativeThreshold_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleGroup.AtLeast(-1, GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A")));
    }

    [Fact]
    public void AtLeast_ThresholdAboveMemberCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleGroup.AtLeast(2, GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A")));
    }

    [Fact]
    public void Exactly_ThresholdAboveMemberCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleGroup.Exactly(3, GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule("A"), RuleTestHelper.CreatePassingRule("B")));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateRule(string code, bool broken)
    {
        return broken ? RuleTestHelper.CreateBrokenRule(code) : RuleTestHelper.CreatePassingRule(code);
    }

    private static IBusinessRule CreateCountingRule(string code, bool broken, Action onEvaluated)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(() =>
            {
                onEvaluated();
                return broken;
            })
            .Build();
    }

    private static IBusinessRule CreateContextReadingRule(string code)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(ctx => ctx.Get<bool>(FlagKey))
            .Build();
    }

    private sealed class RecordingObserver : IRuleExecutionObserver
    {
        public BusinessRuleResult? LastBrokenResult { get; private set; }

        public void OnBeforeEvaluate(IBusinessRule rule)
        {
        }

        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
        {
        }

        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
        {
            LastBrokenResult = result;
        }
    }
}
