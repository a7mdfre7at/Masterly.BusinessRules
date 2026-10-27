namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Stores the requested <see cref="RuleScope"/> under <see cref="RuleParameters.ScopeKey"/> for the duration of one
    /// registry batch and puts back what the caller's context held before (or removes the key), so layer selection and
    /// parameter resolution always use the same scope and a reused context never keeps a previous call's scope.
    /// </summary>
    internal readonly struct RuleScopeOverride
    {
        private readonly BusinessRuleContext _context;
        private readonly bool _hadValue;
        private readonly object? _previous;

        private RuleScopeOverride(BusinessRuleContext context, bool hadValue, object? previous)
        {
            _context = context;
            _hadValue = hadValue;
            _previous = previous;
        }

        /// <summary>
        /// Remembers the context's current scope entry and replaces it with <paramref name="scope"/>.
        /// </summary>
        /// <param name="context">The context the batch evaluates against.</param>
        /// <param name="scope">The scope the batch was requested for.</param>
        /// <returns>The override to <see cref="Restore"/> when the batch ends.</returns>
        public static RuleScopeOverride Apply(BusinessRuleContext context, RuleScope scope)
        {
            bool hadValue = context.ContainsKey(RuleParameters.ScopeKeyName);
            context.TryGet(RuleParameters.ScopeKeyName, out object? previous);
            context.Set(RuleParameters.ScopeKey, scope);
            return new RuleScopeOverride(context, hadValue, previous);
        }

        /// <summary>
        /// Puts back the scope entry the context held before <see cref="Apply"/>, or removes the key when there was none.
        /// </summary>
        public void Restore()
        {
            if (_hadValue)
            {
                _context.Set(RuleParameters.ScopeKeyName, _previous);
                return;
            }

            _context.Remove(RuleParameters.ScopeKeyName);
        }
    }
}
