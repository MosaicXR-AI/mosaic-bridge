namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsBodyPose
    {
        public string GameObjectName { get; set; }
        public float[] PositionBefore { get; set; }
        public float[] PositionAfter  { get; set; }
        public float[] RotationBefore { get; set; }
        public float[] RotationAfter  { get; set; }
    }

    public sealed class PhysicsSimulateResult
    {
        public int   StepsSimulated { get; set; }
        public float TotalTime      { get; set; }
        public PhysicsBodyPose[] Poses { get; set; }
        public string Message { get; set; }
    }
}
