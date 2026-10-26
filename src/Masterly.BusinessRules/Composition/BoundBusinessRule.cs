using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Binds a subject-typed <see cref="IBusinessRule{TSubject}"/> to one subject, producing an ordinary
    /// <see cref="IBusinessRule"/> that forwards all seven metadata members and evaluates the typed rule against the
    /// bound subject. Every decorator (<c>.Cached()</c>, <c>.When()</c>, <c>.WithSeverity()</c>), composition operator
    /// and checker therefore works on a bound rule without any member ever throwing for lack of a subject.
    /// Created through the <see cref="TypedBusinessRuleExtensions.For{TSubject}(IBusinessRule{TSubject}, TSubject)"/> extension.
    /// The subject is handed to the rule as-is, so a rule may validate a null subject; value-type subjects are supported.
    /// </summary>
    /// <typeparam name="TSubject">The type of the bound subject.</typeparam>
    /// <example>
    /// <code>
    /// IBusinessRule bound = new OrderMustHaveItemsRule().For(order);
    /// IBusinessRule cached = bound.Cached(TimeSpan.FromMinutes(1));
    /// </code>
    /// </example>
    public sealed class BoundBusinessRule<TSubject> : IBusinessRule
    {
        private readonly IBusinessRule<TSubject> _rule;
        private readonly TSubject _subject;

        /// <summary>
        /// Initializes a new <see cref="BoundBusinessRule{TSubject}"/> over a typed rule and the subject it validates.
        /// </summary>
        /// <param name="rule">The subject-typed rule.</param>
        /// <param name="subject">The subject to evaluate the rule against.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public BoundBusinessRule(IBusinessRule<TSubject> rule, TSubject subject)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _subject = subject;
        }

        /// <summary>
        /// Gets the bound subject-typed rule.
        /// </summary>
        public IBusinessRule<TSubject> Rule => _rule;

        /// <summary>
        /// Gets the subject the rule is evaluated against.
        /// </summary>
        public TSubject Subject => _subject;

        /// <inheritdoc />
        public string Code => _rule.Code;

        /// <inheritdoc />
        public string Message => _rule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _rule.Severity;

        /// <inheritdoc />
        public string Name => _rule.Name;

        /// <inheritdoc />
        public string Description => _rule.Description;

        /// <inheritdoc />
        public string Category => _rule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _rule.Tags;

        /// <inheritdoc />
        public bool IsBroken() => _rule.IsBroken(_subject);

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context) => _rule.IsBroken(_subject, context);

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the typed rule is broken for the bound subject.</exception>
        public void Check()
        {
            if (IsBroken())
            {
                throw new BusinessRuleValidationException(new[] { Evaluate() });
            }
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the typed rule is broken for the bound subject.</exception>
        public void Check(BusinessRuleContext context)
        {
            if (IsBroken(context))
            {
                throw new BusinessRuleValidationException(new[] { Evaluate(context) });
            }
        }

        /// <inheritdoc />
        /// <remarks>Forwards to the typed rule's <see cref="IBusinessRule{TSubject}.Evaluate(TSubject)"/> factory.</remarks>
        public BusinessRuleResult Evaluate() => _rule.Evaluate(_subject);

        /// <inheritdoc />
        /// <remarks>Forwards to the typed rule's <see cref="IBusinessRule{TSubject}.Evaluate(TSubject, BusinessRuleContext)"/> factory.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context) => _rule.Evaluate(_subject, context);
    }
}
