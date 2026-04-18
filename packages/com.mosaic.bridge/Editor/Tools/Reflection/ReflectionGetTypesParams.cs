namespace Mosaic.Bridge.Tools.Reflection
{
    public sealed class ReflectionGetTypesParams
    {
        /// <summary>
        /// Optional assembly name filter (contains match). Only assemblies whose name
        /// contains this string are searched.
        /// </summary>
        public string AssemblyFilter { get; set; }

        /// <summary>
        /// Optional base type filter. Only types that derive from this type (or implement
        /// this interface) are returned. Must be a fully-qualified type name.
        /// </summary>
        public string BaseType { get; set; }

        /// <summary>
        /// Optional type name filter (contains match on FullName).
        /// </summary>
        public string NameFilter { get; set; }
    }
}
