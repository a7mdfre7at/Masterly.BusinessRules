namespace Masterly.BusinessRules.Diagnostics.UnitTests.Support;

/// <summary>
/// Rule factories with the metadata the diagnostics tests assert on.
/// </summary>
public static class TestRules
{
    public const string Category = "checkout";
    public const string Boom = "boom";

    public static IBusinessRule Sync(string code, bool broken, RuleSeverity severity = RuleSeverity.Error, string? message = null, Action? onEvaluate = null)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(message ?? $"{code} message")
            .WithName($"{code} name")
            .WithCategory(Category)
            .WithSeverity(severity)
            .When(() =>
            {
                onEvaluate?.Invoke();
                return broken;
            })
            .Build();
    }

    public static IBusinessRule SyncThrowing(string code, Exception? exception = null)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage($"{code} message")
            .WithName($"{code} name")
            .WithCategory(Category)
            .When(() => throw (exception ?? new InvalidOperationException(Boom)))
            .Build();
    }

    public static IAsyncBusinessRule Async(string code, bool broken, RuleSeverity severity = RuleSeverity.Error, string? message = null, Func<Task>? onEvaluate = null)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(message ?? $"{code} message")
            .WithName($"{code} name")
            .WithCategory(Category)
            .WithSeverity(severity)
            .WhenAsync(async (context, cancellationToken) =>
            {
                if (onEvaluate != null)
                {
                    await onEvaluate();
                }

                return broken;
            })
            .Build();
    }

    public static IAsyncBusinessRule AsyncThrowing(string code, Exception? exception = null)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage($"{code} message")
            .WithName($"{code} name")
            .WithCategory(Category)
            .WhenAsync(async (context, cancellationToken) =>
            {
                await Task.Yield();
                throw exception ?? new InvalidOperationException(Boom);
            })
            .Build();
    }

    public static IAsyncBusinessRule AsyncCancellable(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage($"{code} message")
            .WithCategory(Category)
            .WhenAsync(async (context, cancellationToken) =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            })
            .Build();
    }
}
