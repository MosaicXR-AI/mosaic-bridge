using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.VFX
{
    public sealed class VfxPlaybackParams
    {
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>"play", "stop", "reinit", or "event" (send a named event via SendEvent).</summary>
        [Required] public string Action { get; set; }

        /// <summary>Required for "event" — the event name to send.</summary>
        public string EventName { get; set; }
    }
}
