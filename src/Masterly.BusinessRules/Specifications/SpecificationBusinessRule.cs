using System;
using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Base class for rules whose verdict is written once as an expression tree and used twice: compiled for in-memory
    /// evaluation (<see cref="IsBroken(TSubject)"/>, the typed checkers, <c>.For(subject)</c>) and handed to a LINQ
    /// provider by <see cref="SpecificationQueryableExtensions.WhereBroken{TSubject}"/> /
    /// <see cref="SpecificationQueryableExtensions.WhereSatisfies{TSubject}"/>. Inherit from this class and override
    /// <see cref="Code"/>, <see cref="Message"/> and <see cref="BrokenWhen"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="BrokenWhen"/> is read once and cached; the delegate behind <see cref="IsBroken(TSubject)"/> is compiled
    /// lazily on first use, so constructing a rule never compiles anything. Both <c>IsBroken</c> overloads are sealed so
    /// that the in-memory verdict and the translated predicate can never disagree; the context is therefore not
    /// consulted. Anything inside the expression that a provider evaluates server-side (for example
    /// <see cref="DateTime.UtcNow"/>) is evaluated by that provider when translated and on the local clock in memory.
    /// Only a specification rule, or a combination of specification rules through <c>AllOf</c>/<c>AnyOf</c>/<c>Not</c>,
    /// is translatable: wrapping it in <c>.Cached()</c>, <c>.When()</c>, <c>.WithSeverity()</c>, the typed
    /// <c>.And()</c>/<c>.Or()</c>/<c>.Not()</c> or <c>.For(subject)</c> yields an ordinary rule that
    /// <c>WhereBroken</c>/<c>WhereSatisfies</c> do not accept.
    /// </remarks>
    /// <typeparam name="TSubject">The type of the subject the rule validates.</typeparam>
    /// <example>
    /// <code>
    /// public sealed class OrderIsOverdueRule : SpecificationBusinessRule&lt;Order&gt;
    /// {
    ///     public override string Code => "ORDER.OVERDUE";
    ///     public override string Message => "Order is past its due date.";
    ///     public override Expression&lt;Func&lt;Order, bool&gt;&gt; BrokenWhen => o => o.DueDate &lt; DateTime.UtcNow &amp;&amp; !o.IsPaid;
    /// }
    /// </code>
    /// </example>
    public abstract class SpecificationBusinessRule<TSubject> : BaseBusinessRule<TSubject>, ISpecificationRule<TSubject>
    {
        private readonly Lazy<Expression<Func<TSubject, bool>>> _brokenExpression;
        private readonly Lazy<Expression<Func<TSubject, bool>>> _satisfiedExpression;
        private readonly Lazy<Func<TSubject, bool>> _isBroken;

        /// <summary>
        /// Initializes a new <see cref="SpecificationBusinessRule{TSubject}"/>. Nothing is read or compiled until the
        /// first evaluation or the first access to an expression property.
        /// </summary>
        protected SpecificationBusinessRule()
        {
            _brokenExpression = new Lazy<Expression<Func<TSubject, bool>>>(ReadBrokenWhen);
            _satisfiedExpression = new Lazy<Expression<Func<TSubject, bool>>>(() => SpecificationExpressions.Negate(_brokenExpression.Value));
            _isBroken = new Lazy<Func<TSubject, bool>>(() => _brokenExpression.Value.Compile());
        }

        /// <summary>
        /// Gets the predicate that is true for every subject that violates the rule. It is read once, on first use, and
        /// cached for the lifetime of the rule, so the getter may build the expression inline.
        /// </summary>
        public abstract Expression<Func<TSubject, bool>> BrokenWhen { get; }

        /// <summary>
        /// Gets the cached <see cref="BrokenWhen"/> expression. The same instance is returned on every call.
        /// </summary>
        /// <exception cref="RuleConfigurationException">Thrown when <see cref="BrokenWhen"/> returns null.</exception>
        public Expression<Func<TSubject, bool>> BrokenExpression => _brokenExpression.Value;

        /// <summary>
        /// Gets the logical negation of <see cref="BrokenExpression"/> as a lambda over the same parameter. The same
        /// instance is returned on every call.
        /// </summary>
        /// <exception cref="RuleConfigurationException">Thrown when <see cref="BrokenWhen"/> returns null.</exception>
        public Expression<Func<TSubject, bool>> SatisfiedExpression => _satisfiedExpression.Value;

        /// <summary>
        /// Evaluates the compiled <see cref="BrokenWhen"/> predicate against the subject. The predicate is compiled once,
        /// on the first call, and reused afterwards.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <returns><c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        /// <exception cref="RuleConfigurationException">Thrown when <see cref="BrokenWhen"/> returns null.</exception>
        public sealed override bool IsBroken(TSubject subject) => _isBroken.Value(subject);

        /// <summary>
        /// Evaluates the compiled <see cref="BrokenWhen"/> predicate against the subject. The context is not consulted,
        /// because a translated predicate has no context either; this keeps the in-memory and the translated verdicts identical.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The evaluation context; ignored.</param>
        /// <returns><c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        /// <exception cref="RuleConfigurationException">Thrown when <see cref="BrokenWhen"/> returns null.</exception>
        public sealed override bool IsBroken(TSubject subject, BusinessRuleContext context) => IsBroken(subject);

        private Expression<Func<TSubject, bool>> ReadBrokenWhen()
        {
            Expression<Func<TSubject, bool>> expression = BrokenWhen;
            if (expression is null)
            {
                throw new RuleConfigurationException(Code, SpecificationText.MissingBrokenWhen);
            }

            return expression;
        }
    }
}
