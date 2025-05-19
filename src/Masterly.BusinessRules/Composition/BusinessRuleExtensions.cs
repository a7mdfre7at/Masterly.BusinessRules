using Masterly.BusinessRules.Abstractions;

namespace Masterly.BusinessRules.Composition
{
    public static class BusinessRuleExtensions
    {
        public static IBusinessRule And(this IBusinessRule first, IBusinessRule second)
        {
            return new SimpleBusinessRule(
                () => first.IsBroken() && second.IsBroken(),
                $"Both rules must be satisfied: {first.Message} AND {second.Message}",
                $"{first.Code}+{second.Code}"
            );
        }

        public static IBusinessRule Or(this IBusinessRule first, IBusinessRule second)
        {
            return new SimpleBusinessRule(
                () => first.IsBroken() || second.IsBroken(),
                $"At least one rule must be satisfied: {first.Message} OR {second.Message}",
                $"{first.Code}|{second.Code}"
            );
        }

        public static IBusinessRule Not(this IBusinessRule rule)
        {
            return new SimpleBusinessRule(
                () => !rule.IsBroken(),
                $"NOT: {rule.Message}",
                $"!{rule.Code}"
            );
        }
    }
}