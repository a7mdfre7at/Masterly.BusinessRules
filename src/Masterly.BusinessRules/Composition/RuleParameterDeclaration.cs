using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// One parameter declared on a builder with <c>WithParameter&lt;T&gt;(name, fallback)</c>: the name, the declared
    /// type and the fallback used when no provider supplies a value. The non-generic base lets a builder hold
    /// declarations of different value types in one list.
    /// </summary>
    internal abstract class RuleParameterDeclaration
    {
        /// <summary>
        /// Initializes the declaration with its name.
        /// </summary>
        protected RuleParameterDeclaration(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the declared value type.
        /// </summary>
        public abstract Type ValueType { get; }

        /// <summary>
        /// Resolves the value through a sync provider, or returns the fallback when the provider is null or has no value.
        /// </summary>
        public abstract object? Resolve(IRuleParameterProvider? provider, RuleScope? scope);

        /// <summary>
        /// Resolves the value through an async provider, or returns the fallback when it has no value.
        /// </summary>
        public abstract Task<object?> ResolveAsync(IAsyncRuleParameterProvider provider, RuleScope? scope, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The typed declaration behind <see cref="RuleParameterDeclaration"/>.
    /// </summary>
    /// <typeparam name="T">The declared value type.</typeparam>
    internal sealed class RuleParameterDeclaration<T> : RuleParameterDeclaration
    {
        private readonly T _fallback;

        /// <summary>
        /// Initializes the declaration with its name and fallback.
        /// </summary>
        public RuleParameterDeclaration(string name, T fallback)
            : base(name)
        {
            _fallback = fallback;
        }

        /// <inheritdoc />
        public override Type ValueType => typeof(T);

        /// <inheritdoc />
        public override object? Resolve(IRuleParameterProvider? provider, RuleScope? scope)
        {
            if (provider != null && provider.TryGet(Name, scope, out T value))
            {
                return value;
            }

            return _fallback;
        }

        /// <inheritdoc />
        public override async Task<object?> ResolveAsync(IAsyncRuleParameterProvider provider, RuleScope? scope, CancellationToken cancellationToken)
        {
            RuleParameterLookup<T> lookup = await provider.TryGetAsync<T>(Name, scope, cancellationToken);
            return lookup.Found ? lookup.Value : _fallback;
        }
    }
}
