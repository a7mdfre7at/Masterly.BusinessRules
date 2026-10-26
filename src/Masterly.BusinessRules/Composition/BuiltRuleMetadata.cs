using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The immutable metadata captured by a <see cref="BusinessRuleMetadataBuilder{TBuilder}"/> and forwarded by the
    /// rule it builds. An empty <c>Name</c> defaults to the code, as the builders have always done.
    /// </summary>
    internal sealed class BuiltRuleMetadata
    {
        /// <summary>
        /// Initializes the metadata from the builder's configured values.
        /// </summary>
        public BuiltRuleMetadata(
            string code,
            string message,
            RuleSeverity severity,
            string name,
            string description,
            string category,
            IReadOnlyList<string> tags)
        {
            Code = code;
            Message = message;
            Severity = severity;
            Name = string.IsNullOrEmpty(name) ? code : name;
            Description = description;
            Category = category;
            Tags = tags;
        }

        /// <summary>Gets the rule code.</summary>
        public string Code { get; }

        /// <summary>Gets the violation message.</summary>
        public string Message { get; }

        /// <summary>Gets the severity.</summary>
        public RuleSeverity Severity { get; }

        /// <summary>Gets the name, defaulting to the code.</summary>
        public string Name { get; }

        /// <summary>Gets the description.</summary>
        public string Description { get; }

        /// <summary>Gets the category.</summary>
        public string Category { get; }

        /// <summary>Gets the tags.</summary>
        public IReadOnlyList<string> Tags { get; }
    }
}
