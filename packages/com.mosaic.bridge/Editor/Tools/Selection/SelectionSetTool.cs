using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Selection
{
    public static class SelectionSetTool
    {
        [MosaicTool("selection/set",
                    "Sets the current editor selection. Accepts any combination of: InstanceIds, AssetPaths, Name, Names. Provide at least one.",
                    isReadOnly: false)]
        public static ToolResult<SelectionSetResult> Set(SelectionSetParams p)
        {
            bool hasInstanceIds = p.InstanceIds != null && p.InstanceIds.Length > 0;
            bool hasAssetPaths  = p.AssetPaths  != null && p.AssetPaths.Length  > 0;
            bool hasName        = !string.IsNullOrEmpty(p.Name);
            bool hasNames       = p.Names != null && p.Names.Length > 0;

            if (!hasInstanceIds && !hasAssetPaths && !hasName && !hasNames)
                return ToolResult<SelectionSetResult>.Fail(
                    "At least one of InstanceIds, AssetPaths, Name, or Names must be provided",
                    ErrorCodes.INVALID_PARAM);

            var resolved  = new List<Object>();
            var warnings  = new List<string>();

            if (hasInstanceIds)
            {
                foreach (var id in p.InstanceIds)
                {
#pragma warning disable CS0618
                    var obj = UnityEngine.Resources.EntityIdToObject(id);
#pragma warning restore CS0618
                    if (obj == null)
                        warnings.Add($"Instance ID {id} could not be resolved and was skipped");
                    else
                        resolved.Add(obj);
                }
            }

            if (hasAssetPaths)
            {
                foreach (var path in p.AssetPaths)
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj == null)
                        warnings.Add($"Asset path '{path}' could not be resolved and was skipped");
                    else
                        resolved.Add(obj);
                }
            }

            // Convenience: resolve Name / Names via scene hierarchy search.
            if (hasName || hasNames)
            {
                var namesToFind = new List<string>();
                if (hasName) namesToFind.Add(p.Name);
                if (hasNames) namesToFind.AddRange(p.Names);

                // Fetch once per call — avoid O(n^2) scans when multiple names are supplied.
                var allGameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var name in namesToFind)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    int matches = 0;
                    foreach (var go in allGameObjects)
                    {
                        if (go.name == name)
                        {
                            resolved.Add(go);
                            matches++;
                        }
                    }
                    if (matches == 0)
                        warnings.Add($"GameObject named '{name}' not found in scene — skipped");
                }
            }

            UnityEditor.Selection.objects = resolved.ToArray();

            var names = new string[resolved.Count];
            for (int i = 0; i < resolved.Count; i++)
                names[i] = resolved[i].name;

            var result = new SelectionSetResult
            {
                Count = resolved.Count,
                Names = names
            };

            if (warnings.Count > 0)
                return ToolResult<SelectionSetResult>.OkWithWarnings(result, warnings.ToArray());

            return ToolResult<SelectionSetResult>.Ok(result);
        }
    }
}
