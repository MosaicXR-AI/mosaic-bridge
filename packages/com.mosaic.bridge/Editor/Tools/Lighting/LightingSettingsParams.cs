using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingSettingsParams
    {
        /// <summary>Action: create, get, set.</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for a LightingSettings asset (e.g. "Assets/Scenes/Level1.lighting").
        /// get/set with no path reads/writes the active scene's currently assigned settings
        /// (Lightmapping.lightingSettings) instead of a specific asset.</summary>
        public string AssetPath { get; set; }

        /// <summary>create only: also assign the new/existing settings to the active scene via
        /// Lightmapping.SetLightingSettingsForScene.</summary>
        public bool AssignToActiveScene { get; set; }

        /// <summary>"ProgressiveCPU" or "ProgressiveGPU". Null to leave unchanged.</summary>
        public string Lightmapper { get; set; }
        public float? LightmapResolution { get; set; }
        public int? LightmapMaxSize { get; set; }
        public bool? Ao { get; set; }
        /// <summary>"IndirectOnly", "Shadowmask", or "Subtractive". Null to leave unchanged.</summary>
        public string MixedBakeMode { get; set; }
        public bool? BakedGI { get; set; }
        public bool? RealtimeGI { get; set; }
        public float? IndirectResolution { get; set; }
        public float? AoMaxDistance { get; set; }
        public int? DirectSampleCount { get; set; }
        public int? IndirectSampleCount { get; set; }
        public int? MinBounces { get; set; }
        public int? MaxBounces { get; set; }
        /// <summary>"None", "Auto", or "Advanced". Null to leave unchanged.</summary>
        public string FilteringMode { get; set; }
    }
}
