namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIAddElementParams
    {
        /// <summary>InstanceId of the parent Canvas or UI element.</summary>
        public int? ParentInstanceId { get; set; }

        /// <summary>Name of the parent Canvas or UI element (fallback if ParentInstanceId not set).</summary>
        public string ParentName { get; set; }

        /// <summary>
        /// Type of UI element to create: button, text, image, slider, toggle, dropdown, input-field.
        /// </summary>
        public string ElementType { get; set; }

        /// <summary>Optional name for the created element.</summary>
        public string Name { get; set; }

        /// <summary>Anchored position [x, y]. Defaults to [0, 0].</summary>
        public float[] AnchoredPosition { get; set; }

        /// <summary>Size delta [width, height]. Defaults vary by element type.</summary>
        public float[] SizeDelta { get; set; }
    }
}
