using UnityEditor.SceneManagement;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public static class SceneGetInfoTool
    {
        [MosaicTool("scene/get_info",
                    "Returns metadata about the currently active scene",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<SceneGetInfoResult> GetInfo(object p)
        {
            var scene = EditorSceneManager.GetActiveScene();

            return ToolResult<SceneGetInfoResult>.Ok(new SceneGetInfoResult
            {
                Name = scene.name,
                Path = scene.path,
                IsDirty = scene.isDirty,
                RootObjectCount = scene.rootCount,
                IsLoaded = scene.isLoaded
            });
        }
    }
}
