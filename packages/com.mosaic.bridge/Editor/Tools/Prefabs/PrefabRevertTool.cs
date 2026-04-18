using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public static class PrefabRevertTool
    {
        [MosaicTool("prefab/revert",
                    "Reverts all overrides on a prefab instance back to the source prefab values",
                    isReadOnly: false)]
        public static ToolResult<PrefabRevertResult> Execute(PrefabRevertParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<PrefabRevertResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<PrefabRevertResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return ToolResult<PrefabRevertResult>.Fail(
                    "Not a prefab instance", ErrorCodes.INVALID_PARAM);

            // Collect override details before reverting
            var revertedPaths       = new List<string>();
            var revertedAddedComp   = new List<string>();
            var revertedRemovedComp = new List<string>();
            var revertedAddedGOs    = new List<string>();
            var revertedRemovedGOs  = new List<string>();

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
                        revertedPaths.Add($"{ov.instanceObject.GetType().Name}.{prop.propertyPath}");
                    }
                    so.Dispose();
                }
            }

            var addedComps = PrefabUtility.GetAddedComponents(go);
            if (addedComps != null)
            {
                foreach (var ac in addedComps)
                {
                    revertedAddedComp.Add(ac.instanceComponent != null
                        ? ac.instanceComponent.GetType().Name : "Unknown");
                }
            }

            var removedComps = PrefabUtility.GetRemovedComponents(go);
            if (removedComps != null)
            {
                foreach (var rc in removedComps)
                {
                    revertedRemovedComp.Add(rc.assetComponent != null
                        ? rc.assetComponent.GetType().Name : "Unknown");
                }
            }

            var addedGOs = PrefabUtility.GetAddedGameObjects(go);
            if (addedGOs != null)
            {
                foreach (var ag in addedGOs)
                {
                    revertedAddedGOs.Add(ag.instanceGameObject != null
                        ? ag.instanceGameObject.name : "Unknown");
                }
            }

            var removedGOs = PrefabUtility.GetRemovedGameObjects(go);
            if (removedGOs != null)
            {
                foreach (var rg in removedGOs)
                {
                    revertedRemovedGOs.Add(rg.assetGameObject != null
                        ? rg.assetGameObject.name : "Unknown");
                }
            }

            int totalCount = revertedPaths.Count
                           + revertedAddedComp.Count
                           + revertedRemovedComp.Count
                           + revertedAddedGOs.Count
                           + revertedRemovedGOs.Count;

            var mode = p.Mode == "user"
                ? InteractionMode.UserAction
                : InteractionMode.AutomatedAction;

            PrefabUtility.RevertPrefabInstance(go, mode);

            return ToolResult<PrefabRevertResult>.Ok(new PrefabRevertResult
            {
                GameObjectName             = go.name,
                Message                    = $"Reverted {totalCount} override(s) to source prefab values",
                RevertedCount              = totalCount,
                RevertedPropertyPaths      = revertedPaths,
                RevertedAddedComponents    = revertedAddedComp,
                RevertedRemovedComponents  = revertedRemovedComp,
                RevertedAddedGameObjects   = revertedAddedGOs,
                RevertedRemovedGameObjects = revertedRemovedGOs
            });
        }
    }
}
