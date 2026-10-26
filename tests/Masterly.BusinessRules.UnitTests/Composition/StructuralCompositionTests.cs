namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// Covers the structural And/Or/Not nodes on the sync side: runtime types, walker interfaces,
/// context forwarding, short-circuiting and member results. The truth tables themselves are pinned
/// by <see cref="CompositionTruthTableTests"/>.
/// </summary>
public class StructuralCompositionTests
{
    private const string FirstCode = "FIRST";
    private const string SecondCode = "SECOND";
    private const string FlagKey = "flag";

    [Fact]
    public void And_ReturnsAndBusinessRule_WithOperatorAndOperands()
    {
        IBusinessRule first = RuleTestHelper.CreatePassingRule(FirstCode);
        IBusinessRule second = RuleTestHelper.CreatePassingRule(SecondCode);

        IBusinessRule composed = first.And(second);

        AndBusinessRule node = Assert.IsType<AndBusinessRule>(composed);
        Assert.Equal(RuleOperator.And, node.Operator);
        Assert.Equal(2, node.Operands.Count);
        Assert.Same(first, node.Operands[0]);
        Assert.Same(second, node.Operands[1]);
    }

    [Fact]
    public void Or_ReturnsOrBusinessRule_WithOperatorAndOperands()
    {
        IBusinessRule first = RuleTestHelper.CreatePassingRule(FirstCode);
        IBusinessRule second = RuleTestHelper.CreatePassingRule(SecondCode);

        IBusinessRule composed = first.Or(second);

        OrBusinessRule node = Assert.IsType<OrBusinessRule>(composed);
        Assert.Equal(RuleOperator.Or, node.Operator);
        Assert.Equal(new[] { first, second }, node.Operands);
    }

    [Fact]
    public void Not_ReturnsNotBusinessRule_WithSingleOperand()
    {
        IBusinessRule operand = RuleTestHelper.CreatePassingRule(FirstCode);

        IBusinessRule composed = operand.Not();

        NotBusinessRule node = Assert.IsType<NotBusinessRule>(composed);
        Assert.Equal(RuleOperator.Not, node.Operator);
        IBusinessRule only = Assert.Single(node.Operands);
        Assert.Same(operand, only);
    }

    [Fact]
    public void And_ImplementsIComposedBusinessRule()
    {
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(FirstCode).And(RuleTestHelper.CreatePassingRule(SecondCode));

        Assert.IsAssignableFrom<IComposedBusinessRule>(composed);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void And_IsBrokenWithContext_ForwardsContextToBothOperands(bool flag, bool expectedBroken)
    {
        IBusinessRule composed = CreateContextReadingRule(FirstCode).And(CreateContextReadingRule(SecondCode));
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, flag));

