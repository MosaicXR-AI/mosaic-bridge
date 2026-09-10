using UnityEditor;
using UnityEditor.SceneManagement;

namespace Mosaic.Bridge.Tools.Scenes
{
    /// <summary>
    /// What to do with an unsaved scene before replacing it.
    /// </summary>
    /// <remarks>
    /// Every scene switch used to call SaveCurrentModifiedScenesIfUserWantsTo, which raises
    /// Unity's "Save changes to scene?" dialog whenever the open scene is dirty. A modal
    /// pumps its own event loop on the main thread, so every request queued behind it times
    /// out until a human clicks the button. For an Editor being driven remotely that is fatal
    /// and unrecoverable: one reviewer's scene/open returned "the Editor did not answer within
    /// 120s" and nothing could reach the Editor again until someone walked to the machine.
    ///
    /// So the default is now to save, without asking. A caller that wants the old behaviour
    /// can ask for it, and is told what it costs.
    /// </remarks>
    internal static class SceneSaveMode
    {
        internal const string Save = "save";
        internal const string Discard = "discard";
        internal const string Prompt = "prompt";

        internal static string Resolve(string requested, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(requested)) return Save;
            var mode = requested.Trim().ToLowerInvariant();
            if (mode == Save || mode == Discard || mode == Prompt) return mode;
            error = $"Invalid SaveMode '{requested}'. Valid values: save, discard, prompt.";
            return null;
        }

        /// <summary>Returns true when unsaved work was written to disk.</summary>
        internal static bool Apply(string mode)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isDirty) return false;

            switch (mode)
            {
                case Discard:
                    return false;
                case Prompt:
                    return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                default:
                    // An unsaved scene that has never been saved has no path to save to.
                    // Saving it would itself raise a file dialog, which is the thing this
                    // exists to avoid, so it is left alone and the caller is not blocked.
                    if (string.IsNullOrEmpty(scene.path)) return false;
                    return EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
