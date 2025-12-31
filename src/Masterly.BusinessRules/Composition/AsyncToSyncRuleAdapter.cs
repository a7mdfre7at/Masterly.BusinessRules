using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Adapts an asynchronous business rule to work as a synchronous rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This adapter blocks the calling thread using <c>GetAwaiter().GetResult()</c>.
    /// Use with caution to avoid deadlocks, especially in UI applications or ASP.NET contexts.
    /// </para>
    /// <para>
    /// Prefer using async rules natively with <see cref="AsyncBusinessRuleChecker"/> when possible.
    /// </para>
    /// </remarks>
    public sealed class AsyncToSyncRuleAdapter : IBusinessRule
    {
        private readonly IAsyncBusinessRule _asyncRule;
        private readonly BusinessRuleContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncToSyncRuleAdapter"/> class.
        /// </summary>
        /// <param name="asyncRule">The asynchronous rule to adapt.</param>
        /// <param name="context">Optional context to use for parameterless evaluations. Defaults to an empty context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="asyncRule"/> is null.</exception>
        public AsyncToSyncRuleAdapter(IAsyncBusinessRule asyncRule, BusinessRuleContext? context = null)
        {
            _asyncRule = asyncRule ?? throw new ArgumentNullException(nameof(asyncRule));
            _context = context ?? new BusinessRuleContext();
        }

        /// <inheritdoc />
        public string Code => _asyncRule.Code;

        /// <inheritdoc />
        public string Message => _asyncRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _asyncRule.Severity;

        /// <inheritdoc />
        public string Name => _asyncRule.Name;

        /// <inheritdoc />
        public string Description => _asyncRule.Description;

        /// <inheritdoc />
        public string Category => _asyncRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _asyncRule.Tags;

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        public bool IsBroken()
        {
            return _asyncRule.IsBrokenAsync(_context).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        public bool IsBroken(BusinessRuleContext context)
        {
            return _asyncRule.IsBrokenAsync(context).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public BusinessRuleResult Evaluate()
        {
            return new BusinessRuleResult(Code, Message, Severity);
        }

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when the wrapped rule is broken.</exception>
        public void Check()
        {
            _asyncRule.CheckAsync(_context).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when the wrapped rule is broken.</exception>
        public void Check(BusinessRuleContext context)
        {
            _asyncRule.CheckAsync(context).GetAwaiter().GetResult();
        }
    }
}
