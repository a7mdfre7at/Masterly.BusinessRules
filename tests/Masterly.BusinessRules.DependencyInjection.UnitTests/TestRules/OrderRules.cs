namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

/// <summary>
/// The codes of the rules scanning finds in this assembly.
/// </summary>
public static class TestRuleCodes
{
    public const string NoItems = "ORDER.NO_ITEMS";
    public const string NegativeTotal = "ORDER.NEGATIVE_TOTAL";
    public const string TooLarge = "ORDER.TOO_LARGE";
    public const string Audit = "ORDER.AUDIT";
    public const string TooManyItems = "ORDER.TOO_MANY_ITEMS";
    public const string Internal = "ORDER.INTERNAL";
    public const string ContextFlag = "CONTEXT.FLAG";
    public const string ContextFlagAsync = "CONTEXT.FLAG_ASYNC";
    public const string FlagKey = "flag";
    public const int MaxItems = 100;
}

public sealed class OrderMustHaveItemsRule : BaseBusinessRule<Order>
{
    public override string Code => TestRuleCodes.NoItems;

    public override string Message => "Order must contain at least one item.";

    public override bool IsBroken(Order order) => order.ItemCount == 0;
}

public sealed class OrderTotalMustBePositiveRule : BaseBusinessRule<Order>
{
    public override string Code => TestRuleCodes.NegativeTotal;

    public override string Message => "Order total must not be negative.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override bool IsBroken(Order order) => order.Total < 0m;
}

public sealed class OrderLimitRule : BaseBusinessRule<Order>
{
    private readonly IOrderLimitSource _limits;

    public OrderLimitRule(IOrderLimitSource limits)
    {
        _limits = limits;
    }

    public override string Code => TestRuleCodes.TooLarge;

    public override string Message => "Order total exceeds the limit.";

    public override bool IsBroken(Order order) => order.Total > _limits.Limit;
}

public sealed class OrderMustNotExceedItemLimitAsyncRule : BaseAsyncBusinessRule<Order>
{
    public override string Code => TestRuleCodes.TooManyItems;

    public override string Message => "Order has too many items.";

    public override Task<bool> IsBrokenAsync(Order order, BusinessRuleContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(order.ItemCount > TestRuleCodes.MaxItems);
    }
}

/// <summary>
/// Implements both typed interfaces so scanning registers it twice (once per interface); never broken.
/// </summary>
public sealed class OrderAuditRule : IBusinessRule<Order>, IAsyncBusinessRule<Order>
{
    public string Code => TestRuleCodes.Audit;

    public string Message => "Audit never blocks.";

    public bool IsBroken(Order subject) => false;

    public bool IsBroken(Order subject, BusinessRuleContext context) => false;

    public BusinessRuleResult Evaluate(Order subject) => new BusinessRuleResult(Code, Message, RuleSeverity.Info);

    public Task<bool> IsBrokenAsync(Order subject, BusinessRuleContext context, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<BusinessRuleResult?> EvaluateAsync(Order subject, BusinessRuleContext context, CancellationToken cancellationToken = default) => Task.FromResult<BusinessRuleResult?>(null);
}

/// <summary>
/// An untyped rule the container can construct: broken when the context flag is true, never broken without a context.
/// </summary>
public sealed class ContextFlagRule : BaseBusinessRule
{
    public override string Code => TestRuleCodes.ContextFlag;

    public override string Message => "The context flag is raised.";

    public override bool IsBroken() => false;

    public override bool IsBroken(BusinessRuleContext context) => context.TryGet(TestRuleCodes.FlagKey, out bool flag) && flag;
}

public sealed class ContextFlagAsyncRule : BaseAsyncBusinessRule
{
    public override string Code => TestRuleCodes.ContextFlagAsync;

    public override string Message => "The context flag is raised.";

    public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(context.TryGet(TestRuleCodes.FlagKey, out bool flag) && flag);
    }
}

/// <summary>
/// Abstract: scanning must skip it.
/// </summary>
public abstract class AbstractOrderRule : BaseBusinessRule<Order>
{
    public override string Code => "ORDER.ABSTRACT";

    public override string Message => "Abstract rules are never registered.";
}

/// <summary>
/// Open generic: scanning must skip it.
/// </summary>
public sealed class AlwaysPassingRule<TSubject> : BaseBusinessRule<TSubject>
{
    public override string Code => "GENERIC.PASS";

    public override string Message => "Open generic rules are never registered.";

    public override bool IsBroken(TSubject subject) => false;
}

/// <summary>
/// Internal: registered only with <c>IncludeNonPublicTypes</c>.
/// </summary>
internal sealed class InternalOrderRule : BaseBusinessRule<Order>
{
    public override string Code => TestRuleCodes.Internal;

    public override string Message => "Internal rules are opt-in.";

    public override bool IsBroken(Order order) => false;
}
