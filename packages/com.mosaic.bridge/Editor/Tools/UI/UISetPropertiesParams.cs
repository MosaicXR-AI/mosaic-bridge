namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UISetPropertiesParams
    {
        /// <summary>InstanceId of the target UI element.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of the target UI element (fallback if InstanceId not set).</summary>
        public string Name { get; set; }

        /// <summary>Text content for Text/TMP_Text/InputField/Dropdown label.</summary>
        public string Text { get; set; }

        /// <summary>Font size for Text/TMP_Text components.</summary>
        public int? FontSize { get; set; }

        /// <summary>Color [r, g, b, a] in 0-1 range. Applied to Text, Image, or Button targetGraphic.</summary>
        public float[] Color { get; set; }

        /// <summary>Asset path to a Sprite (for Image component).</summary>
        public string Sprite { get; set; }

        /// <summary>Interactable flag for Button/Toggle/Slider/Dropdown/InputField.</summary>
        public bool? Interactable { get; set; }
    }
}
