using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Reads properties by name on runtime values (interpreter) and resolves them on static types (typed compiler),
    /// subject to <see cref="RuleExpressionOptions"/>. <c>Count</c> and <c>Length</c> on strings and
    /// <see cref="ICollection"/>s are built-ins that never need a declared property. Lookups are exact-case first, then
    /// case-insensitive when unambiguous; property metadata is cached per type and name.
    /// </summary>
    internal static class RuleExpressionMemberAccessor
    {
        public const string CountMember = "Count";
        public const string LengthMember = "Length";

        private const BindingFlags InstanceProperties = BindingFlags.Public | BindingFlags.Instance;

        private static readonly ConcurrentDictionary<(Type Type, string Member), PropertyInfo?> PropertyCache = new ConcurrentDictionary<(Type, string), PropertyInfo?>();

        /// <summary>
        /// Reads <paramref name="member"/> from <paramref name="target"/>; null-safe.
        /// </summary>
        /// <exception cref="RuleExpressionBindingException">Thrown when the member does not exist or is not readable under the options.</exception>
        public static object? GetValue(object? target, string member, RuleExpressionOptions options, string pathText)
        {
            if (target == null)
            {
                return null;
            }

            Type type = target.GetType();
            PropertyInfo? property = FindProperty(type, member);
            if (property != null && options.IsAccessible(property))
            {
                return Read(property, target);
            }

            if (IsCountOrLength(member))
            {
                if (target is string text)
                {
                    return text.Length;
                }

                if (target is ICollection collection)
                {
                    return collection.Count;
                }
            }

            throw new RuleExpressionBindingException(pathText, AccessibleMembers(type, options));
        }

        /// <summary>
        /// Finds a public instance property by name on <paramref name="type"/> (including the members of inherited
        /// interfaces when the type is an interface). Exact case wins; otherwise a single case-insensitive match.
        /// </summary>
        public static PropertyInfo? FindProperty(Type type, string member) => PropertyCache.GetOrAdd((type, member), key => Lookup(key.Type, key.Member));

        public static bool IsCountOrLength(string member)
            => string.Equals(member, CountMember, StringComparison.Ordinal) || string.Equals(member, LengthMember, StringComparison.Ordinal);

        /// <summary>
        /// Lists the members of <paramref name="type"/> an expression may read under <paramref name="options"/>, plus
        /// the applicable built-ins.
        /// </summary>
        public static IReadOnlyList<string> AccessibleMembers(Type type, RuleExpressionOptions options)
        {
            SortedSet<string> names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (PropertyInfo property in AllProperties(type))
            {
                if (options.IsAccessible(property))
                {
                    names.Add(property.Name);
                }
            }

            if (type == typeof(string))
            {
                names.Add(CountMember);
                names.Add(LengthMember);
            }
            else if (typeof(ICollection).IsAssignableFrom(type))
            {
                names.Add(CountMember);
            }

            return names.ToArray();
        }

        private static PropertyInfo? Lookup(Type type, string member)
        {
            PropertyInfo? exact = null;
            PropertyInfo? caseInsensitive = null;
            bool ambiguous = false;

            foreach (PropertyInfo property in AllProperties(type))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (string.Equals(property.Name, member, StringComparison.Ordinal))
                {
                    if (exact == null || IsMoreDerived(property, exact))
                    {
                        exact = property;
                    }
                }
                else if (string.Equals(property.Name, member, StringComparison.OrdinalIgnoreCase))
                {
                    ambiguous = caseInsensitive != null && !string.Equals(caseInsensitive.Name, property.Name, StringComparison.Ordinal);
                    caseInsensitive = caseInsensitive != null && IsMoreDerived(caseInsensitive, property) ? caseInsensitive : property;
                }
            }

            if (exact != null)
            {
                return exact;
            }

            return ambiguous ? null : caseInsensitive;
        }

        private static IEnumerable<PropertyInfo> AllProperties(Type type)
        {
            IEnumerable<PropertyInfo> properties = type.GetProperties(InstanceProperties);
            if (type.IsInterface)
            {
                properties = properties.Concat(type.GetInterfaces().SelectMany(inherited => inherited.GetProperties(InstanceProperties)));
            }

            return properties;
        }

        private static bool IsMoreDerived(PropertyInfo candidate, PropertyInfo current)
        {
            Type? candidateType = candidate.DeclaringType;
            Type? currentType = current.DeclaringType;
            return candidateType != null && currentType != null && candidateType != currentType && currentType.IsAssignableFrom(candidateType);
        }

        private static object? Read(PropertyInfo property, object target)
        {
            try
            {
                return property.GetValue(target);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw; // unreachable: Throw() never returns, but the compiler needs a terminating statement
            }
        }
    }
}
