using System;

namespace Mosaic.Bridge.Contracts.Attributes
{
    /// <summary>
    /// Marks a parameter class property as required. The bridge's parameter validator
    /// (per FR14) rejects requests where required properties are missing or null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredAttribute : Attribute
    {
        /// <summary>
        /// Optional custom error message returned when validation fails.
        /// If not specified, the validator generates a default message.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
