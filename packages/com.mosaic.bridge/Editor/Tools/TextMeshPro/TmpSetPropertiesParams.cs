#if MOSAIC_HAS_TMP
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public sealed class TmpSetPropertiesParams
    {
        /// <summary>Name of the GameObject with a TMP component.</summary>
        [Required] public string GameObjectName { get; set; }

        /// <summary>New text content. Null leaves unchanged.</summary>
        public string Text { get; set; }

        /// <summary>Font size. Null leaves unchanged.</summary>
        public float? FontSize { get; set; }

        /// <summary>RGBA color as float[4]. Null leaves unchanged.</summary>
        public float[] Color { get; set; }

        /// <summary>Text alignment: left, center, right, justified. Null leaves unchanged.</summary>
        public string Alignment { get; set; }

        /// <summary>Font style: normal, bold, italic. Null leaves unchanged.</summary>
        public string FontStyle { get; set; }

        /// <summary>Overflow mode: overflow, ellipsis, truncate, page. Null leaves unchanged.</summary>
        public string OverflowMode { get; set; }

        /// <summary>Margins as float[4] LTRB. Null leaves unchanged.</summary>
        public float[] Margins { get; set; }

        /// <summary>Line spacing. Null leaves unchanged.</summary>
        public float? LineSpacing { get; set; }

        /// <summary>Character spacing. Null leaves unchanged.</summary>
        public float? CharacterSpacing { get; set; }

        /// <summary>Word spacing. Null leaves unchanged.</summary>
        public float? WordSpacing { get; set; }

        /// <summary>Enable or disable rich text parsing. Null leaves unchanged.</summary>
        public bool? EnableRichText { get; set; }
    }
}
#endif
