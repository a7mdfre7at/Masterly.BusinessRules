namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines a synchronous business rule evaluated against a subject supplied at evaluation time rather than
    /// captured by the constructor. A subject-typed rule is stateless, so one instance can be registered as a
    /// singleton and reused for many subjects. It does <b>not</b> derive from <see cref="IBusinessRule"/>: no member
    /// can be called without a subject, so none ever throws for the lack of one. Bind a subject with
    /// <see cref="TypedBusinessRuleExtensions.For{TSubject}(IBusinessRule{TSubject}, TSubject)"/> to obtain an
    /// <see cref="IBusinessRule"/> that works with every decorator and checker, or pass the subject to the typed
    /// <see cref="BusinessRuleChecker"/> overloads.
    /// </summary>
    /// <typeparam name="TSubject">The type of the subject the rule validates. Value types are supported.</typeparam>
    public interface IBusinessRule<in TSubject> : IBusinessRuleMetadata
    {
        /// <summary>
        /// Evaluates whether the business rule is broken for the given subject.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <returns>True if the rule is broken; otherwise, false.</returns>
        bool IsBroken(TSubject subject);

        /// <summary>
        /// Evaluates whether the business rule is broken for the given subject using the provided context.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <returns>True if the rule is broken; otherwise, false.</returns>
        bool IsBroken(TSubject subject, BusinessRuleContext context);

        /// <summary>
        /// Creates the result describing this rule as broken for the given subject: its code, message, severity and
        /// metadata. This is a factory, not an evaluation; the library calls it only after
        /// <see cref="IsBroken(TSubject)"/> returned true.
        /// </summary>
        /// <param name="subject">The subject the rule was evaluated against.</param>
        /// <returns>A <see cref="BusinessRuleResult"/> representing the violation.</returns>
        BusinessRuleResult Evaluate(TSubject subject);

        /// <summary>
        /// Creates the result describing this rule as broken for the given subject using the provided context, so
        /// that context-aware details can be attached. The default implementation ignores the context and returns
        /// <see cref="Evaluate(TSubject)"/>. This is a factory, not an evaluation.
        /// </summary>
        /// <param name="subject">The subject the rule was evaluated against.</param>
        /// <param name="context">The context the rule was evaluated with.</param>
        /// <returns>A <see cref="BusinessRuleResult"/> representing the violation.</returns>
        BusinessRuleResult Evaluate(TSubject subject, BusinessRuleContext context) => Evaluate(subject);
    }
}
