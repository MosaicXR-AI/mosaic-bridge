namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsSettingsResult
    {
        public float[] Gravity                 { get; set; }
        public float  BounceThreshold           { get; set; }
        public float  DefaultContactOffset      { get; set; }
        public int    DefaultSolverIterations   { get; set; }
        public int    DefaultSolverVelocityIterations { get; set; }
        public float  SleepThreshold            { get; set; }
        public float  DefaultMaxDepenetrationVelocity { get; set; }
        public float  DefaultMaxAngularSpeed    { get; set; }
        public string SimulationMode            { get; set; }
        public bool   QueriesHitTriggers        { get; set; }
        public bool   QueriesHitBackfaces       { get; set; }
        public bool   AutoSyncTransforms        { get; set; }
        public string Message                   { get; set; }
    }
}
