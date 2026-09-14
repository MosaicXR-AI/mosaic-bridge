using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIRemoveListenerParams
    {
        public int? InstanceId { get; set; }
        public string GameObjectName { get; set; }

        [Required] public string ComponentType { get; set; }
        [Required] public string EventName { get; set; }

        /// <summary>Index into the event's persistent listener list, as reported by
        /// ui/add_listener's own ListenerIndex. Required — index 0 is a valid value, which is why
        /// this is nullable rather than a plain int (0 must not read as "missing").</summary>
        public int? Index { get; set; }
    }
}
