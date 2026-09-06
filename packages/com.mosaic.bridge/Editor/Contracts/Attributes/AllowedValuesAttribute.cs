using System;

namespace Mosaic.Bridge.Contracts.Attributes
{
    /// <summary>
    /// The values a string parameter accepts, published in the tool's inputSchema.
    /// </summary>
    /// <remarks>
    /// Written after an acceptance round found the closed loop from the other side: the
    /// validator knows the six valid primitives and says so when you get one wrong, but the
    /// published schema described the parameter as "string". So the only way to learn a
    /// value was to send a wrong one — a documented path that requires a failed call is not
    /// a documented path.
    ///
    /// <para><see cref="Exhaustive"/> is the whole point of the attribute. `primitiveType`
    /// accepts those six names and nothing else, so its list is a JSON Schema `enum`.
    /// `captureTarget` accepts the known panels OR the title of any open editor window, so
    /// its list is `examples`: publishing it as an enum would be a lie, and a client that
    /// trusted it would refuse the very calls the feature exists for.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AllowedValuesAttribute : Attribute
    {
        public string[] Values { get; }

        /// <summary>True when the list is the complete set (emitted as `enum`); false when
        /// other values are also valid (emitted as `examples`).</summary>
        public bool Exhaustive { get; set; } = true;

        public AllowedValuesAttribute(params string[] values)
        {
            Values = values ?? Array.Empty<string>();
        }

        /// <summary>Takes the names from a C# enum, so the schema cannot drift from the type
        /// the tool actually parses into.</summary>
        public AllowedValuesAttribute(Type enumType)
        {
            Values = enumType != null && enumType.IsEnum ? Enum.GetNames(enumType) : Array.Empty<string>();
        }
    }
}
