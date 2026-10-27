using System.Globalization;
using System.Reflection;

namespace Masterly.BusinessRules.Expressions.UnitTests;

public class RuleExpressionOptionsTests
{
    [Fact]
    public void Default_HasDocumentedValues()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default;

        Assert.Equal(RuleExpressionOptions.DefaultMaxLength, options.MaxLength);
        Assert.Equal(RuleExpressionOptions.DefaultMaxDepth, options.MaxDepth);
        Assert.Null(options.MemberFilter);
        Assert.False(options.RequireRuleAccessibleAttribute);
        Assert.Same(CultureInfo.InvariantCulture, options.Culture);
    }

    [Fact]
    public void Default_IsASingleton()
    {
        Assert.Same(RuleExpressionOptions.Default, RuleExpressionOptions.Default);
    }

    [Fact]
    public void WithMaxLength_ReturnsCopy_LeavingDefaultUntouched()
    {
        RuleExpressionOptions derived = RuleExpressionOptions.Default.WithMaxLength(10);

        Assert.Equal(10, derived.MaxLength);
        Assert.Equal(RuleExpressionOptions.DefaultMaxLength, RuleExpressionOptions.Default.MaxLength);
        Assert.NotSame(RuleExpressionOptions.Default, derived);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithMaxLength_LessThanOne_ThrowsArgumentOutOfRangeException(int maxLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleExpressionOptions.Default.WithMaxLength(maxLength));
    }

    [Fact]
    public void WithMaxDepth_ReturnsCopy()
    {
        RuleExpressionOptions derived = RuleExpressionOptions.Default.WithMaxDepth(3);

        Assert.Equal(3, derived.MaxDepth);
        Assert.Equal(RuleExpressionOptions.DefaultMaxLength, derived.MaxLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void WithMaxDepth_LessThanOne_ThrowsArgumentOutOfRangeException(int maxDepth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleExpressionOptions.Default.WithMaxDepth(maxDepth));
    }

    [Fact]
    public void WithMemberFilter_ReturnsCopy_AndNullClearsIt()
    {
        Func<PropertyInfo, bool> filter = property => property.Name != "Ssn";

        RuleExpressionOptions derived = RuleExpressionOptions.Default.WithMemberFilter(filter);
        RuleExpressionOptions cleared = derived.WithMemberFilter(null);

        Assert.Same(filter, derived.MemberFilter);
        Assert.Null(cleared.MemberFilter);
    }

    [Fact]
    public void WithRequireRuleAccessibleAttribute_ReturnsCopy()
    {
        RuleExpressionOptions derived = RuleExpressionOptions.Default.WithRequireRuleAccessibleAttribute(true);

        Assert.True(derived.RequireRuleAccessibleAttribute);
        Assert.False(RuleExpressionOptions.Default.RequireRuleAccessibleAttribute);
    }

    [Fact]
    public void WithCulture_ReturnsCopy()
    {
        CultureInfo german = CultureInfo.GetCultureInfo("de-DE");

        RuleExpressionOptions derived = RuleExpressionOptions.Default.WithCulture(german);

        Assert.Same(german, derived.Culture);
    }

    [Fact]
    public void WithCulture_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleExpressionOptions.Default.WithCulture(null!));
    }

    [Fact]
    public void With_Chained_KeepsEverySetting()
    {
        CultureInfo german = CultureInfo.GetCultureInfo("de-DE");
        Func<PropertyInfo, bool> filter = _ => true;

        RuleExpressionOptions derived = RuleExpressionOptions.Default
            .WithMaxLength(20)
            .WithMaxDepth(5)
            .WithMemberFilter(filter)
            .WithRequireRuleAccessibleAttribute(true)
            .WithCulture(german);

        Assert.Equal(20, derived.MaxLength);
        Assert.Equal(5, derived.MaxDepth);
        Assert.Same(filter, derived.MemberFilter);
        Assert.True(derived.RequireRuleAccessibleAttribute);
        Assert.Same(german, derived.Culture);
    }

    [Fact]
    public void RuleAccessibleAttribute_TargetsPropertiesAndTypes()
    {
        AttributeUsageAttribute usage = typeof(RuleAccessibleAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Property));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Struct));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Interface));
        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }
}
