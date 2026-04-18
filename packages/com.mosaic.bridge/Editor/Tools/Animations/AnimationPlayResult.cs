namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationPlayResult
    {
        public string Action { get; set; }
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public string StateName { get; set; }
        public int LayerIndex { get; set; }
        public float NormalizedTime { get; set; }
        public string Message { get; set; }
    }
}
