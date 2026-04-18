namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UISetRectTransformParams
    {
        /// <summary>InstanceId of the target UI element.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of the target UI element (fallback if InstanceId not set).</summary>
        public string Name { get; set; }

        /// <summary>Anchor min [x, y]. Range 0-1.</summary>
        public float[] AnchorMin { get; set; }

        /// <summary>Anchor max [x, y]. Range 0-1.</summary>
        public float[] AnchorMax { get; set; }

        /// <summary>Pivot [x, y]. Range 0-1.</summary>
        public float[] Pivot { get; set; }

        /// <summary>Size delta [width, height].</summary>
        public float[] SizeDelta { get; set; }

        /// <summary>Anchored position [x, y].</summary>
        public float[] AnchoredPosition { get; set; }
    }
}
