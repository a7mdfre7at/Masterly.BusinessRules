namespace Masterly.BusinessRules.Expressions.UnitTests;

public enum CustomerTier
{
    Standard = 0,
    Gold = 1,
    Vip = 2,
}

public sealed class Address
{
    public string City { get; set; } = string.Empty;
}

public sealed class Customer
{
    public string Name { get; set; } = string.Empty;

    public CustomerTier Tier { get; set; }

    public Address? Address { get; set; }

    public int? Age { get; set; }

    public string Ssn { get; set; } = string.Empty;
}

public sealed class OrderItem
{
    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

public sealed class Order
{
    public List<OrderItem> Items { get; set; } = new List<OrderItem>();

    public decimal Total { get; set; }

    public Customer? Customer { get; set; }

    public string[] Tags { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Codes { get; set; } = Array.Empty<string>();

    public DateTime PlacedAt { get; set; }

    public bool IsGift { get; set; }
}

public sealed class AnnotatedCustomer
{
    [RuleAccessible]
    public CustomerTier Tier { get; set; }

    public string Ssn { get; set; } = string.Empty;

    public List<string> Notes { get; set; } = new List<string>();
}

[RuleAccessible]
public sealed class AnnotatedOrder
{
    public decimal Total { get; set; }

    public AnnotatedCustomer? Customer { get; set; }
}

public sealed class ThrowingSubject
{
    public const string FailureMessage = "getter failed";

    public int Boom => throw new InvalidOperationException(FailureMessage);
}

public sealed class CaseCollidingSubject
{
    public int Value { get; set; }

    public int VALUE { get; set; }

    public int Other { get; set; }
}

public static class TestOrders
{
    public static Order Standard() => new Order
    {
        Items = new List<OrderItem> { new OrderItem { Sku = "A", Quantity = 2 }, new OrderItem { Sku = "B", Quantity = 1 } },
        Total = 12_000m,
        Customer = new Customer { Name = "Alice", Tier = CustomerTier.Gold, Address = new Address { City = "Amman" }, Age = 34, Ssn = "123-45-6789" },
        Tags = new[] { "rush", "fragile" },
        Codes = new[] { "X1", "X2", "X3" },
        PlacedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        IsGift = false,
    };

    public static Order Empty() => new Order { Items = new List<OrderItem>(), Total = 0m, Customer = null };
}
