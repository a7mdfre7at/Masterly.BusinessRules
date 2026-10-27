using System;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A misconfigured property set detected while sampling (a factory returned null, or the generator could not produce
    /// an input). The sampler lets it escape instead of recording it as a counter-example, so the caller sees the
    /// configuration fault, not a law failure; it leaves the sampler as a plain <see cref="InvalidOperationException"/>.
    /// </summary>
    internal sealed class RulePropertySetupException : InvalidOperationException
    {
        public RulePropertySetupException(string message) : base(message)
        {
        }

        public RulePropertySetupException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Converts the internal marker into the documented public exception type, keeping the message and cause.
        /// </summary>
        public InvalidOperationException ToPublicException()
        {
            return new InvalidOperationException(Message, InnerException);
        }
    }
}
