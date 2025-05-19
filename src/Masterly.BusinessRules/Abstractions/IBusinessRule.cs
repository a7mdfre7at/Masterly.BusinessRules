using Masterly.BusinessRules.Core;

namespace Masterly.BusinessRules.Abstractions
{
    public interface IBusinessRule
    {
        string Code { get; }
        string Message { get; }
        RuleSeverity Severity { get; }
        bool IsBroken();
        void Check();
        BusinessRuleResult Evaluate();
    }
}