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
                    "Opens a scene by asset path (e.g. Assets/Scenes/Main.unity). Unsaved changes in the " +
                    "current scene are SAVED first by default. Pass SaveMode 'discard' to throw them away, " +
                    "or 'prompt' to ask — but note that a prompt blocks the Editor's main thread, so every " +
                    "request queued behind it times out until a human clicks the dialog.",
                    isReadOnly: false)]
        public static ToolResult<SceneOpenResult> Open(SceneOpenParams p)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p.Path) == null)
                return ToolResult<SceneOpenResult>.Fail(
                    $"Scene not found at path: '{p.Path}'", ErrorCodes.NOT_FOUND);

            var mode = SceneSaveMode.Resolve(p.SaveMode, out var error);
            if (error != null)
                return ToolResult<SceneOpenResult>.Fail(error, ErrorCodes.INVALID_PARAM);

            var saved = SceneSaveMode.Apply(mode);
            var scene = EditorSceneManager.OpenScene(p.Path, OpenSceneMode.Single);

            return ToolResult<SceneOpenResult>.Ok(new SceneOpenResult
            {
                SceneName = scene.name,
                ScenePath = scene.path,
                PreviousSceneSaved = saved
            });
        }
    }
}
