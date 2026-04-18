using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.LOD
{
    public sealed class LodCreateParams
    {
        /// <summary>Name of the target GameObject. Required unless InstanceId is provided.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over Name if both are set.</summary>
        public int? InstanceId { get; set; }
        /// <summary>LOD levels to configure. Each entry has a ScreenHeight and optional RendererPaths.</summary>
        [Required] public LodLevelInput[] Levels { get; set; }
    }

    public sealed class LodLevelInput
    {
        /// <summary>Screen relative height threshold (0.0 to 1.0) at which this LOD kicks in.</summary>
        public float ScreenHeight { get; set; }
        /// <summary>Relative paths to child GameObjects whose Renderers should be assigned to this LOD level.</summary>
        public string[] RendererPaths { get; set; }
    }
}