        bool actualBroken = composed.IsBroken(context);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Or_IsBrokenWithContext_ForwardsContextToBothOperands(bool flag, bool expectedBroken)
    {
        IBusinessRule composed = CreateContextReadingRule(FirstCode).Or(CreateContextReadingRule(SecondCode));
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, flag));

        bool actualBroken = composed.IsBroken(context);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Not_IsBrokenWithContext_ForwardsContextToOperand(bool flag, bool expectedBroken)
    {
        IBusinessRule composed = CreateContextReadingRule(FirstCode).Not();
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, flag));

        bool actualBroken = composed.IsBroken(context);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public void And_CheckWithContext_ForwardsContextToOperands()
    {
        IBusinessRule composed = CreateContextReadingRule(FirstCode).And(CreateContextReadingRule(SecondCode));
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, true));

        Assert.Throws<BusinessRuleValidationException>(() => composed.Check(context));
    }

    [Fact]
    public void And_IsBrokenWithContext_ReachesParameterlessRulesThroughBaseClass()
    {
        OverloadRecordingRule recording = new OverloadRecordingRule();
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode).And(recording);

        composed.IsBroken(RuleTestHelper.CreateContext());

        Assert.True(recording.LastCallHadContext);
    }

    [Fact]
    public void And_IsBrokenWithoutContext_UsesParameterlessOverload()
    {
        OverloadRecordingRule recording = new OverloadRecordingRule();
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode).And(recording);

        composed.IsBroken();

        Assert.False(recording.LastCallHadContext);
    }

    [Fact]
    public void And_IsBroken_WhenFirstPasses_DoesNotEvaluateSecond()
    {
        int secondEvaluations = 0;
        IBusinessRule second = CreateCountingRule(SecondCode, broken: true, () => secondEvaluations++);
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(FirstCode).And(second);

        bool actualBroken = composed.IsBroken();

        Assert.False(actualBroken);
        Assert.Equal(0, secondEvaluations);
    }

    [Fact]
    public void Or_IsBroken_WhenFirstBroken_DoesNotEvaluateSecond()
    {
        int secondEvaluations = 0;
        IBusinessRule second = CreateCountingRule(SecondCode, broken: false, () => secondEvaluations++);
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode).Or(second);

        bool actualBroken = composed.IsBroken();

        Assert.True(actualBroken);
        Assert.Equal(0, secondEvaluations);
    }

    [Fact]
    public void And_IsBroken_EvaluatesEachOperandExactlyOnce()
    {
        int firstEvaluations = 0;
        int secondEvaluations = 0;
        IBusinessRule composed = CreateCountingRule(FirstCode, broken: true, () => firstEvaluations++)
            .And(CreateCountingRule(SecondCode, broken: true, () => secondEvaluations++));

        composed.IsBroken();

        Assert.Equal(1, firstEvaluations);
        Assert.Equal(1, secondEvaluations);
    }

    [Fact]
    public void And_Check_WhenBroken_ResultDetailsContainBothOperandResults()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode).And(RuleTestHelper.CreateBrokenRule(SecondCode));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => composed.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(new[] { FirstCode, SecondCode }, result.Details.Select(detail => detail.Code));
        Assert.Equal(new[] { FirstCode, SecondCode }, exception.Flatten().Select(leaf => leaf.Code));
    }

    [Fact]
    public void Or_Check_WhenOnlySecondBroken_ResultDetailsContainSecondOnly()
    {
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(FirstCode).Or(RuleTestHelper.CreateBrokenRule(SecondCode));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => composed.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        BusinessRuleResult detail = Assert.Single(result.Details);
        Assert.Equal(SecondCode, detail.Code);
    }

    [Fact]
    public void Not_Check_WhenBroken_ResultHasNoDetails()
    {
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(FirstCode).Not();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => composed.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal($"!{FirstCode}", result.Code);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void And_Evaluate_DoesNotEvaluateOperands()
    {
        int evaluations = 0;
        IBusinessRule composed = CreateCountingRule(FirstCode, broken: true, () => evaluations++)
            .And(CreateCountingRule(SecondCode, broken: true, () => evaluations++));

        BusinessRuleResult result = composed.Evaluate();

        Assert.Equal($"{FirstCode}+{SecondCode}", result.Code);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void And_Message_KeepsLegacySynthesisedText()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode, "first").And(RuleTestHelper.CreateBrokenRule(SecondCode, "second"));

        Assert.Equal("Both rules must be satisfied: first AND second", composed.Message);
    }

    [Fact]
    public void Or_Message_KeepsLegacySynthesisedText()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode, "first").Or(RuleTestHelper.CreateBrokenRule(SecondCode, "second"));

        Assert.Equal("At least one rule must be satisfied: first OR second", composed.Message);
    }

    [Fact]
    public void Not_Message_KeepsLegacySynthesisedText()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode, "first").Not();

        Assert.Equal("NOT: first", composed.Message);
    }

    [Fact]
    public void AndBusinessRule_Constructor_NullOperand_ThrowsArgumentNullException()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule(FirstCode);

        Assert.Throws<ArgumentNullException>(() => new AndBusinessRule(null!, rule));
        Assert.Throws<ArgumentNullException>(() => new AndBusinessRule(rule, null!));
    }

    [Fact]
    public void OrBusinessRule_Constructor_NullOperand_ThrowsArgumentNullException()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule(FirstCode);

        Assert.Throws<ArgumentNullException>(() => new OrBusinessRule(null!, rule));
        Assert.Throws<ArgumentNullException>(() => new OrBusinessRule(rule, null!));
    }

    [Fact]
    public void NotBusinessRule_Constructor_NullOperand_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new NotBusinessRule(null!));
    }

    [Fact]
    public void NestedComposition_KeepsTreeShape()
    {
        IBusinessRule a = RuleTestHelper.CreatePassingRule("A");
        IBusinessRule b = RuleTestHelper.CreatePassingRule("B");
        IBusinessRule c = RuleTestHelper.CreatePassingRule("C");

        IBusinessRule composed = a.And(b).Or(c.Not());

        IComposedBusinessRule root = Assert.IsAssignableFrom<IComposedBusinessRule>(composed);
        Assert.Equal(RuleOperator.Or, root.Operator);
        IComposedBusinessRule left = Assert.IsAssignableFrom<IComposedBusinessRule>(root.Operands[0]);
        Assert.Equal(RuleOperator.And, left.Operator);
        IComposedBusinessRule right = Assert.IsAssignableFrom<IComposedBusinessRule>(root.Operands[1]);
        Assert.Equal(RuleOperator.Not, right.Operator);
        Assert.Equal("A+B|!C", composed.Code);
    }

    [Fact]
    public void CompositeBusinessRule_ImplementsIComposed_WithOperatorAll()
    {
        IBusinessRule first = RuleTestHelper.CreatePassingRule(FirstCode);
        IBusinessRule second = RuleTestHelper.CreatePassingRule(SecondCode);
        CompositeBusinessRule composite = new CompositeBusinessRule(new[] { first, second });

        IComposedBusinessRule composed = Assert.IsAssignableFrom<IComposedBusinessRule>(composite);

        Assert.Equal(RuleOperator.All, composed.Operator);
        Assert.Equal(new[] { first, second }, composed.Operands);
    }

    [Fact]
    public void CompositeBusinessRule_Check_WhenBroken_ThrowsEachBrokenChildIndividually()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(new[]
        {
            RuleTestHelper.CreateBrokenRule(FirstCode),
            RuleTestHelper.CreatePassingRule("MIDDLE"),
            RuleTestHelper.CreateBrokenRule(SecondCode)
        });

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => composite.Check());

        Assert.Equal(new[] { FirstCode, SecondCode }, exception.BrokenRules.Select(result => result.Code));
    }

    [Fact]
    public void CompositeBusinessRule_IsBrokenWithContext_ForwardsContext()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(new[] { CreateContextReadingRule(FirstCode) });

        Assert.True(composite.IsBroken(RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.False(composite.IsBroken(RuleTestHelper.CreateContext((FlagKey, false))));
    }

    [Fact]
    public void CompositeBusinessRule_EmptyRules_PassesAndEvaluateAllIsEmpty()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(Array.Empty<IBusinessRule>());

        Assert.False(composite.IsBroken());
        Assert.Empty(composite.EvaluateAll());
        Assert.Empty(composite.Operands);
    }

    [Fact]
    public void CachedBusinessRule_ImplementsIDecorated_ExposingInnerRule()
    {
        IBusinessRule inner = RuleTestHelper.CreatePassingRule(FirstCode);

        IBusinessRule cached = inner.Cached(TimeSpan.FromMinutes(1));

        IDecoratedBusinessRule decorated = Assert.IsAssignableFrom<IDecoratedBusinessRule>(cached);
        Assert.Same(inner, decorated.InnerRule);
    }

    [Fact]
    public void CachedBusinessRule_Check_WhenInnerGroupBroken_ThrowsResultWithMemberDetails()
    {
        const string groupCode = "GROUP";
        IBusinessRule group = RuleGroup.All(groupCode, groupCode, RuleTestHelper.CreateBrokenRule(FirstCode), RuleTestHelper.CreateBrokenRule(SecondCode));
        IBusinessRule cached = group.Cached(TimeSpan.FromMinutes(1));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => cached.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(groupCode, result.Code);
        Assert.Equal(new[] { FirstCode, SecondCode }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public void CachedBusinessRule_CheckWithContext_OnCacheHit_ThrowsResultWithoutDetails()
    {
        const string groupCode = "GROUP";
        IBusinessRule group = RuleGroup.All(groupCode, groupCode, RuleTestHelper.CreateBrokenRule(FirstCode));
        IBusinessRule cached = group.Cached(TimeSpan.FromMinutes(1));
        BusinessRuleContext context = RuleTestHelper.CreateContext();
        Assert.True(cached.IsBroken(context));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => cached.Check(context));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(groupCode, result.Code);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void BusinessRuleChecker_CheckAll_WithCachedGroup_SurfacesMemberDetails()
    {
        const string groupCode = "GROUP";
        IBusinessRule cached = RuleGroup.All(groupCode, groupCode, RuleTestHelper.CreateBrokenRule(FirstCode))
            .Cached(TimeSpan.FromMinutes(1));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(RuleTestHelper.CreateContext(), cached));

        Assert.Equal(FirstCode, Assert.Single(Assert.Single(exception.BrokenRules).Details).Code);
    }

    [Fact]
    public void ConditionalBusinessRule_ImplementsIDecorated_ExposingInnerRule()
    {
        IBusinessRule inner = RuleTestHelper.CreatePassingRule(FirstCode);

        IBusinessRule conditional = inner.When(() => true);

        IDecoratedBusinessRule decorated = Assert.IsAssignableFrom<IDecoratedBusinessRule>(conditional);
        Assert.Same(inner, decorated.InnerRule);
    }

    [Fact]
    public void AsyncToSyncRuleAdapter_ExposesInnerRule()
    {
        IAsyncBusinessRule inner = RuleTestHelper.CreatePassingAsyncRule(FirstCode);

        AsyncToSyncRuleAdapter adapter = new AsyncToSyncRuleAdapter(inner);

        Assert.Same(inner, adapter.InnerRule);
    }

    [Fact]
    public void BusinessRuleChecker_CheckAll_WithComposedRule_EvaluatesOperandsOnce()
    {
        int evaluations = 0;
        IBusinessRule composed = CreateCountingRule(FirstCode, broken: true, () => evaluations++)
            .And(CreateCountingRule(SecondCode, broken: true, () => evaluations++));

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(composed));

        Assert.Equal(2, evaluations);
    }

    private static IBusinessRule CreateContextReadingRule(string code)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(ctx => ctx.Get<bool>(FlagKey))
            .Build();
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

    private sealed class OverloadRecordingRule : BaseBusinessRule
    {
        public bool? LastCallHadContext { get; private set; }

        public override string Code => "OVERLOAD";

        public override string Message => "Overload recording rule";

        public override bool IsBroken()
        {
            LastCallHadContext = false;
            return true;
        }

        public override bool IsBroken(BusinessRuleContext context)
        {
            LastCallHadContext = true;
            return true;
        }
    }
}
