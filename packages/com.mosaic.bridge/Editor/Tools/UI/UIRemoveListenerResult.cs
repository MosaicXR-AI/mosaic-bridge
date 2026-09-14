namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIRemoveListenerResult
    {
        public string GameObjectName { get; set; }
        public string EventName { get; set; }
        public int RemovedIndex { get; set; }
        public int RemainingListenerCount { get; set; }
    }
}
