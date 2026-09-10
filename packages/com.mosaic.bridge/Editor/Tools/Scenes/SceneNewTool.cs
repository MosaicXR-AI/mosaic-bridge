using UnityEditor;
using UnityEditor.SceneManagement;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public static class SceneNewTool
    {
        [MosaicTool("scene/new",
                    "Creates a new empty scene. Unsaved changes in the current scene are SAVED first " +
                    "by default; pass SaveMode 'discard' or 'prompt' to change that. A prompt blocks the " +
                    "Editor's main thread until a human clicks it.",
                    isReadOnly: false)]
        public static ToolResult<SceneNewResult> New(SceneNewParams p)
        {
            var previousSceneName = EditorSceneManager.GetActiveScene().name;

            if (p.SaveCurrent)
            {
                var mode = SceneSaveMode.Resolve(p.SaveMode, out var error);
                if (error != null)
                    return ToolResult<SceneNewResult>.Fail(error, Mosaic.Bridge.Contracts.Errors.ErrorCodes.INVALID_PARAM);
                SceneSaveMode.Apply(mode);
            }

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            return ToolResult<SceneNewResult>.Ok(new SceneNewResult
            {
                PreviousScene = previousSceneName,
                NewSceneName = newScene.name
            });
        }
    }
}
