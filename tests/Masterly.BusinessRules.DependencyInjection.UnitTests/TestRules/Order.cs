namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

/// <summary>
/// The subject the scanned test rules validate.
/// </summary>
public sealed class Order
{
    public Order(int itemCount, decimal total)
    {
        ItemCount = itemCount;
        Total = total;
    }

    public int ItemCount { get; }

    public decimal Total { get; }
}

/// <summary>
/// A dependency injected into <see cref="OrderLimitRule"/>, so scanning registers a rule with a constructor parameter.
/// </summary>
public interface IOrderLimitSource
{
    decimal Limit { get; }
}

public sealed class FixedOrderLimitSource : IOrderLimitSource
{
    public FixedOrderLimitSource(decimal limit)
    {
        Limit = limit;
    }

    public decimal Limit { get; }
}
