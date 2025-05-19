using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    public interface IAsyncBusinessRule
    {
        string Code { get; }
        string Message { get; }
        RuleSeverity Severity { get; }
        Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
        Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
        Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
    }
}