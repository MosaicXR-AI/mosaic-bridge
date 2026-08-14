using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectGetInfoTool
    {
        [MosaicTool("gameobject/get_info",
                    "Returns detailed information about a GameObject in the currently open scene",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<GameObjectGetInfoResult> GetInfo(GameObjectGetInfoParams p)
        {
            // Find() only sees ACTIVE objects in the loaded scene. During Play Mode a caller may
            // well be asking about something inactive, and "not found" would be a lie.
            var go = GameObject.Find(p.Name) ?? FindIncludingInactive(p.Name);
            if (go == null)
                return ToolResult<GameObjectGetInfoResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            var rawComponents = go.GetComponents<Component>();
            var componentNames = new List<string>(rawComponents.Length);
            foreach (var c in rawComponents)
            {
                if (c != null)
                    componentNames.Add(c.GetType().Name);
            }

            return ToolResult<GameObjectGetInfoResult>.Ok(new GameObjectGetInfoResult
            {
                InstanceId        = UnityIds.Of(go),
                Name              = go.name,
                HierarchyPath     = GameObjectToolHelpers.GetHierarchyPath(go.transform),
                ActiveSelf        = go.activeSelf,
                ActiveInHierarchy = go.activeInHierarchy,
                Components        = componentNames.ToArray(),
                Tag               = go.tag,
                Layer             = LayerMask.LayerToName(go.layer),
                ChildCount        = go.transform.childCount,
                Position          = Xyz(go.transform.position),
                LocalPosition     = Xyz(go.transform.localPosition),
                Rotation          = Xyz(go.transform.eulerAngles),
                LocalScale        = Xyz(go.transform.localScale)
            });
        }

        // Arrays rather than a Vector3: the JSON schema generator describes float[] plainly, and a
        // caller comparing two samples wants numbers it can subtract without knowing Unity's types.
        private static float[] Xyz(Vector3 v) => new[] { v.x, v.y, v.z };

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.name != name) continue;
                if (go.hideFlags != HideFlags.None) continue;
                // IsValid(), not scene.name: an UNTITLED scene has an empty name, so testing the
                // name excludes perfectly real objects in a scene nobody has saved yet. A prefab
                // asset is what we actually mean to skip, and its scene is invalid.
                if (!go.scene.IsValid()) continue;
                return go;
            }
            return null;
        }
    }
}
