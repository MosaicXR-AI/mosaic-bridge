using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Core.Discovery
{
    public sealed class ToolRegistryEntry
    {
        public string ToolName { get; }       // "mosaic_gameobject/create"
        public string Description { get; }
        public string Category { get; }
        public bool IsReadOnly { get; }
        public ToolContext Context { get; }
        public MethodInfo Method { get; }
        public System.Type ParamType { get; } // first parameter type; null if method takes no params

        public ToolRegistryEntry(string toolName, MosaicToolAttribute attr,
                                 MethodInfo method, System.Type paramType)
        {
            ToolName = toolName;
            Description = attr.Description;
            Category = attr.Category;
            IsReadOnly = attr.IsReadOnly;
            Context = attr.Context;
            Method = method;
            ParamType = paramType;
        }
    }
}
