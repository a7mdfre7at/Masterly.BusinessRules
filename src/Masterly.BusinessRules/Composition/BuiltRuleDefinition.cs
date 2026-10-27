using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The immutable snapshot of a rule builder's configuration, handed to an <see cref="IBuiltRuleFactory"/> or
    /// <see cref="IAsyncBuiltRuleFactory"/> at <c>Build()</c>. It carries the seven metadata members, resolves the
    /// parameters declared with <c>WithParameter&lt;T&gt;</c> exactly as a built rule does, and builds results with
    /// the context-aware details from <c>WithDetail</c>/<c>WithActual</c>/<c>WithExpected</c>/<c>WithRemediation</c>,
    /// so a rule created by a factory behaves like any other built rule apart from its condition.
    /// </summary>
    public sealed class BuiltRuleDefinition
    {
        private readonly BuiltRuleMetadata _metadata;
        private readonly RuleParameterBinding _parameters;
        private readonly BuiltRuleDescriptor _descriptor;

        internal BuiltRuleDefinition(BuiltRuleMetadata metadata, RuleParameterBinding parameters, BuiltRuleDescriptor descriptor)
        {
            _metadata = metadata;
            _parameters = parameters;
            _descriptor = descriptor;
        }

        /// <summary>Gets the rule code.</summary>
        public string Code => _metadata.Code;

        /// <summary>Gets the violation message.</summary>
        public string Message => _metadata.Message;

        /// <summary>Gets the severity.</summary>
        public RuleSeverity Severity => _metadata.Severity;

        /// <summary>Gets the name; the code when no name was configured.</summary>
        public string Name => _metadata.Name;

        /// <summary>Gets the description; empty when none was configured.</summary>
        public string Description => _metadata.Description;

        /// <summary>Gets the category; empty when none was configured.</summary>
        public string Category => _metadata.Category;

        /// <summary>Gets the tags, copied when the rule was built.</summary>
        public IReadOnlyList<string> Tags => _metadata.Tags;

        /// <summary>
        /// Gets whether any context-aware detail function was configured.
        /// </summary>
        public bool HasDetails => !_descriptor.IsEmpty;

        /// <summary>
        /// Resolves the declared parameters for a synchronous evaluation, through the provider stored under
        /// <see cref="RuleParameters.ContextKey"/> or the injected one, for the scope under <see cref="RuleParameters.ScopeKey"/>.
        /// A null context resolves the global scope through the injected provider.
        /// </summary>
        /// <param name="context">The evaluation context, or null on the parameterless path.</param>
        /// <returns>The resolved parameters; <see cref="RuleParameters.Empty"/> when nothing was declared.</returns>
        /// <exception cref="ContextValueTypeMismatchException">Thrown when a reserved key holds a value of another type.</exception>
        public RuleParameters ResolveParameters(BusinessRuleContext? context)
        {
            return _parameters.Resolve(context);
        }

        /// <summary>
        /// Resolves the declared parameters for an asynchronous evaluation, preferring an async provider (from
        /// <see cref="RuleParameters.AsyncContextKey"/>, then the injected async one) over a sync one.
        /// </summary>
        /// <param name="context">The evaluation context, or null to resolve the global scope.</param>
        /// <param name="cancellationToken">A token to cancel the resolution.</param>
        /// <returns>The resolved parameters; <see cref="RuleParameters.Empty"/> when nothing was declared.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        /// <exception cref="ContextValueTypeMismatchException">Thrown when a reserved key holds a value of another type.</exception>
        public async Task<RuleParameters> ResolveParametersAsync(BusinessRuleContext? context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _parameters.ResolveAsync(context, cancellationToken);
        }

        /// <summary>
        /// Builds the broken result for <paramref name="rule"/>, attaching the configured details. The detail functions
        /// run only here; an exception inside any of them degrades the result to one without details.
        /// </summary>
        /// <param name="rule">The rule whose metadata the result carries (normally the rule the factory created).</param>
        /// <param name="context">The evaluation context, or null on the parameterless path.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public BusinessRuleResult CreateResult(IBusinessRuleMetadata rule, BusinessRuleContext? context)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return _descriptor.CreateResult(rule, context);
        }
    }
}
