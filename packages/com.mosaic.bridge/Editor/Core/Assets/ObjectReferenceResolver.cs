using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Core.Assets
{
    /// <summary>
    /// O4 §3.1: one place that resolves a caller-supplied path into the Unity Object an
    /// ObjectReference field actually wants, instead of every route re-deriving its own version of
    /// "load something and hope it fits." Handles sub-asset addressing (`Assets/sheet.png#Run_03`),
    /// a Sprite field pointed at its parent texture, and a prefab/asset path pointed at a Component
    /// field — and, critically, fails with a specific reason on a type mismatch rather than handing
    /// back an Object the caller's field cannot actually hold (Unity's own
    /// SerializedProperty.objectReferenceValue setter does not validate this for you).
    /// </summary>
    public static class ObjectReferenceResolver
    {
        /// <summary>Splits "Assets/sheet.png#Run_03" into ("Assets/sheet.png", "Run_03"). No '#'
        /// means no sub-asset — subAssetName comes back null.</summary>
        public static void SplitSubAsset(string targetPath, out string assetPath, out string subAssetName)
        {
            var hash = targetPath?.IndexOf('#') ?? -1;
            if (hash < 0)
            {
                assetPath = targetPath;
                subAssetName = null;
                return;
            }
            assetPath = targetPath.Substring(0, hash);
            subAssetName = targetPath.Substring(hash + 1);
        }

        /// <summary>Reads the real field type off a SerializedProperty's own type string
        /// ("PPtr&lt;$Rigidbody&gt;"), resolved to a System.Type via the caller's own type lookup
        /// (every tool in this codebase already has one). Null if the property isn't an
        /// ObjectReference or its type name can't be resolved.</summary>
        public static Type ResolveFieldType(SerializedProperty prop, Func<string, Type> lookupTypeByName)
        {
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) return null;
            var t = prop.type;
            const string prefix = "PPtr<$";
            if (string.IsNullOrEmpty(t) || !t.StartsWith(prefix, StringComparison.Ordinal) || !t.EndsWith(">", StringComparison.Ordinal))
                return null;
            var name = t.Substring(prefix.Length, t.Length - prefix.Length - 1);
            if (name == "Object") return typeof(UnityEngine.Object);
            return lookupTypeByName?.Invoke(name);
        }

        /// <summary>
        /// Resolves an asset-path-shaped target (optionally with a #subAssetName suffix) to an
        /// Object assignable to fieldType. Returns false with a specific `error` on any mismatch —
        /// asset not found, sub-asset not found, wrong type, no Sprite/Component at the path —
        /// instead of returning null and leaving the caller to report success anyway.
        /// </summary>
        public static bool TryResolveAsset(string targetPath, Type fieldType, out UnityEngine.Object resolved, out string error)
        {
            resolved = null;
            error = null;
            if (string.IsNullOrEmpty(targetPath))
            {
                error = "Target asset path is empty";
                return false;
            }

            var effectiveType = fieldType ?? typeof(UnityEngine.Object);
            SplitSubAsset(targetPath, out var assetPath, out var subAssetName);

            if (!string.IsNullOrEmpty(subAssetName))
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var match = all?.FirstOrDefault(o => o != null && o.name == subAssetName && effectiveType.IsInstanceOfType(o));
                if (match == null)
                {
                    var available = all != null && all.Length > 0
                        ? " Available sub-assets: " + string.Join(", ", all.Where(o => o != null).Select(o => $"{o.name} ({o.GetType().Name})"))
                        : " (no sub-assets found at that path)";
                    error = $"No sub-asset named '{subAssetName}' of type '{effectiveType.Name}' at '{assetPath}'.{available}";
                    return false;
                }
                resolved = match;
                return true;
            }

            // A Sprite field pointed at the texture that contains it: fall back to the texture's
            // own sub-sprites rather than fail — the caller almost always means "the sprite sheet",
            // not a specific slice, when it hasn't named one.
            if (typeof(Sprite).IsAssignableFrom(effectiveType))
            {
                var direct = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (direct != null)
                {
                    resolved = direct;
                    return true;
                }
                var subs = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
                if (subs.Length > 0)
                {
                    resolved = subs[0];
                    return true;
                }
                error = $"'{assetPath}' has no Sprite asset and no sliced sub-sprites, but the field expects a Sprite.";
                return false;
            }

            var loaded = AssetDatabase.LoadAssetAtPath(assetPath, effectiveType);
            if (loaded != null)
            {
                resolved = loaded;
                return true;
            }

            // effectiveType may be more specific than the asset's own type (e.g. a Component on a
            // prefab) — load generically to give a precise mismatch reason instead of a bare
            // "not found" when the path itself is fine.
            var generic = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (generic == null)
            {
                error = $"Asset not found: '{assetPath}'";
                return false;
            }

            if (generic is GameObject go && typeof(Component).IsAssignableFrom(effectiveType))
            {
                var comp = go.GetComponent(effectiveType);
                if (comp != null)
                {
                    resolved = comp;
                    return true;
                }
                error = $"'{assetPath}' has no component of type '{effectiveType.Name}'.";
                return false;
            }

            error = $"Asset at '{assetPath}' is a '{generic.GetType().Name}', not compatible with the expected type '{effectiveType.Name}'.";
            return false;
        }
    }
}
