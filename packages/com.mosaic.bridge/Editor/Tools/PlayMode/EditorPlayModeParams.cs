using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.PlayMode
{
    public sealed class EditorPlayModeParams
    {
        /// <summary>
        /// The action to perform: "play", "pause", "stop", "step", or "status".
        /// </summary>
        [Required]
        [AllowedValues("play", "pause", "stop", "step", "status")]
        public string Action { get; set; }

        /// <summary>
        /// Seconds of game time to drive after the action, for an Editor that is not the
        /// focused window. Optional; 0 means do not pump.
        /// </summary>
        public float PumpSeconds { get; set; }
    }
}
