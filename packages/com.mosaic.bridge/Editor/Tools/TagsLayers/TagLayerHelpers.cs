using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Tools.TagsLayers
{
    /// <summary>
    /// Shared helpers for tag/layer/static tools: GameObject lookup and flag parsing.
    /// </summary>
    internal static class TagLayerHelpers
    {
        /// <summary>
        /// Finds a GameObject by InstanceId (preferred) or by Name fallback.
        /// Returns null if neither resolves.
        /// </summary>
        public static GameObject FindGameObject(int? instanceId, string name)
        {
            if (instanceId.HasValue)
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
                if (obj != null) return obj;
            }

            if (!string.IsNullOrEmpty(name))
                return GameObject.Find(name);

            return null;
        }

        /// <summary>
        /// Parses a comma-separated string of StaticEditorFlags names into a flags enum value.
        /// E.g. "BatchingStatic, OccludeeStatic" → StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic
        /// Also supports "Everything" and "Nothing" (0).
        /// </summary>
        public static bool TryParseStaticFlags(string flagsString, out StaticEditorFlags result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(flagsString))
                return false;

            string trimmed = flagsString.Trim();

            if (string.Equals(trimmed, "Nothing", System.StringComparison.OrdinalIgnoreCase))
            {
                result = 0;
                return true;
            }

            if (string.Equals(trimmed, "Everything", System.StringComparison.OrdinalIgnoreCase))
            {
                result = (StaticEditorFlags)(-1); // All bits set
                return true;
            }

            string[] parts = trimmed.Split(',');
            foreach (string part in parts)
            {
                string flag = part.Trim();
                if (string.IsNullOrEmpty(flag)) continue;

                if (System.Enum.TryParse<StaticEditorFlags>(flag, ignoreCase: true, out var parsed))
                {
                    result |= parsed;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Converts StaticEditorFlags to a human-readable comma-separated string.
        /// </summary>
        public static string StaticFlagsToString(StaticEditorFlags flags)
        {
            if (flags == 0) return "Nothing";
            if (flags == (StaticEditorFlags)(-1)) return "Everything";
            return flags.ToString();
        }
    }
}
