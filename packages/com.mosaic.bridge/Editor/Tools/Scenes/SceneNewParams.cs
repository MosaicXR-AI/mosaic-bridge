using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneNewParams
    {
        public bool SaveCurrent { get; set; } = true;

        /// <summary>What to do with unsaved changes: "save" (default), "discard", or "prompt".</summary>
        [AllowedValues("save", "discard", "prompt")]
        public string SaveMode { get; set; }
    }
}
