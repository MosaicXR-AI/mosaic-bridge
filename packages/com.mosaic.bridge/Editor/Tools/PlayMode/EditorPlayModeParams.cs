using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.PlayMode
{
    public sealed class EditorPlayModeParams
    {
        /// <summary>
        /// The action to perform: "play", "pause", "stop", or "step".
        /// </summary>
        [Required] public string Action { get; set; }
    }
}
