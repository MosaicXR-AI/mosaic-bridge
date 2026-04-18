namespace Mosaic.Bridge.Tools.Measure
{
    public sealed class AnnotationCreateParams
    {
        /// <summary>Annotation type: "text" (default), "leader_line", "arrow", "dimension", "callout", "pin".</summary>
        public string Type { get; set; } = "text";

        /// <summary>Text content to display.</summary>
        public string Text { get; set; }

        /// <summary>World position [x,y,z] (required).</summary>
        public float[] Position { get; set; }

        /// <summary>Target endpoint [x,y,z] for leader/arrow/dimension types.</summary>
        public float[] TargetPoint { get; set; }

        /// <summary>Optional target GameObject (annotation is parented to it, moves with it).</summary>
        public string TargetGameObject { get; set; }

        /// <summary>Font size for TextMesh. Default 14.</summary>
        public int FontSize { get; set; } = 14;

        /// <summary>Text color [r,g,b,a]. Default yellow [1,1,0,1].</summary>
        public float[] TextColor { get; set; }

        /// <summary>Line color [r,g,b,a]. Default white [1,1,1,1].</summary>
        public float[] LineColor { get; set; }

        /// <summary>Background color [r,g,b,a] for callout background.</summary>
        public float[] BackgroundColor { get; set; }

        /// <summary>Optional annotation GameObject name.</summary>
        public string Name { get; set; }

        /// <summary>Optional parent GameObject name to attach annotation under.</summary>
        public string Parent { get; set; }
    }
}
