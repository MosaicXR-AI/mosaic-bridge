using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    /// <summary>
    /// Helper for reading and writing ShaderGraph .shadergraph JSON files directly.
    /// ShaderGraph files are JSON on disk, making them parseable without reflection
    /// into Unity's internal ShaderGraph APIs.
    /// </summary>
    internal static class ShaderGraphJsonHelper
    {
        /// <summary>Parse a .shadergraph file and return the root JObject.</summary>
        public static JObject ReadGraph(string assetPath)
        {
            string fullPath = GetFullPath(assetPath);
            if (!File.Exists(fullPath))
                return null;

            string json = File.ReadAllText(fullPath);
            return JObject.Parse(json);
        }

        /// <summary>Write a JObject back to a .shadergraph file.</summary>
        public static void WriteGraph(string assetPath, JObject graph)
        {
            string fullPath = GetFullPath(assetPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, graph.ToString(Newtonsoft.Json.Formatting.Indented));
        }

        /// <summary>Count nodes in the graph JSON.</summary>
        public static int CountNodes(JObject graph)
        {
            var nodes = graph?["m_SerializedNodes"] as JArray
                     ?? graph?["m_Nodes"] as JArray
                     ?? graph?["nodes"] as JArray;
            return nodes?.Count ?? 0;
        }

        /// <summary>Count edges in the graph JSON.</summary>
        public static int CountEdges(JObject graph)
        {
            var edges = graph?["m_SerializedEdges"] as JArray
                     ?? graph?["m_Edges"] as JArray
                     ?? graph?["edges"] as JArray;
            return edges?.Count ?? 0;
        }

        /// <summary>Extract property definitions from the graph JSON.</summary>
        public static List<ShaderGraphProperty> ExtractProperties(JObject graph)
        {
            var result = new List<ShaderGraphProperty>();

            var properties = graph?["m_SerializedProperties"] as JArray
                          ?? graph?["m_Properties"] as JArray
                          ?? graph?["properties"] as JArray;

            if (properties == null)
                return result;

            foreach (var prop in properties)
            {
                // Each property may be serialized as a JSON string that itself is JSON,
                // or as a direct object. Handle both cases.
                JObject propObj = null;
                if (prop.Type == JTokenType.String)
                {
                    try { propObj = JObject.Parse(prop.Value<string>()); }
                    catch { continue; }
                }
                else if (prop.Type == JTokenType.Object)
                {
                    propObj = (JObject)prop;
                }

                if (propObj == null) continue;

                var name = propObj["m_Name"]?.Value<string>()
                        ?? propObj["m_DisplayName"]?.Value<string>()
                        ?? propObj["name"]?.Value<string>()
                        ?? "Unknown";

                var refName = propObj["m_OverrideReferenceName"]?.Value<string>()
                           ?? propObj["m_ReferenceName"]?.Value<string>()
                           ?? propObj["referenceName"]?.Value<string>()
                           ?? "";

                var type = propObj["m_Type"]?.Value<string>()
                        ?? propObj["type"]?.Value<string>()
                        ?? propObj["$type"]?.Value<string>()
                        ?? "Unknown";

                // Extract a short type name from fully qualified type if needed
                if (type.Contains(","))
                    type = type.Substring(0, type.IndexOf(','));
                if (type.Contains("."))
                    type = type.Substring(type.LastIndexOf('.') + 1);

                var defaultValue = propObj["m_Value"]?.ToString()
                                ?? propObj["m_DefaultValue"]?.ToString()
                                ?? propObj["value"]?.ToString()
                                ?? "";

                result.Add(new ShaderGraphProperty
                {
                    Name = name,
                    ReferenceName = refName,
                    Type = type,
                    DefaultValue = defaultValue
                });
            }

            return result;
        }

        /// <summary>
        /// Find a property by name in the graph and set its default value.
        /// Returns the old value string, or null if property not found.
        /// </summary>
        public static string SetPropertyDefault(JObject graph, string propertyName, string newValueJson)
        {
            var properties = graph?["m_SerializedProperties"] as JArray
                          ?? graph?["m_Properties"] as JArray
                          ?? graph?["properties"] as JArray;

            if (properties == null)
                return null;

            for (int i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                JObject propObj = null;
                bool isSerialized = false;

                if (prop.Type == JTokenType.String)
                {
                    try
                    {
                        propObj = JObject.Parse(prop.Value<string>());
                        isSerialized = true;
                    }
                    catch { continue; }
                }
                else if (prop.Type == JTokenType.Object)
                {
                    propObj = (JObject)prop;
                }

                if (propObj == null) continue;

                var name = propObj["m_Name"]?.Value<string>()
                        ?? propObj["m_DisplayName"]?.Value<string>()
                        ?? propObj["name"]?.Value<string>();

                if (name != propertyName) continue;

                // Found the property — get old value
                string oldValue = propObj["m_Value"]?.ToString()
                               ?? propObj["m_DefaultValue"]?.ToString()
                               ?? propObj["value"]?.ToString()
                               ?? "";

                // Set new value
                JToken newToken;
                try { newToken = JToken.Parse(newValueJson); }
                catch { newToken = new JValue(newValueJson); }

                if (propObj.ContainsKey("m_Value"))
                    propObj["m_Value"] = newToken;
                else if (propObj.ContainsKey("m_DefaultValue"))
                    propObj["m_DefaultValue"] = newToken;
                else if (propObj.ContainsKey("value"))
                    propObj["value"] = newToken;
                else
                    propObj["m_Value"] = newToken;

                // Write back if it was a serialized string
                if (isSerialized)
                    properties[i] = new JValue(propObj.ToString(Newtonsoft.Json.Formatting.None));

                return oldValue;
            }

            return null;
        }

        /// <summary>Convert asset-relative path to absolute filesystem path.</summary>
        internal static string GetFullPath(string assetPath)
        {
            return Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", assetPath));
        }
    }
}
