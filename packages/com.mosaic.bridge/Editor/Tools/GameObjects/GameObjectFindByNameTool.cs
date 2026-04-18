using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectFindByNameTool
    {
        [MosaicTool("gameobject/find_by_name",
                    "Finds GameObjects by name; supports exact and partial (contains) matching",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<GameObjectFindByNameResult> FindByName(GameObjectFindByNameParams p)
        {
            var matches = new List<GameObjectRef>();

            if (p.ExactMatch)
            {
                var go = GameObject.Find(p.Name);
                if (go != null)
                {
                    matches.Add(new GameObjectRef
                    {
                        InstanceId    = go.GetInstanceID(),
                        Name          = go.name,
                        HierarchyPath = GameObjectToolHelpers.GetHierarchyPath(go.transform)
                    });
                }
            }
            else
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in all)
                {
                    // Exclude assets — only include scene objects
                    if (!go.scene.IsValid())
                        continue;

                    if (go.name.Contains(p.Name))
                    {
                        matches.Add(new GameObjectRef
                        {
                            InstanceId    = go.GetInstanceID(),
                            Name          = go.name,
                            HierarchyPath = GameObjectToolHelpers.GetHierarchyPath(go.transform)
                        });
                    }
                }
            }

            return ToolResult<GameObjectFindByNameResult>.Ok(new GameObjectFindByNameResult
            {
                Matches = matches
            });
        }
    }
}
