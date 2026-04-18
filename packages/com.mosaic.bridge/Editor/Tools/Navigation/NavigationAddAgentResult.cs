namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationAddAgentResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public float Speed { get; set; }
        public float AngularSpeed { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }
        public float StoppingDistance { get; set; }
    }
}
