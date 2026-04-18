using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public static class PrefabInfoTool
    {
        [MosaicTool("prefab/info",
                    "Returns metadata about a prefab asset at the given project path",
                    isReadOnly: true)]
        public static ToolResult<PrefabInfoResult> Execute(PrefabInfoParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<PrefabInfoResult>.Fail(
                    "Path is required", ErrorCodes.INVALID_PARAM);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p.Path);
            if (asset == null)
                return ToolResult<PrefabInfoResult>.Fail(
                    $"Prefab not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            var assetType = PrefabUtility.GetPrefabAssetType(asset);
            string prefabTypeName;
            switch (assetType)
            {
                case PrefabAssetType.Regular:  prefabTypeName = "Regular"; break;
                case PrefabAssetType.Variant:  prefabTypeName = "Variant"; break;
                case PrefabAssetType.Model:    prefabTypeName = "Model";   break;
                default:                       prefabTypeName = assetType.ToString(); break;
            }

            string variantBasePath = null;
            if (assetType == PrefabAssetType.Variant)
            {
                var baseObj = PrefabUtility.GetCorrespondingObjectFromSource(asset);
                if (baseObj != null)
                    variantBasePath = AssetDatabase.GetAssetPath(baseObj);
            }

            var componentTypes = asset.GetComponents<Component>()
                .Select(c => c.GetType().Name)
                .ToList();

            // --- Override information ---
            int variantDepth = CalculateVariantDepth(asset);

            var propertyOverrides = new List<PropertyOverrideEntry>();
            var addedComponents   = new List<AddedComponentEntry>();
            var removedComponents = new List<RemovedComponentEntry>();
            var addedGameObjects  = new List<AddedGameObjectEntry>();
            var removedGameObjects = new List<RemovedGameObjectEntry>();

            bool hasOverrides = false;

            // Override inspection only applies to prefab instances in the scene,
            // but we can also inspect the variant asset itself for overrides relative to base.
            // For assets, use the asset root; for scene instances use the instance root.
            var root = asset;

            var objOverrides = PrefabUtility.GetObjectOverrides(root, includeDefaultOverrides: false);
            if (objOverrides != null && objOverrides.Count > 0)
            {
                hasOverrides = true;
                foreach (var ov in objOverrides)
                {
                    if (ov.instanceObject == null) continue;

                    var so = new SerializedObject(ov.instanceObject);
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(ov.instanceObject);
                    SerializedObject sourceSo = source != null ? new SerializedObject(source) : null;

                    var prop = so.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        if (!prop.prefabOverride) continue;

                        string valueStr = SerializedPropertyToString(prop);

                        propertyOverrides.Add(new PropertyOverrideEntry
                        {
                            ComponentType = ov.instanceObject.GetType().Name,
                            PropertyPath  = prop.propertyPath,
                            ModifiedValue = valueStr
                        });
                    }

                    sourceSo?.Dispose();
                    so.Dispose();
                }
            }

            var addedComps = PrefabUtility.GetAddedComponents(root);
            if (addedComps != null && addedComps.Count > 0)
            {
                hasOverrides = true;
                foreach (var ac in addedComps)
                {
                    addedComponents.Add(new AddedComponentEntry
                    {
                        ComponentType  = ac.instanceComponent != null
                            ? ac.instanceComponent.GetType().Name : "Unknown",
                        GameObjectName = ac.instanceComponent != null
                            ? ac.instanceComponent.gameObject.name : "Unknown"
                    });
                }
            }

            var removedComps = PrefabUtility.GetRemovedComponents(root);
            if (removedComps != null && removedComps.Count > 0)
            {
                hasOverrides = true;
                foreach (var rc in removedComps)
                {
                    removedComponents.Add(new RemovedComponentEntry
                    {
                        ComponentType  = rc.assetComponent != null
                            ? rc.assetComponent.GetType().Name : "Unknown",
                        GameObjectName = rc.containingInstanceGameObject != null
                            ? rc.containingInstanceGameObject.name : "Unknown"
                    });
                }
            }

            var addedGOs = PrefabUtility.GetAddedGameObjects(root);
            if (addedGOs != null && addedGOs.Count > 0)
            {
                hasOverrides = true;
                foreach (var ag in addedGOs)
                {
                    addedGameObjects.Add(new AddedGameObjectEntry
                    {
                        Name = ag.instanceGameObject != null
                            ? ag.instanceGameObject.name : "Unknown"
                    });
                }
            }

            var removedGOs = PrefabUtility.GetRemovedGameObjects(root);
            if (removedGOs != null && removedGOs.Count > 0)
            {
                hasOverrides = true;
                foreach (var rg in removedGOs)
                {
                    removedGameObjects.Add(new RemovedGameObjectEntry
                    {
                        Name = rg.assetGameObject != null
                            ? rg.assetGameObject.name : "Unknown"
                    });
                }
            }

            return ToolResult<PrefabInfoResult>.Ok(new PrefabInfoResult
            {
                Path              = p.Path,
                Name              = asset.name,
                Guid              = guid,
                PrefabType        = prefabTypeName,
                VariantBasePath   = variantBasePath,
                ComponentTypes    = componentTypes,
                ChildCount        = asset.transform.childCount,
                HasOverrides      = hasOverrides,
                VariantDepth      = variantDepth,
                PropertyOverrides = propertyOverrides,
                AddedComponents   = addedComponents,
                RemovedComponents = removedComponents,
                AddedGameObjects  = addedGameObjects,
                RemovedGameObjects = removedGameObjects
            });
        }

        /// <summary>
        /// Walks the variant chain to determine nesting depth.
        /// 0 = regular prefab, 1 = direct variant, 2+ = variant of variant, etc.
        /// </summary>
        static int CalculateVariantDepth(GameObject asset)
        {
            int depth = 0;
            var current = asset;
            while (PrefabUtility.GetPrefabAssetType(current) == PrefabAssetType.Variant)
            {
                depth++;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(current);
                if (source == null || source == current) break;
                current = source;
            }
            return depth;
        }

        /// <summary>
        /// Converts a SerializedProperty value to a human-readable string.
        /// </summary>
        static string SerializedPropertyToString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:       return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:       return prop.boolValue.ToString();
                case SerializedPropertyType.Float:         return prop.floatValue.ToString("G");
                case SerializedPropertyType.String:        return prop.stringValue;
                case SerializedPropertyType.Color:         return prop.colorValue.ToString();
                case SerializedPropertyType.Vector2:       return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:       return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:       return prop.vector4Value.ToString();
                case SerializedPropertyType.Quaternion:    return prop.quaternionValue.ToString();
                case SerializedPropertyType.Enum:          return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                                                              ? prop.enumDisplayNames[prop.enumValueIndex]
                                                              : prop.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})"
                        : "null";
                case SerializedPropertyType.Rect:          return prop.rectValue.ToString();
                case SerializedPropertyType.Bounds:        return prop.boundsValue.ToString();
                case SerializedPropertyType.AnimationCurve: return prop.animationCurveValue != null
                    ? $"AnimationCurve({prop.animationCurveValue.length} keys)"
                    : "null";
                default: return $"({prop.propertyType})";
            }
        }
    }
}
