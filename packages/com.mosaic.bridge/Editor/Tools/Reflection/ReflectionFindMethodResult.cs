using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Reflection
{
    public sealed class ReflectionFindMethodResult
    {
        public string TypeName { get; set; }
        public int MethodCount { get; set; }
        public List<MethodEntry> Methods { get; set; }

        public sealed class MethodEntry
        {
            public string Name { get; set; }
            public string ReturnType { get; set; }
            public List<ParameterEntry> Parameters { get; set; }
            public bool IsStatic { get; set; }
            public bool IsPublic { get; set; }
            public string DeclaringType { get; set; }
        }

        public sealed class ParameterEntry
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }
    }
}
