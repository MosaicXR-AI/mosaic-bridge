using System;
using System.Collections.Generic;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Discovery
{
    /// <summary>
    /// Generates JSON Schema objects from C# parameter classes via reflection.
    /// Used by the /tools endpoint to provide accurate inputSchema for MCP clients.
    /// Handles recursive types (e.g. TreeNodeDef.Children: TreeNodeDef[]) via cycle detection.
    /// </summary>
    public static class JsonSchemaGenerator
    {
        /// <summary>
        /// Generates a JSON Schema object for the given parameter type.
        /// Returns a minimal schema for null types (tools with no parameters).
        /// </summary>
        public static JObject Generate(Type paramType)
        {
            return Generate(paramType, new HashSet<Type>());
        }

        private static JObject Generate(Type paramType, HashSet<Type> visited)
        {
            if (paramType == null)
            {
                return new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["additionalProperties"] = false
                };
            }

            // Cycle detection — if we've already seen this type, return a reference stub
            if (!visited.Add(paramType))
            {
                return new JObject
                {
                    ["type"] = "object",
                    ["description"] = $"(recursive reference to {paramType.Name})"
                };
            }

            var properties = new JObject();
            var required = new JArray();

            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;

                var schema = TypeToSchema(prop.PropertyType, visited);
                properties[ToCamelCase(prop.Name)] = schema;

                if (prop.GetCustomAttribute<RequiredAttribute>() != null)
                {
                    required.Add(ToCamelCase(prop.Name));
                }
            }

            var result = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (required.Count > 0)
                result["required"] = required;

            result["additionalProperties"] = false;

            // Remove from visited so the same type can appear in different branches
            visited.Remove(paramType);

            return result;
        }

        private static JObject TypeToSchema(Type type, HashSet<Type> visited)
        {
            // Handle nullable types
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                type = underlying;

            // String
            if (type == typeof(string))
                return new JObject { ["type"] = "string" };

            // Boolean
            if (type == typeof(bool))
                return new JObject { ["type"] = "boolean" };

            // Integer types
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                return new JObject { ["type"] = "integer" };

            // Floating point types
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return new JObject { ["type"] = "number" };

            // Dictionary types — accept any JSON object
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return new JObject { ["type"] = "object" };

            // Arrays and lists
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = TypeToSchema(elementType, visited)
                };
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = TypeToSchema(elementType, visited)
                };
            }

            // JToken / JObject / JArray — accept any JSON
            if (typeof(JToken).IsAssignableFrom(type))
                return new JObject { };

            // Enum types
            if (type.IsEnum)
            {
                var values = new JArray();
                foreach (var name in Enum.GetNames(type))
                    values.Add(name);
                return new JObject { ["type"] = "string", ["enum"] = values };
            }

            // Nested object types (with cycle detection)
            if (type.IsClass || type.IsValueType)
                return Generate(type, visited);

            // Fallback
            return new JObject { };
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.Length == 1) return name.ToLowerInvariant();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
