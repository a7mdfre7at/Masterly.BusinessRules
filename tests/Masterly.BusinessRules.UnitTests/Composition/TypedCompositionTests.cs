namespace Masterly.BusinessRules.UnitTests;

public class TypedCompositionTests
{
    private const string FlagKey = "flag";

    // ---------------------------------------------------------------------
    // Truth tables
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void And_IsBrokenOnlyWhenBothOperandsAreBroken(bool firstBroken, bool secondBroken, bool expected)
    {
        IBusinessRule<int> composed = Rule("A", firstBroken).And(Rule("B", secondBroken));

        Assert.Equal(expected, composed.IsBroken(0));
        Assert.Equal(expected, composed.IsBroken(0, new BusinessRuleContext()));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Or_IsBrokenWhenEitherOperandIsBroken(bool firstBroken, bool secondBroken, bool expected)
    {
        IBusinessRule<int> composed = Rule("A", firstBroken).Or(Rule("B", secondBroken));

        Assert.Equal(expected, composed.IsBroken(0));
        Assert.Equal(expected, composed.IsBroken(0, new BusinessRuleContext()));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Not_InvertsTheOperand(bool operandBroken, bool expected)
    {
        IBusinessRule<int> composed = Rule("A", operandBroken).Not();

        Assert.Equal(expected, composed.IsBroken(0));
        Assert.Equal(expected, composed.IsBroken(0, new BusinessRuleContext()));
    }

    // ---------------------------------------------------------------------
    // Subject and context forwarding
    // ---------------------------------------------------------------------

    [Fact]
    public void And_ForwardsTheSubjectToBothOperands()
    {
        List<int> seen = new List<int>();
        IBusinessRule<int> composed = Recording("A", seen).And(Recording("B", seen));

        composed.IsBroken(42);

        Assert.Equal(new[] { 42, 42 }, seen);
    }

    [Fact]
    public void Or_ForwardsTheSubjectToBothOperands()
    {
        List<int> seen = new List<int>();
        IBusinessRule<int> composed = Passing("A", seen).Or(Passing("B", seen));

        composed.IsBroken(7);

        Assert.Equal(new[] { 7, 7 }, seen);
    }

    [Fact]
    public void Not_ForwardsTheSubjectToTheOperand()
    {
        List<int> seen = new List<int>();
        IBusinessRule<int> composed = Recording("A", seen).Not();

        composed.IsBroken(9);

        Assert.Equal(new[] { 9 }, seen);
    }

    [Fact]
    public void And_ForwardsTheContextToBothOperands()
    {
        IBusinessRule<int> composed = ContextRule("A").And(ContextRule("B"));

        Assert.True(composed.IsBroken(0, RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.False(composed.IsBroken(0, RuleTestHelper.CreateContext((FlagKey, false))));
    }

    [Fact]
    public void Or_ForwardsTheContextToBothOperands()
    {
        IBusinessRule<int> composed = Rule("A", false).Or(ContextRule("B"));

        Assert.True(composed.IsBroken(0, RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.False(composed.IsBroken(0, RuleTestHelper.CreateContext((FlagKey, false))));
    }

    [Fact]
    public void Not_ForwardsTheContextToTheOperand()
    {
        IBusinessRule<int> composed = ContextRule("A").Not();

        Assert.False(composed.IsBroken(0, RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.True(composed.IsBroken(0, RuleTestHelper.CreateContext((FlagKey, false))));
    }

    [Fact]
    public void IsBroken_WithoutContext_UsesTheSubjectOnlyOverloadOfOperands()
    {
        bool contextOverloadCalled = false;
        IBusinessRule<int> operand = new OverloadRecordingRule(() => contextOverloadCalled = true);

        operand.And(Rule("B", true)).IsBroken(1);

        Assert.False(contextOverloadCalled);
    }

    // ---------------------------------------------------------------------
    // Short-circuit
    // ---------------------------------------------------------------------

    [Fact]
    public void And_WhenFirstPasses_DoesNotEvaluateSecond()
    {
        int secondCalls = 0;
        IBusinessRule<int> composed = Rule("A", false).And(Counting("B", true, () => secondCalls++));

        Assert.False(composed.IsBroken(0));
        Assert.Equal(0, secondCalls);
    }

    [Fact]
    public void And_WhenFirstBroken_EvaluatesSecond()
    {
        int secondCalls = 0;
        IBusinessRule<int> composed = Rule("A", true).And(Counting("B", false, () => secondCalls++));

        Assert.False(composed.IsBroken(0));
        Assert.Equal(1, secondCalls);
    }

    [Fact]
    public void Or_WhenFirstBroken_DoesNotEvaluateSecond()
    {
        int secondCalls = 0;
        IBusinessRule<int> composed = Rule("A", true).Or(Counting("B", false, () => secondCalls++));

        Assert.True(composed.IsBroken(0));
        Assert.Equal(0, secondCalls);
    }

    [Fact]
    public void Or_WhenFirstPasses_EvaluatesSecond()
    {
        int secondCalls = 0;
        IBusinessRule<int> composed = Rule("A", false).Or(Counting("B", true, () => secondCalls++));

        Assert.True(composed.IsBroken(0));
        Assert.Equal(1, secondCalls);
    }

    // ---------------------------------------------------------------------
    // Metadata and structure
    // ---------------------------------------------------------------------

    [Fact]
    public void And_SynthesisesCodeMessageAndMetadata()
    {
        IBusinessRule<int> first = Rule("A", true);
        IBusinessRule<int> second = Rule("B", true);

        IBusinessRule<int> composed = first.And(second);

        AndBusinessRule<int> node = Assert.IsType<AndBusinessRule<int>>(composed);
        Assert.Equal("A+B", node.Code);
        Assert.Equal("Both rules must be satisfied: msg A AND msg B", node.Message);
        Assert.Equal(RuleSeverity.Error, node.Severity);
        Assert.Equal("AndBusinessRule", node.Name);
        Assert.Equal("Broken only when both operands are broken.", node.Description);
        Assert.Equal(string.Empty, node.Category);
        Assert.Empty(node.Tags);
        Assert.Equal(RuleOperator.And, node.Operator);
        Assert.Equal(new[] { first, second }, node.Operands);
    }

    [Fact]
    public void Or_SynthesisesCodeMessageAndMetadata()
    {
        IBusinessRule<int> composed = Rule("A", true).Or(Rule("B", true));

        OrBusinessRule<int> node = Assert.IsType<OrBusinessRule<int>>(composed);
        Assert.Equal("A|B", node.Code);
        Assert.Equal("At least one rule must be satisfied: msg A OR msg B", node.Message);
        Assert.Equal("OrBusinessRule", node.Name);
        Assert.Equal("Broken when either operand is broken.", node.Description);
        Assert.Equal(RuleOperator.Or, node.Operator);
        Assert.Equal(2, node.Operands.Count);
    }

    [Fact]
    public void Not_SynthesisesCodeMessageAndMetadata()
    {
        IBusinessRule<int> operand = Rule("A", true);

        IBusinessRule<int> composed = operand.Not();

        NotBusinessRule<int> node = Assert.IsType<NotBusinessRule<int>>(composed);
        Assert.Equal("!A", node.Code);
        Assert.Equal("NOT: msg A", node.Message);
        Assert.Equal("NotBusinessRule", node.Name);
        Assert.Equal("Broken when the operand passes.", node.Description);
        Assert.Equal(RuleOperator.Not, node.Operator);
        Assert.Same(operand, Assert.Single(node.Operands));
    }

    [Fact]
    public void TypedNodes_ReportTheSameCodesAsTheNonGenericNodes()
    {
        IBusinessRule<int> typed = Rule("A", true).And(Rule("B", true)).Or(Rule("C", true).Not());
        IBusinessRule plain = RuleTestHelper.CreateBrokenRule("A").And(RuleTestHelper.CreateBrokenRule("B")).Or(RuleTestHelper.CreateBrokenRule("C").Not());

        Assert.Equal(plain.Code, typed.Code);
    }

    [Fact]
    public void Evaluate_IsAPureFactoryCarryingTheNodeMetadata()
    {
        int calls = 0;
        IBusinessRule<int> composed = Counting("A", true, () => calls++).And(Counting("B", true, () => calls++));

        BusinessRuleResult result = composed.Evaluate(1);
        BusinessRuleResult contextResult = composed.Evaluate(1, new BusinessRuleContext());

        Assert.Equal(0, calls);
        Assert.Equal("A+B", result.Code);
        Assert.Equal("A+B", contextResult.Code);
        Assert.Equal("AndBusinessRule", result.Name);
    }

    [Fact]
    public void Operators_NullOperands_ThrowArgumentNullException()
    {
        IBusinessRule<int> rule = Rule("A", true);
        IBusinessRule<int> missing = null!;

        Assert.Throws<ArgumentNullException>(() => rule.And(missing));
        Assert.Throws<ArgumentNullException>(() => missing.And(rule));
        Assert.Throws<ArgumentNullException>(() => rule.Or(missing));
        Assert.Throws<ArgumentNullException>(() => missing.Or(rule));
        Assert.Throws<ArgumentNullException>(() => missing.Not());
    }

    // ---------------------------------------------------------------------
    // Chaining, binding and checking
    // ---------------------------------------------------------------------

    [Fact]
    public void Operators_Chain()
    {
        IBusinessRule<int> composed = Rule("A", true).And(Rule("B", true)).Or(Rule("C", false)).Not();

        Assert.False(composed.IsBroken(0));
        Assert.Equal("!A+B|C", composed.Code);
    }

    [Fact]
    public void Composed_BoundToSubject_RunsThroughCheckAndChecker()
    {
        IBusinessRule<int> composed = Threshold("BIG", 10).And(Threshold("HUGE", 100));

        Assert.Throws<BusinessRuleValidationException>(() => composed.For(500).Check());
        composed.For(50).Check();
        Assert.Equal("BIG+HUGE", Assert.Single(BusinessRuleChecker.EvaluateAll(500, new[] { composed }).BrokenRules).Code);
    }

    [Fact]
    public void Composed_WorksWithContravariantOperands()
    {
        IBusinessRule<object> anyObject = BusinessRuleBuilder<object>.Create("OBJ").WithMessage("m").When(value => value == null).Build();
        IBusinessRule<string> typed = StringRule("S", true);

        IBusinessRule<string> composed = typed.And(anyObject);

        Assert.False(composed.IsBroken("text"));
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IBusinessRule<int> Rule(string code, bool broken)
    {
        return BusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").When(_ => broken).Build();
    }

    private static IBusinessRule<string> StringRule(string code, bool broken)
    {
        return BusinessRuleBuilder<string>.Create(code).WithMessage($"msg {code}").When(_ => broken).Build();
    }

    private static IBusinessRule<int> Counting(string code, bool broken, Action onEvaluate)
    {
        return BusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").When(_ => { onEvaluate(); return broken; }).Build();
    }

    private static IBusinessRule<int> Recording(string code, List<int> seen)
    {
        return BusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").When(subject => { seen.Add(subject); return true; }).Build();
    }

    private static IBusinessRule<int> Passing(string code, List<int> seen)
    {
        return BusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").When(subject => { seen.Add(subject); return false; }).Build();
    }

    private static IBusinessRule<int> ContextRule(string code)
    {
        return BusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").When((_, ctx) => ctx.Get<bool>(FlagKey)).Build();
    }

    private static IBusinessRule<int> Threshold(string code, int limit)
    {
        return BusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").When(value => value > limit).Build();
    }

    private sealed class OverloadRecordingRule : BaseBusinessRule<int>
    {
        private readonly Action _onContextOverload;
        public OverloadRecordingRule(Action onContextOverload) => _onContextOverload = onContextOverload;
        public override string Code => "REC";
        public override string Message => "rec";
        public override bool IsBroken(int subject) => true;

        public override bool IsBroken(int subject, BusinessRuleContext context)
        {
            _onContextOverload();
            return true;
        }
    }
}
