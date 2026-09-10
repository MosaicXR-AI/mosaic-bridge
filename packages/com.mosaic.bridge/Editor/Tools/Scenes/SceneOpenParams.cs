using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneOpenParams
    {
        [Required] public string Path { get; set; }

        /// <summary>
        /// What to do with unsaved changes in the current scene: "save" (default),
        /// "discard", or "prompt" for Unity's dialog.
        /// </summary>
        [AllowedValues("save", "discard", "prompt")]
        public string SaveMode { get; set; }
    }
}
