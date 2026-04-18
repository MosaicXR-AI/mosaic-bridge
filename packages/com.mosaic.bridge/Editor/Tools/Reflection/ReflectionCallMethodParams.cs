using Mosaic.Bridge.Contracts.Attributes;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.Reflection
{
    public sealed class ReflectionCallMethodParams
    {
        /// <summary>
        /// Fully-qualified type name (e.g. "UnityEngine.Application").
        /// </summary>
        [Required] public string TypeName { get; set; }

        /// <summary>
        /// Name of the method to invoke.
        /// </summary>
        [Required] public string MethodName { get; set; }

        /// <summary>
        /// JSON array of arguments to pass. Each element is converted to the target parameter type.
        /// </summary>
        public JArray Arguments { get; set; }

        /// <summary>
        /// When true, allows invocation of non-public methods. Default false.
        /// </summary>
        public bool AllowPrivate { get; set; }

        /// <summary>
        /// Optional GameObject hierarchy path (e.g. "/Canvas/Panel") for instance method calls.
        /// If set, the method is invoked on a component of the specified type found on that GameObject.
        /// </summary>
        public string InstancePath { get; set; }
    }
}
