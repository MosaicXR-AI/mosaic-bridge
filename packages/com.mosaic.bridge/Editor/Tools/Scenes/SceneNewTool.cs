using UnityEditor;
using UnityEditor.SceneManagement;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public static class SceneNewTool
    {
        [MosaicTool("scene/new",
                    "Creates a new empty scene, optionally saving the current scene first",
                    isReadOnly: false)]
        public static ToolResult<SceneNewResult> New(SceneNewParams p)
        {
            var previousSceneName = EditorSceneManager.GetActiveScene().name;

            if (p.SaveCurrent)
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            return ToolResult<SceneNewResult>.Ok(new SceneNewResult
            {
                PreviousScene = previousSceneName,
                NewSceneName = newScene.name
            });
        }
    }
}
