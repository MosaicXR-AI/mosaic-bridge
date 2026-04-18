using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Scenes
{
    public static class SceneOpenTool
    {
        [MosaicTool("scene/open",
                    "Opens a scene by asset path (e.g. Assets/Scenes/Main.unity), saving the current scene first if modified",
                    isReadOnly: false)]
        public static ToolResult<SceneOpenResult> Open(SceneOpenParams p)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p.Path) == null)
                return ToolResult<SceneOpenResult>.Fail(
                    $"Scene not found at path: '{p.Path}'", ErrorCodes.NOT_FOUND);

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var scene = EditorSceneManager.OpenScene(p.Path, OpenSceneMode.Single);

            return ToolResult<SceneOpenResult>.Ok(new SceneOpenResult
            {
                SceneName = scene.name,
                ScenePath = scene.path
            });
        }
    }
}
