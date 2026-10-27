using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A decorator that records every verdict of the wrapped rule into a <see cref="RuleCoverageSession"/> under the
    /// rule's <see cref="Code"/>. All seven metadata members are forwarded unchanged; the verdict is forwarded too.
    /// Outcomes are recorded from both <see cref="IsBroken()"/> overloads and both <see cref="Check()"/> overloads; the
    /// <see cref="Evaluate()"/> factories record nothing because they do not evaluate. A rule that throws records nothing.
    /// Wrap the operands <em>before</em> composing them so operand outcomes (and short-circuited operands) are visible.
    /// Created through <see cref="RuleCoverageExtensions.WithCoverage(IBusinessRule, RuleCoverageSession)"/>.
    /// It implements the core <see cref="IExplainableDecoratedBusinessRule"/> seam, so <c>Explain</c> shows a
    /// <c>COVERAGE</c> node with the wrapped rule's own explanation tree beneath it, and a coverage-wrapped operand
    /// inside <c>And</c>/<c>Or</c>/<c>Not</c> or a group records its outcome through the same single evaluation.
    /// </summary>
    public sealed class CoverageBusinessRule : IExplainableDecoratedBusinessRule
    {
        private readonly IBusinessRule _innerRule;
        private readonly RuleCoverageSession _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoverageBusinessRule"/> class and registers the inner rule's
        /// code with the session so it is reported even when never evaluated.
        /// </summary>
        /// <param name="innerRule">The rule whose outcomes are recorded.</param>
        /// <param name="session">The session that receives the outcomes.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="session"/> is null.</exception>
        public CoverageBusinessRule(IBusinessRule innerRule, RuleCoverageSession session)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.Register(innerRule);
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the operator label of this decorator's explanation node.
        /// </summary>
        public string ExplanationOperator => RuleCoverageText.CoverageOperator;

        /// <summary>
        /// Gets the session the outcomes are recorded into.
        /// </summary>
        public RuleCoverageSession Session => _session;

        /// <inheritdoc />
        public string Code => _innerRule.Code;

        /// <inheritdoc />
        public string Message => _innerRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _innerRule.Severity;

        /// <inheritdoc />
        public string Name => _innerRule.Name;

        /// <inheritdoc />
        public string Description => _innerRule.Description;

        /// <inheritdoc />
        public string Category => _innerRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _innerRule.Tags;

        /// <inheritdoc />
        public bool IsBroken()
        {
            bool isBroken = _innerRule.IsBroken();
            _session.Record(_innerRule, isBroken);
            return isBroken;
        }

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context)
        {
            bool isBroken = _innerRule.IsBroken(context);
            _session.Record(_innerRule, isBroken);
            return isBroken;
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule is broken; the inner rule's own exception propagates.</exception>
        public void Check()
        {
            try
            {
                _innerRule.Check();
            }
            catch (BusinessRuleValidationException)
            {
                _session.Record(_innerRule, true);
                throw;
            }

            _session.Record(_innerRule, false);
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule is broken; the inner rule's own exception propagates.</exception>
        public void Check(BusinessRuleContext context)
        {
            try
            {
                _innerRule.Check(context);
            }
            catch (BusinessRuleValidationException)
            {
                _session.Record(_innerRule, true);
                throw;
            }

            _session.Record(_innerRule, false);
        }

        /// <inheritdoc />
        /// <remarks>Forwarded to the inner rule; nothing is recorded because this is a factory, not an evaluation.</remarks>
        public BusinessRuleResult Evaluate() => _innerRule.Evaluate();

        /// <inheritdoc />
        /// <remarks>Forwarded to the inner rule; nothing is recorded because this is a factory, not an evaluation.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context) => _innerRule.Evaluate(context);

        /// <summary>
        /// Evaluates the wrapped rule once through <paramref name="evaluation"/>, records the outcome and forwards the verdict.
        /// </summary>
        /// <param name="evaluation">The one-shot evaluation of the wrapped rule supplied by the core walker.</param>
        /// <returns>The wrapped rule's broken result, or null when it passed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is null.</exception>
        public BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            BusinessRuleResult? result = evaluation.EvaluateInner();
            _session.Record(_innerRule, result != null);
            return result;
        }
    }
}
