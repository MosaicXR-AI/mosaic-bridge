namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddCharacterControllerResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public float[] Center        { get; set; }
        public float  Radius         { get; set; }
        public float  Height         { get; set; }
        public float  SlopeLimit     { get; set; }
        public float  StepOffset     { get; set; }
        public float  SkinWidth      { get; set; }
        public float  MinMoveDistance { get; set; }
        public bool   RigidbodyRemoved { get; set; }
    }
}
