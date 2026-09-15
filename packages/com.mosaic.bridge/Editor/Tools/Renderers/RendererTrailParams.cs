namespace Mosaic.Bridge.Tools.Renderers
{
    public sealed class RendererTrailParams
    {
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>How long the trail takes to fade out, in seconds.</summary>
        public float? Time { get; set; }
        /// <summary>Minimum distance the object must travel before a new trail vertex is added.</summary>
        public float? MinVertexDistance { get; set; }
        /// <summary>Whether the trail is currently being emitted.</summary>
        public bool? Emitting { get; set; }
        /// <summary>Destroys the GameObject once the trail has fully faded after Emitting is set false.</summary>
        public bool? Autodestruct { get; set; }

        public float? StartWidth { get; set; }
        public float? EndWidth { get; set; }
        public float[] StartColor { get; set; }
        public float[] EndColor { get; set; }
        public string MaterialPath { get; set; }
    }
}
