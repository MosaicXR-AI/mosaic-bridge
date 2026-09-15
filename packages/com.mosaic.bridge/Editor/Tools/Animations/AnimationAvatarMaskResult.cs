namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationAvatarMaskResult
    {
        public string Action { get; set; }
        public string MaskPath { get; set; }

        // -- set-body-part --
        public string BodyPart { get; set; }
        public bool? Active { get; set; }

        // -- add-transform-path / remove-transform-path --
        public string TransformPath { get; set; }
        public int TransformCount { get; set; }

        // -- info --
        public AvatarMaskBodyPartInfo[] BodyParts { get; set; }
        public AvatarMaskTransformInfo[] TransformPaths { get; set; }
    }

    public sealed class AvatarMaskBodyPartInfo
    {
        public string Part { get; set; }
        public bool Active { get; set; }
    }

    public sealed class AvatarMaskTransformInfo
    {
        public string Path { get; set; }
        public bool Active { get; set; }
    }
}
