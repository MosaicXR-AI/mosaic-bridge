namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationIkSetupResult
    {
        public string ScriptPath { get; set; }
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public string Solver { get; set; }
        public int ChainLength { get; set; }
    }
}
