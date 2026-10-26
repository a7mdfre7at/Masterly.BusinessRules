namespace Masterly.BusinessRules.UnitTests;

public class RuleExplanationTests
{
    private const string RootCode = "ROOT";
    private const string ChildCode = "CHILD";

    [Fact]
    public void Constructor_SyncRule_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RuleExplanation((IBusinessRule)null!, RuleOutcome.Passed, false, string.Empty, string.Empty, TimeSpan.Zero, null, null));
    }

    [Fact]
    public void Constructor_AsyncRule_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RuleExplanation((IAsyncBusinessRule)null!, RuleOutcome.Passed, false, string.Empty, string.Empty, TimeSpan.Zero, null, null));
    }

    [Fact]
    public void Constructor_SyncRule_CapturesRuleMetadata()
    {
        IBusinessRule rule = RuleTestHelper.CreateBrokenRule(RootCode, "Root message");

        RuleExplanation explanation = new RuleExplanation(rule, RuleOutcome.Broken, true, string.Empty, string.Empty, TimeSpan.Zero, null, null);

        Assert.Same(rule, explanation.Rule);
        Assert.Null(explanation.AsyncRule);
        Assert.Equal(RootCode, explanation.Code);
        Assert.Equal("Root message", explanation.Message);
        Assert.Equal(RuleSeverity.Error, explanation.Severity);
        Assert.Empty(explanation.Children);
    }

    [Fact]
    public void Constructor_AsyncRule_CapturesRuleMetadata()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreateBrokenAsyncRule(RootCode, "Root message");

        RuleExplanation explanation = new RuleExplanation(rule, RuleOutcome.Broken, true, string.Empty, string.Empty, TimeSpan.Zero, null, null);

        Assert.Same(rule, explanation.AsyncRule);
        Assert.Null(explanation.Rule);
        Assert.Equal(RootCode, explanation.Code);
    }

    [Fact]
    public void Constructor_NullChildren_ChildrenIsEmpty()
    {
        RuleExplanation explanation = CreateLeaf(RootCode, RuleOutcome.Passed, isBroken: false);

        Assert.NotNull(explanation.Children);
        Assert.Empty(explanation.Children);
    }

    [Fact]
    public void ToText_LeafPassed_RendersOutcomeAndCode()
    {
        RuleExplanation explanation = CreateLeaf(RootCode, RuleOutcome.Passed, isBroken: false);

        string text = explanation.ToText();

        Assert.StartsWith("[PASSED]  ROOT", text);
        Assert.DoesNotContain("\"", text);
    }

    [Fact]
    public void ToText_LeafBroken_RendersQuotedMessage()
    {
        RuleExplanation explanation = CreateLeaf(RootCode, RuleOutcome.Broken, isBroken: true);

        string text = explanation.ToText();

        Assert.Contains("[BROKEN]  ROOT  \"ROOT\"", text);
        Assert.Contains(" ms)", text);
    }

    [Fact]
    public void ToText_NotEvaluated_RendersReasonWithoutDuration()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule(RootCode);
        RuleExplanation explanation = new RuleExplanation(rule, RuleOutcome.NotEvaluated, false, string.Empty, "short-circuit", TimeSpan.Zero, null, null);

        string text = explanation.ToText();

        Assert.Equal("[NOT EVALUATED]  ROOT  reason: short-circuit", text);
    }

    [Fact]
    public void ToText_Tree_RendersBranchesAndOperator()
    {
        RuleExplanation passed = CreateLeaf("A", RuleOutcome.Passed, isBroken: false);
        RuleExplanation broken = CreateLeaf("B", RuleOutcome.Broken, isBroken: true);
        IBusinessRule root = RuleTestHelper.CreateBrokenRule(RootCode);
        RuleExplanation tree = new RuleExplanation(root, RuleOutcome.Broken, true, "ALL", string.Empty, TimeSpan.FromMilliseconds(1.5), null, new[] { passed, broken });

        string[] lines = tree.ToText().Split(Environment.NewLine);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("[BROKEN] ALL  ROOT", lines[0]);
        Assert.Contains("(1.5 ms)", lines[0]);
        Assert.StartsWith(" ├─ [PASSED]  A", lines[1]);
        Assert.StartsWith(" └─ [BROKEN]  B", lines[2]);
    }

    [Fact]
    public void ToText_NestedTree_IndentsGrandchildren()
    {
        RuleExplanation grandchild = CreateLeaf("C", RuleOutcome.Broken, isBroken: true);
        RuleExplanation child = new RuleExplanation(RuleTestHelper.CreateBrokenRule(ChildCode), RuleOutcome.Broken, true, "NOT", string.Empty, TimeSpan.Zero, null, new[] { grandchild });
        RuleExplanation leaf = CreateLeaf("D", RuleOutcome.Passed, isBroken: false);
        RuleExplanation tree = new RuleExplanation(RuleTestHelper.CreateBrokenRule(RootCode), RuleOutcome.Broken, true, "ALL", string.Empty, TimeSpan.Zero, null, new[] { child, leaf });

        string[] lines = tree.ToText().Split(Environment.NewLine);

        Assert.StartsWith(" ├─ [BROKEN] NOT  CHILD", lines[1]);
        Assert.StartsWith(" │   └─ [BROKEN]  C", lines[2]);
        Assert.StartsWith(" └─ [PASSED]  D", lines[3]);
    }

    [Fact]
    public void ToString_ReturnsToText()
    {
        RuleExplanation explanation = CreateLeaf(RootCode, RuleOutcome.Passed, isBroken: false);

        Assert.Equal(explanation.ToText(), explanation.ToString());
    }

    [Fact]
    public void BrokenLeaves_NodeNotBroken_ReturnsEmpty()
    {
        RuleExplanation explanation = CreateLeaf(RootCode, RuleOutcome.Passed, isBroken: false);

        Assert.Empty(explanation.BrokenLeaves());
    }

    [Fact]
    public void BrokenLeaves_BrokenLeaf_ReturnsItself()
    {
        RuleExplanation explanation = CreateLeaf(RootCode, RuleOutcome.Broken, isBroken: true);

        RuleExplanation only = Assert.Single(explanation.BrokenLeaves());
        Assert.Same(explanation, only);
    }

    [Fact]
    public void BrokenLeaves_BrokenParentWithBrokenChildren_ReturnsDeepestBrokenNodes()
    {
        RuleExplanation passed = CreateLeaf("A", RuleOutcome.Passed, isBroken: false);
        RuleExplanation brokenB = CreateLeaf("B", RuleOutcome.Broken, isBroken: true);
        RuleExplanation brokenC = CreateLeaf("C", RuleOutcome.Broken, isBroken: true);
        RuleExplanation inner = new RuleExplanation(RuleTestHelper.CreateBrokenRule(ChildCode), RuleOutcome.Broken, true, "ALL", string.Empty, TimeSpan.Zero, null, new[] { brokenC });
        RuleExplanation root = new RuleExplanation(RuleTestHelper.CreateBrokenRule(RootCode), RuleOutcome.Broken, true, "ALL", string.Empty, TimeSpan.Zero, null, new[] { passed, brokenB, inner });

        List<RuleExplanation> leaves = root.BrokenLeaves().ToList();

        Assert.Equal(new[] { "B", "C" }, leaves.Select(leaf => leaf.Code));
    }

    [Fact]
    public void BrokenLeaves_BrokenParentWithoutBrokenChildren_ReturnsParent()
    {
        RuleExplanation passed = CreateLeaf("A", RuleOutcome.Passed, isBroken: false);
        RuleExplanation root = new RuleExplanation(RuleTestHelper.CreateBrokenRule(RootCode), RuleOutcome.Broken, true, "NOT", string.Empty, TimeSpan.Zero, null, new[] { passed });

        RuleExplanation only = Assert.Single(root.BrokenLeaves());
        Assert.Same(root, only);
    }

    private static RuleExplanation CreateLeaf(string code, RuleOutcome outcome, bool isBroken)
    {
        IBusinessRule rule = isBroken ? RuleTestHelper.CreateBrokenRule(code, code) : RuleTestHelper.CreatePassingRule(code, code);
        return new RuleExplanation(rule, outcome, isBroken, string.Empty, string.Empty, TimeSpan.Zero, null, null);
    }
}
