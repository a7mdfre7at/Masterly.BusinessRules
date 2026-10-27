using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Finds the rule types of an assembly and describes their registrations. Candidates are concrete, closed classes
    /// (no abstract classes, no open generics) that pass the visibility and type filters of <see cref="BusinessRuleOptions"/>;
    /// each is registered under every closed <see cref="IBusinessRule{TSubject}"/> / <see cref="IAsyncBusinessRule{TSubject}"/>
    /// it implements and, when untyped rules are included, under <see cref="IBusinessRule"/> / <see cref="IAsyncBusinessRule"/>.
    /// Types are visited in ordinal order of their full name so registration order is deterministic.
    /// </summary>
    internal static class RuleTypeScanner
    {
        private static readonly Type SyncTypedDefinition = typeof(IBusinessRule<>);
        private static readonly Type AsyncTypedDefinition = typeof(IAsyncBusinessRule<>);
        private static readonly Type SyncUntyped = typeof(IBusinessRule);
        private static readonly Type AsyncUntyped = typeof(IAsyncBusinessRule);

        /// <summary>
        /// Describes one registration per (rule interface, rule type) pair found in the assembly.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <param name="options">The filters and lifetime.</param>
        /// <returns>The descriptors, in deterministic order.</returns>
        public static IEnumerable<ServiceDescriptor> Scan(Assembly assembly, BusinessRuleOptions options)
        {
            foreach (Type type in LoadTypes(assembly).OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                if (!IsCandidate(type, options))
                {
                    continue;
                }

                foreach (Type service in RuleServicesOf(type, options.IncludeUntypedRules))
                {
                    yield return new ServiceDescriptor(service, type, options.Lifetime);
                }
            }
        }

        /// <summary>
        /// Returns the rule interfaces a type is registered under.
        /// </summary>
        /// <param name="type">The candidate type.</param>
        /// <param name="includeUntyped">Whether the plain <see cref="IBusinessRule"/> / <see cref="IAsyncBusinessRule"/> count.</param>
        /// <returns>The closed rule interfaces, in the order the runtime reports them.</returns>
        public static IEnumerable<Type> RuleServicesOf(Type type, bool includeUntyped)
        {
            foreach (Type contract in type.GetInterfaces())
            {
                if (IsTypedRuleInterface(contract) || (includeUntyped && IsUntypedRuleInterface(contract)))
                {
                    yield return contract;
                }
            }
        }

        private static bool IsCandidate(Type type, BusinessRuleOptions options)
        {
            if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
            {
                return false;
            }

            if (!options.IncludeNonPublicTypes && !type.IsVisible)
            {
                return false;
            }

            return options.TypeFilter == null || options.TypeFilter(type);
        }

        private static bool IsTypedRuleInterface(Type contract)
        {
            if (!contract.IsGenericType)
            {
                return false;
            }

            Type definition = contract.GetGenericTypeDefinition();
            return definition == SyncTypedDefinition || definition == AsyncTypedDefinition;
        }

        private static bool IsUntypedRuleInterface(Type contract)
        {
            return contract == SyncUntyped || contract == AsyncUntyped;
        }

        private static IEnumerable<Type> LoadTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // A partially loadable assembly still yields the types that did load; the others cannot be rules we can construct.
                List<Type> loaded = new List<Type>();
                foreach (Type? type in exception.Types)
                {
                    if (type != null)
                    {
                        loaded.Add(type);
                    }
                }

                return loaded;
            }
        }
    }
}
