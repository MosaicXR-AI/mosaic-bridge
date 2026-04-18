#if MOSAIC_HAS_TMP
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public sealed class TmpCreateParams
    {
        /// <summary>The text content to display.</summary>
        [Required] public string Text { get; set; }

        /// <summary>
        /// Context type: "ui" for Canvas-based TextMeshProUGUI, "world" for 3D TextMeshPro.
        /// Default is "ui".
        /// </summary>
        public string Context { get; set; } = "ui";

        /// <summary>Name for the created GameObject. Defaults to "TMP Text".</summary>
        public string Name { get; set; }

        /// <summary>Font size. Default 36.</summary>
        public float FontSize { get; set; } = 36f;

        /// <summary>RGBA color as float[4] (0-1 range). Null defaults to white.</summary>
        public float[] Color { get; set; }

        /// <summary>Optional path to a TMP_FontAsset in the project (loaded via AssetDatabase).</summary>
        public string FontAsset { get; set; }

        /// <summary>Optional parent GameObject name. For "ui" context, a Canvas is auto-created if no parent has one.</summary>
        public string Parent { get; set; }
    }
}
#endif
