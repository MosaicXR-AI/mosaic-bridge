namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneOpenResult
    {
        public string SceneName { get; set; }
        public string ScenePath { get; set; }

        /// <summary>Whether unsaved changes in the previous scene were written to disk.</summary>
        public bool PreviousSceneSaved { get; set; }
    }
}
