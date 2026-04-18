using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Selection
{
    public static class SelectionGetTool
    {
        [MosaicTool("selection/get",
                    "Returns the current editor selection as a list of objects with names, instance IDs, and paths",
                    isReadOnly: true)]
        public static ToolResult<SelectionGetResult> Get(SelectionGetParams p)
        {
            var selected = UnityEditor.Selection.objects;
            var objects = new List<SelectedObject>();

            foreach (var obj in selected)
            {
                if (obj == null) continue;

                var go = obj as GameObject;
                bool isGameObject = go != null;
                string hierarchyPath = null;
                string assetPath = null;

                if (isGameObject && go.scene.IsValid())
                {
                    hierarchyPath = GetHierarchyPath(go.transform);
                }
                else
                {
                    assetPath = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(assetPath))
                        assetPath = null;
                }

                objects.Add(new SelectedObject
                {
                    Name = obj.name,
                    InstanceId = obj.GetInstanceID(),
                    HierarchyPath = hierarchyPath,
                    AssetPath = assetPath,
                    IsGameObject = isGameObject
                });
            }

            return ToolResult<SelectionGetResult>.Ok(new SelectionGetResult
            {
                Objects = objects,
                Count = objects.Count
            });
        }

        private static string GetHierarchyPath(Transform t)
        {
            var path = t.name;
            var current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
