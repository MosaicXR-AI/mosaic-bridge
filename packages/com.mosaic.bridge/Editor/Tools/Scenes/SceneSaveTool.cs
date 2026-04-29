using UnityEditor.SceneManagement;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public static class SceneSaveTool
    {
        [MosaicTool("scene/save",
                    "Saves the currently active scene to disk. " +
                    "May timeout in large scenes with many assets. " +
                    "If this tool hangs, use editor/run-menu-item with MenuPath='File/Save' as a fallback.",
                    isReadOnly: false)]
        public static ToolResult<SceneSaveResult> Save(object p)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);

            return ToolResult<SceneSaveResult>.Ok(new SceneSaveResult
            {
                ScenePath = scene.path,
                SceneName = scene.name
            });
        }
    }
}
