namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class RagdollBoneInfo
    {
        public string BoneName { get; set; }
        public float  Mass     { get; set; }
        public string ColliderType { get; set; }
        public bool   HasJoint { get; set; }
    }

    public sealed class PhysicsCreateRagdollResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public RagdollBoneInfo[] Bones { get; set; }
        public string Message { get; set; }
    }
}
