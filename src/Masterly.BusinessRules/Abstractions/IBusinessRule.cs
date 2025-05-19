namespace Masterly.BusinessRules
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