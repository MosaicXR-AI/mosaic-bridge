using System.Linq;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public static class SceneGetStatsTool
    {
        [MosaicTool("scene/get_stats",
                    "Returns aggregate statistics (object counts, cameras) for the currently active scene",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<SceneGetStatsResult> GetStats(object p)
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene.IsValid())
                .ToArray();

            var total = allObjects.Length;
            var active = allObjects.Count(go => go.activeInHierarchy);
            var componentCount = allObjects.Sum(go => go.GetComponents<Component>().Length);
            var cameras = UnityEngine.Camera.allCameras.Select(c => c.gameObject.name).ToArray();

            return ToolResult<SceneGetStatsResult>.Ok(new SceneGetStatsResult
            {
                TotalGameObjects = total,
                ActiveGameObjects = active,
                TotalComponents = componentCount,
                ActiveCameras = cameras
            });
        }
    }
}
