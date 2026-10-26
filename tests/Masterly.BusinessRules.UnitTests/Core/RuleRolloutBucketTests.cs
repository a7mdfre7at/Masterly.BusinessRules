namespace Masterly.BusinessRules.UnitTests;

public class RuleRolloutBucketTests
{
    private const string Flag = "credit-score-v2";

    [Theory]
    [InlineData("", 0x811c9dc5u)]
    [InlineData("a", 0xe40c292cu)]
    [InlineData("foobar", 0xbf9cf968u)]
    public void Hash_KnownFnv1aVectors_Match(string text, uint expected)
    {
        Assert.Equal(expected, RuleRolloutBucket.Hash(text));
    }

    [Fact]
    public void Hash_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleRolloutBucket.Hash(null!));
    }

    [Fact]
    public void Compute_IsHashOfFlagSeparatorKeyModuloBucketCount()
    {
        int bucket = RuleRolloutBucket.Compute(Flag, "customer-42");

        Assert.Equal((int)(RuleRolloutBucket.Hash($"{Flag}{RuleRolloutBucket.Separator}customer-42") % RuleRolloutBucket.BucketCount), bucket);
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        Assert.Equal(RuleRolloutBucket.Compute(Flag, "customer-1"), RuleRolloutBucket.Compute(Flag, "customer-1"));
    }

    [Fact]
    public void Compute_AlwaysWithinZeroToNinetyNine()
    {
        IEnumerable<int> buckets = Enumerable.Range(0, 1000).Select(index => RuleRolloutBucket.Compute(Flag, $"customer-{index}"));

        Assert.All(buckets, bucket => Assert.InRange(bucket, 0, RuleRolloutBucket.BucketCount - 1));
    }

    [Fact]
    public void Compute_SpreadsKeysOverManyBuckets()
    {
        IEnumerable<int> distinct = Enumerable.Range(0, 1000).Select(index => RuleRolloutBucket.Compute(Flag, $"customer-{index}")).Distinct();

        Assert.True(distinct.Count() > 90);
    }

    [Fact]
    public void Compute_DifferentFlags_GiveDifferentCohortsForSomeKey()
    {
        Assert.Contains(Enumerable.Range(0, 50), index => RuleRolloutBucket.Compute("flag-a", $"customer-{index}") != RuleRolloutBucket.Compute("flag-b", $"customer-{index}"));
    }

    [Fact]
    public void Compute_NullFlag_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleRolloutBucket.Compute(null!, "key"));
    }

    [Fact]
    public void Compute_NullStableKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleRolloutBucket.Compute(Flag, null!));
    }

    [Fact]
    public void BucketCount_IsOneHundred()
    {
        Assert.Equal(100, RuleRolloutBucket.BucketCount);
    }
}
