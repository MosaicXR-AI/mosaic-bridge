using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGenerateAssetParams
    {
        /// <summary>What to generate, in plain language. e.g. "seamless dark oak floor planks".</summary>
        [Required]
        public string Prompt { get; set; }

        /// <summary>Texture, Material, Sprite, Image, Sound, Animation, or Model3D.</summary>
        [Required]
        public string AssetType { get; set; }

        /// <summary>Short asset name used for the save path. e.g. "floor-oak".</summary>
        [Required]
        public string Name { get; set; }

        /// <summary>Project-relative destination. Defaults to "Assets/Generated".</summary>
        public string DestinationFolder { get; set; }

        /// <summary>
        /// Enrich the prompt with measured values from the bundled PBR knowledge base when
        /// the description names a known material. Default true.
        /// </summary>
        public bool? GroundInKnowledgeBase { get; set; }

        /// <summary>Pixel width for image-like kinds. Optional.</summary>
        public int? Width { get; set; }

        /// <summary>Pixel height for image-like kinds. Optional.</summary>
        public int? Height { get; set; }

        /// <summary>Length in seconds for Sound. Keep short (≤2s) for SFX. Optional.</summary>
        public float? DurationSeconds { get; set; }
    }
}
