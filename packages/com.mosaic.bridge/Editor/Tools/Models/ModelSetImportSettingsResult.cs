namespace Mosaic.Bridge.Tools.Models
{
    public sealed class ModelSetImportSettingsResult
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
        public int ClipCount { get; set; }
        public bool TexturesExtracted { get; set; }
        public bool MaterialsRemapped { get; set; }
    }
}
