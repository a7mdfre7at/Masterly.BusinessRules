using Masterly.BusinessRules.Abstractions;

namespace Masterly.BusinessRules.Core
{
    public sealed class BusinessRuleResult
    {
        public string Code { get; }
        public string Message { get; }
        public RuleSeverity Severity { get; }

        public BusinessRuleResult(string code, string message, RuleSeverity severity)
        {
            Code = code;
            Message = message;
            Severity = severity;
        }
    }
}