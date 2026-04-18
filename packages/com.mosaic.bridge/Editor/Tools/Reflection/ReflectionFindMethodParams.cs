using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Reflection
{
    public sealed class ReflectionFindMethodParams
    {
        /// <summary>
        /// Fully-qualified type name to inspect (e.g. "UnityEngine.Application").
        /// </summary>
        [Required] public string TypeName { get; set; }

        /// <summary>
        /// Optional method name filter. If null/empty, all methods on the type are returned.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// When true, includes non-public methods in the results. Default false.
        /// </summary>
        public bool IncludePrivate { get; set; }
    }
}
