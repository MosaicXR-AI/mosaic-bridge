using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingReflectionProbeParams
    {
        /// <summary>Action: create, set, bake, bake-all.</summary>
        [Required] public string Action { get; set; }

        /// <summary>GameObject name of the probe. Required for set/bake; used as the new GameObject's
        /// name for create. Ignored for bake-all (bakes every probe in the scene).</summary>
        public string Name { get; set; }

        // -- create --
        public float[] Position { get; set; }

        // -- create / set --
        /// <summary>"Baked", "Realtime", or "Custom". Null to leave unchanged.</summary>
        public string Mode { get; set; }
        public int? Resolution { get; set; }
        public float[] Size { get; set; }
        public float[] Center { get; set; }
        public bool? BoxProjection { get; set; }
        public bool? Hdr { get; set; }
        public float? Intensity { get; set; }
        public int? Importance { get; set; }
        public float? NearClipPlane { get; set; }
        public float? FarClipPlane { get; set; }
        public float? ShadowDistance { get; set; }
        public float? BlendDistance { get; set; }

        // -- bake / bake-all --
        /// <summary>Asset path for the baked cubemap (bake only). Defaults to
        /// "Assets/{ProbeName}.exr" alongside the probe's own scene when omitted.</summary>
        public string BakePath { get; set; }
    }
}
