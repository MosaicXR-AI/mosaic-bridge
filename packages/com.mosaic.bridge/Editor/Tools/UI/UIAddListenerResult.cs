namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIAddListenerResult
    {
        public string GameObjectName { get; set; }
        public string EventName { get; set; }
        public string TargetGameObjectName { get; set; }
        public string MethodName { get; set; }

        /// <summary>Index of the new listener in the event's persistent listener list — pass this
        /// to ui/remove_listener.</summary>
        public int ListenerIndex { get; set; }

        public string CallState { get; set; }
    }
}
