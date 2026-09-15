namespace Mosaic.Bridge.Tools.Models
{
    public sealed class ModelClipInfo
    {
        public string Name { get; set; }
        public string TakeName { get; set; }
        public float FirstFrame { get; set; }
        public float LastFrame { get; set; }
        public bool LoopTime { get; set; }
        public bool LoopPose { get; set; }
    }

    public sealed class ModelInfoResult
    {
        public string AssetPath { get; set; }
        public string AnimationType { get; set; }
        public string AvatarSetup { get; set; }
        public bool ImportAnimation { get; set; }
        public float GlobalScale { get; set; }
        public bool UseFileScale { get; set; }
        public string MaterialImportMode { get; set; }
        public bool IsReadable { get; set; }
        public string MeshCompression { get; set; }
        public bool AddCollider { get; set; }
        public bool GenerateSecondaryUV { get; set; }

        /// <summary>Take names Unity found in the source file before any splitting.</summary>
        public string[] DefaultTakeNames { get; set; }

        public ModelClipInfo[] Clips { get; set; }

        /// <summary>Human bone names mapped by the importer's HumanDescription — read-only report;
        /// remapping the rig itself is out of scope (do it in the DCC tool or the Avatar's own
        /// configuration UI, not via this tool).</summary>
        public string[] HumanBoneNames { get; set; }
    }
}
