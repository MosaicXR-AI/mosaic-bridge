namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationAssignResult
    {
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public bool Legacy { get; set; }

        // -- Animator path --
        public string ControllerPath { get; set; }
        public string AvatarPath { get; set; }
        public bool ApplyRootMotion { get; set; }
        public string UpdateMode { get; set; }
        public string CullingMode { get; set; }

        // -- Legacy path --
        public string ClipPath { get; set; }
        public string ClipName { get; set; }
    }
}
