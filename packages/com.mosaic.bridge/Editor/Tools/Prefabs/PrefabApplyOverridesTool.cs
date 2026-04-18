using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public static class PrefabApplyOverridesTool
    {
        [MosaicTool("prefab/apply-overrides",
                    "Applies all prefab instance overrides back to the source prefab asset",
                    isReadOnly: false)]
        public static ToolResult<PrefabApplyOverridesResult> Execute(PrefabApplyOverridesParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<PrefabApplyOverridesResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<PrefabApplyOverridesResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return ToolResult<PrefabApplyOverridesResult>.Fail(
                    "GameObject is not a prefab instance", ErrorCodes.INVALID_PARAM);

            // Collect override details before applying
            var appliedPaths     = new List<string>();
            var appliedAddedComp = new List<string>();
            var breakdown        = new ApplyCountBreakdown();

            var objOverrides = PrefabUtility.GetObjectOverrides(go, includeDefaultOverrides: false);
            if (objOverrides != null)
            {
                foreach (var ov in objOverrides)
                {
                    if (ov.instanceObject == null) continue;
                    var so = new SerializedObject(ov.instanceObject);
                    var prop = so.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        if (!prop.prefabOverride) continue;
                        appliedPaths.Add($"{ov.instanceObject.GetType().Name}.{prop.propertyPath}");
                        breakdown.PropertyOverrides++;
                    }
                    so.Dispose();
                }
            }

            var addedComps = PrefabUtility.GetAddedComponents(go);
            if (addedComps != null)
            {
                foreach (var ac in addedComps)
                {
                    string typeName = ac.instanceComponent != null
                        ? ac.instanceComponent.GetType().Name : "Unknown";
                    appliedAddedComp.Add(typeName);
                    breakdown.AddedComponents++;
                }
            }

            var removedComps = PrefabUtility.GetRemovedComponents(go);
            if (removedComps != null)
                breakdown.RemovedComponents = removedComps.Count;

            var addedGOs = PrefabUtility.GetAddedGameObjects(go);
            if (addedGOs != null)
                breakdown.AddedGameObjects = addedGOs.Count;

            var removedGOs = PrefabUtility.GetRemovedGameObjects(go);
            if (removedGOs != null)
                breakdown.RemovedGameObjects = removedGOs.Count;

            int totalCount = breakdown.PropertyOverrides
                           + breakdown.AddedComponents
                           + breakdown.RemovedComponents
                           + breakdown.AddedGameObjects
                           + breakdown.RemovedGameObjects;

            var mode = p.Mode == "user"
                ? InteractionMode.UserAction
                : InteractionMode.AutomatedAction;

            PrefabUtility.ApplyPrefabInstance(go, mode);

            return ToolResult<PrefabApplyOverridesResult>.Ok(new PrefabApplyOverridesResult
            {
                GameObjectName       = go.name,
                AppliedCount         = totalCount,
                Message              = $"Applied {totalCount} override(s) to source prefab",
                AppliedPropertyPaths = appliedPaths,
                AppliedAddedComponents = appliedAddedComp,
                Breakdown            = breakdown
            });
        }
    }
}
