using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Reflection
{
    public sealed class ReflectionGetTypesResult
    {
        public int TypeCount { get; set; }
        public List<TypeEntry> Types { get; set; }

        public sealed class TypeEntry
        {
            public string FullName { get; set; }
            public string AssemblyName { get; set; }
            public string BaseType { get; set; }
            public bool IsAbstract { get; set; }
            public bool IsInterface { get; set; }
            public bool IsEnum { get; set; }
        }
    }
}
