namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationControllerResult
    {
        public string Action { get; set; }
        public string Path { get; set; }
        public string Guid { get; set; }

        // -- info results --
        public string[] Layers { get; set; }
        public AnimationParameterInfo[] Parameters { get; set; }
        public AnimationStateInfo[] States { get; set; }

        // -- add-parameter result --
        public string AddedParameterName { get; set; }
        public string AddedParameterType { get; set; }

        // -- remove-parameter result --
        public int? RemovedParameterIndex { get; set; }

        // -- add-layer result --
        public string AddedLayerName { get; set; }
    }

    public sealed class AnimationParameterInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public float DefaultFloat { get; set; }
        public int DefaultInt { get; set; }
        public bool DefaultBool { get; set; }
    }

    public sealed class AnimationStateInfo
    {
        public string Name { get; set; }
        public string MotionName { get; set; }
        public string LayerName { get; set; }
        public bool IsDefault { get; set; }
    }
}
