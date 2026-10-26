namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleResultDetailsTests
{
    private const string GroupCode = "GROUP";
    private const string MemberCode = "MEMBER";
    private const string LeafCode = "LEAF";

    [Fact]
    public void Constructor_WithoutDetails_DetailsIsEmpty()
    {
        BusinessRuleResult result = new BusinessRuleResult(LeafCode, "Leaf", RuleSeverity.Error);

        Assert.NotNull(result.Details);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Constructor_WithDetails_ExposesDetailsInOrder()
    {
        BusinessRuleResult first = new BusinessRuleResult("A", "A", RuleSeverity.Error);
        BusinessRuleResult second = new BusinessRuleResult("B", "B", RuleSeverity.Warning);

        BusinessRuleResult result = new BusinessRuleResult(GroupCode, "Group", RuleSeverity.Error, new[] { first, second });

        Assert.Equal(2, result.Details.Count);
        Assert.Same(first, result.Details[0]);
        Assert.Same(second, result.Details[1]);
    }

    [Fact]
    public void Constructor_WithDetails_CopiesTheSource()
    {
        List<BusinessRuleResult> source = new List<BusinessRuleResult> { new BusinessRuleResult("A", "A", RuleSeverity.Error) };
        BusinessRuleResult result = new BusinessRuleResult(GroupCode, "Group", RuleSeverity.Error, source);

        source.Add(new BusinessRuleResult("B", "B", RuleSeverity.Error));

        Assert.Single(result.Details);
    }

    [Fact]
    public void Constructor_WithNullDetails_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessRuleResult(GroupCode, "Group", RuleSeverity.Error, null!));
    }

    [Fact]
    public void Constructor_WithEmptyDetails_DetailsIsEmpty()
    {
        BusinessRuleResult result = new BusinessRuleResult(GroupCode, "Group", RuleSeverity.Error, Array.Empty<BusinessRuleResult>());

        Assert.Empty(result.Details);
    }

    [Fact]
    public void Flatten_LeafResult_ReturnsItself()
    {
        BusinessRuleResult leaf = new BusinessRuleResult(LeafCode, "Leaf", RuleSeverity.Error);

        List<BusinessRuleResult> leaves = leaf.Flatten().ToList();

        BusinessRuleResult only = Assert.Single(leaves);
        Assert.Same(leaf, only);
    }

    [Fact]
    public void Flatten_NestedResult_ReturnsLeavesInEvaluationOrder()
    {
        BusinessRuleResult leafA = new BusinessRuleResult("A", "A", RuleSeverity.Error);
        BusinessRuleResult leafB = new BusinessRuleResult("B", "B", RuleSeverity.Error);
        BusinessRuleResult leafC = new BusinessRuleResult("C", "C", RuleSeverity.Error);
        BusinessRuleResult inner = new BusinessRuleResult(MemberCode, "Member", RuleSeverity.Error, new[] { leafB, leafC });
        BusinessRuleResult outer = new BusinessRuleResult(GroupCode, "Group", RuleSeverity.Error, new[] { leafA, inner });

        List<BusinessRuleResult> leaves = outer.Flatten().ToList();

        Assert.Equal(new[] { "A", "B", "C" }, leaves.Select(leaf => leaf.Code));
        Assert.DoesNotContain(leaves, leaf => leaf.Code == GroupCode || leaf.Code == MemberCode);
    }
}
