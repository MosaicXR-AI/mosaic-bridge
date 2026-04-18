namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationClipResult
    {
        public string Action { get; set; }
        public string Path { get; set; }
        public string Guid { get; set; }
        public string ClipName { get; set; }

        // -- info --
        public float Length { get; set; }
        public float FrameRate { get; set; }
        public bool IsLooping { get; set; }
        public int CurveCount { get; set; }
        public int EventCount { get; set; }
        public AnimationCurveInfo[] Curves { get; set; }
        public AnimationEventInfo[] Events { get; set; }
    }

    public sealed class AnimationCurveInfo
    {
        public string Path { get; set; }
        public string PropertyName { get; set; }
        public string Type { get; set; }
        public int KeyframeCount { get; set; }
    }

    public sealed class AnimationEventInfo
    {
        public float Time { get; set; }
        public string FunctionName { get; set; }
        public string StringParameter { get; set; }
        public float FloatParameter { get; set; }
        public int IntParameter { get; set; }
    }
}
