using System.Linq.Expressions;

namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// The subject every specification test queries: a deliberately plain POCO with the shape a database row would have.
/// </summary>
internal sealed class Order
{
    public int Id { get; set; }

    public decimal Total { get; set; }

    public DateTime DueDate { get; set; }

    public bool IsPaid { get; set; }

    public bool IsFlagged { get; set; }
}

/// <summary>
/// Fixed clock and sample orders shared by the specification tests so that every verdict is deterministic.
/// </summary>
internal static class SpecificationSamples
{
    public static readonly DateTime Today = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);

    public const decimal BigTotal = 1000m;

    /// <summary>Overdue, unpaid, small, not flagged.</summary>
    public static Order OverdueSmall() => new Order { Id = 1, Total = 10m, DueDate = Today.AddDays(-1), IsPaid = false, IsFlagged = false };

    /// <summary>Overdue, unpaid, big, not flagged.</summary>
    public static Order OverdueBig() => new Order { Id = 2, Total = 5000m, DueDate = Today.AddDays(-1), IsPaid = false, IsFlagged = false };

    /// <summary>Not yet due, small, not flagged.</summary>
    public static Order CurrentSmall() => new Order { Id = 3, Total = 10m, DueDate = Today.AddDays(1), IsPaid = false, IsFlagged = false };

    /// <summary>Past due but paid, big, flagged.</summary>
    public static Order PaidBigFlagged() => new Order { Id = 4, Total = 5000m, DueDate = Today.AddDays(-1), IsPaid = true, IsFlagged = true };

    /// <summary>Not yet due, small, flagged.</summary>
    public static Order CurrentSmallFlagged() => new Order { Id = 5, Total = 10m, DueDate = Today.AddDays(1), IsPaid = false, IsFlagged = true };

    public static List<Order> All() => new List<Order> { OverdueSmall(), OverdueBig(), CurrentSmall(), PaidBigFlagged(), CurrentSmallFlagged() };
}

internal sealed class OrderIsOverdueRule : SpecificationBusinessRule<Order>
{
    public const string RuleCode = "ORDER.OVERDUE";
    public const string RuleMessage = "Order is past its due date.";

    public override string Code => RuleCode;

    public override string Message => RuleMessage;

    public override Expression<Func<Order, bool>> BrokenWhen => o => o.DueDate < SpecificationSamples.Today && !o.IsPaid;
}

internal sealed class OrderIsBigRule : SpecificationBusinessRule<Order>
{
    public const string RuleCode = "ORDER.BIG";
    public const string RuleMessage = "Order total exceeds the limit.";

    public override string Code => RuleCode;

    public override string Message => RuleMessage;

    public override Expression<Func<Order, bool>> BrokenWhen => order => order.Total > SpecificationSamples.BigTotal;
}

internal sealed class OrderIsFlaggedRule : SpecificationBusinessRule<Order>
{
    public const string RuleCode = "ORDER.FLAGGED";
    public const string RuleMessage = "Order is flagged for review.";

    public override string Code => RuleCode;

    public override string Message => RuleMessage;

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override string Name => "Flagged";

    public override string Description => "Flags need a manual look";

    public override string Category => "Review";

    public override IReadOnlyList<string> Tags => new[] { "review", "manual" };

    public override Expression<Func<Order, bool>> BrokenWhen => x => x.IsFlagged;

    protected override RuleResultDetails Describe(Order subject, BusinessRuleContext? context)
    {
        return RuleResultDetails.Detail($"Order {subject.Id} is flagged").Actual(subject.Id);
    }
}

/// <summary>
/// Counts how many times <see cref="BrokenWhen"/> is read so tests can prove the expression is cached and compiled once.
/// </summary>
internal sealed class CountingSpecificationRule : SpecificationBusinessRule<int>
{
    public int BrokenWhenReads { get; private set; }

    public override string Code => "COUNT";

    public override string Message => "Counting rule";

    public override Expression<Func<int, bool>> BrokenWhen
    {
        get
        {
            BrokenWhenReads++;
            return value => value < 0;
        }
    }
}

internal sealed class NullBrokenWhenRule : SpecificationBusinessRule<int>
{
    public const string RuleCode = "NULL.BROKEN_WHEN";

    public override string Code => RuleCode;

    public override string Message => "Returns no expression";

    // A contract violation on purpose: the library must report it rather than crash with a NullReferenceException.
    public override Expression<Func<int, bool>> BrokenWhen => null!;
}

/// <summary>
/// A third-party specification whose expressions are supplied directly, used to drive the combinators and the null guards.
/// </summary>
internal sealed class FakeSpecificationRule : ISpecificationRule<Order>
{
    public FakeSpecificationRule(string code, Expression<Func<Order, bool>>? broken, Expression<Func<Order, bool>>? satisfied)
    {
        Code = code;
        BrokenExpression = broken!;
        SatisfiedExpression = satisfied!;
    }

    public string Code { get; }

    public string Message => $"Fake {Code}";

    public Expression<Func<Order, bool>> BrokenExpression { get; }

    public Expression<Func<Order, bool>> SatisfiedExpression { get; }

    public bool IsBroken(Order subject) => BrokenExpression.Compile()(subject);

    public bool IsBroken(Order subject, BusinessRuleContext context) => IsBroken(subject);

    public BusinessRuleResult Evaluate(Order subject) => new BusinessRuleResult(Code, Message, RuleSeverity.Error);
}

/// <summary>
/// Walks an expression tree and records the facts the translation tests assert on: the lambdas it contains, the
/// distinct parameters its bodies reference and whether any <see cref="InvocationExpression"/> is present.
/// </summary>
internal sealed class ExpressionShape : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _parameters = new HashSet<ParameterExpression>();
    private readonly List<LambdaExpression> _lambdas = new List<LambdaExpression>();

    public int InvocationCount { get; private set; }

    public IReadOnlyCollection<ParameterExpression> ReferencedParameters => _parameters;

    public IReadOnlyList<LambdaExpression> Lambdas => _lambdas;

    public static ExpressionShape Of(Expression expression)
    {
        ExpressionShape shape = new ExpressionShape();
        shape.Visit(expression);
        return shape;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        InvocationCount++;
        return base.VisitInvocation(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _parameters.Add(node);
        return base.VisitParameter(node);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _lambdas.Add(node);
        return base.VisitLambda(node);
    }
}
