using Masterly.BusinessRules.Abstractions;

namespace Masterly.BusinessRules.Composition
{
    public static class AsyncBusinessRuleExtensions
    {
        public static IAsyncBusinessRule And(this IAsyncBusinessRule first, IAsyncBusinessRule second)
        {
            return new CompositeAsyncBusinessRule(
                async (ctx, ct) =>
                    await first.IsBrokenAsync(ctx, ct) || await second.IsBrokenAsync(ctx, ct),
                $"{first.Message} AND {second.Message}",
                $"{first.Code}+{second.Code}"
            );
        }

        public static IAsyncBusinessRule Or(this IAsyncBusinessRule first, IAsyncBusinessRule second)
        {
            return new CompositeAsyncBusinessRule(
                async (ctx, ct) =>
                    await first.IsBrokenAsync(ctx, ct) && await second.IsBrokenAsync(ctx, ct),
                $"{first.Message} OR {second.Message}",
                $"{first.Code}|{second.Code}"
            );
        }

        public static IAsyncBusinessRule Not(this IAsyncBusinessRule rule)
        {
            return new CompositeAsyncBusinessRule(
                async (ctx, ct) => !await rule.IsBrokenAsync(ctx, ct),
                $"NOT: {rule.Message}",
                $"!{rule.Code}"
            );
        }
    }
}