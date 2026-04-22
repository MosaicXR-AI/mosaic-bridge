using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public static class PrefabGetOverridesTool
    {
        [MosaicTool("prefab/get-overrides",
                    "Returns detailed override information for a prefab instance including property paths, current values, and source values",
                    isReadOnly: true)]
        public static ToolResult<PrefabGetOverridesResult> Execute(PrefabGetOverridesParams p)
        {
            // Resolve the target GameObject
            GameObject go = null;

            if (p.InstanceId.HasValue)
            {
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value) as GameObject;
                if (go == null)
                    return ToolResult<PrefabGetOverridesResult>.Fail(
                        $"No GameObject found with instanceId {p.InstanceId.Value}",
                        ErrorCodes.NOT_FOUND);
            }
            else if (!string.IsNullOrEmpty(p.GameObjectName))
            {
                go = GameObject.Find(p.GameObjectName);
                if (go == null)
                    return ToolResult<PrefabGetOverridesResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found",
                        ErrorCodes.NOT_FOUND);
            }
            else
            {
                return ToolResult<PrefabGetOverridesResult>.Fail(
                    "Either InstanceId or GameObjectName is required",
                    ErrorCodes.INVALID_PARAM);
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return ToolResult<PrefabGetOverridesResult>.Fail(
                    $"'{go.name}' is not a prefab instance",
                    ErrorCodes.INVALID_PARAM);

            // Source prefab path
            var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
            string sourcePath = sourcePrefab != null
                ? AssetDatabase.GetAssetPath(sourcePrefab) : null;

            var propertyOverrides = new List<OverrideDetail>();
            var addedComponents   = new List<AddedComponentEntry>();
            var removedComponents = new List<RemovedComponentEntry>();
            var addedGameObjects  = new List<AddedGameObjectEntry>();
            var removedGameObjects = new List<RemovedGameObjectEntry>();

            // --- Property overrides ---
            var objOverrides = PrefabUtility.GetObjectOverrides(go, includeDefaultOverrides: false);
            if (objOverrides != null)
            {
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

                        string currentVal = SerializedPropertyToString(prop);
                        string sourceVal  = null;

                        if (sourceSo != null)
                        {
                            var sourceProp = sourceSo.FindProperty(prop.propertyPath);
                            if (sourceProp != null)
                                sourceVal = SerializedPropertyToString(sourceProp);
                        }

                        propertyOverrides.Add(new OverrideDetail
                        {
                            ComponentType = ov.instanceObject.GetType().Name,
                            PropertyPath  = prop.propertyPath,
                            CurrentValue  = currentVal,
                            SourceValue   = sourceVal
                        });
                    }

                    sourceSo?.Dispose();
                    so.Dispose();
                }
            }

            // --- Added components ---
            var addedComps = PrefabUtility.GetAddedComponents(go);
            if (addedComps != null)
            {
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

            // --- Removed components ---
            var removedComps = PrefabUtility.GetRemovedComponents(go);
            if (removedComps != null)
            {
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

            // --- Added GameObjects ---
            var addedGOs = PrefabUtility.GetAddedGameObjects(go);
            if (addedGOs != null)
            {
                foreach (var ag in addedGOs)
                {
                    addedGameObjects.Add(new AddedGameObjectEntry
                    {
                        Name = ag.instanceGameObject != null
                            ? ag.instanceGameObject.name : "Unknown"
                    });
                }
            }

            // --- Removed GameObjects ---
            var removedGOs = PrefabUtility.GetRemovedGameObjects(go);
            if (removedGOs != null)
            {
                foreach (var rg in removedGOs)
                {
                    removedGameObjects.Add(new RemovedGameObjectEntry
                    {
                        Name = rg.assetGameObject != null
                            ? rg.assetGameObject.name : "Unknown"
                    });
                }
            }

            bool hasOverrides = propertyOverrides.Count > 0
                             || addedComponents.Count > 0
                             || removedComponents.Count > 0
                             || addedGameObjects.Count > 0
                             || removedGameObjects.Count > 0;

            return ToolResult<PrefabGetOverridesResult>.Ok(new PrefabGetOverridesResult
            {
                GameObjectName   = go.name,
                SourcePrefabPath = sourcePath,
                HasOverrides     = hasOverrides,
                PropertyOverrides = propertyOverrides,
                AddedComponents   = addedComponents,
                RemovedComponents = removedComponents,
                AddedGameObjects  = addedGameObjects,
                RemovedGameObjects = removedGameObjects
            });
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
