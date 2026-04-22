namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetRendererParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }

        // "Billboard" | "Stretch" | "HorizontalBillboard" | "VerticalBillboard" | "Mesh" | "None"
        public string RenderMode { get; set; }

        public float? VelocityScale { get; set; }
        public float? LengthScale { get; set; }
        public float? MaxParticleSize { get; set; }
        public float? MinParticleSize { get; set; }
        public string MaterialPath { get; set; }

        // "None" | "Distance" | "OldestInFront" | "YoungestInFront"
        public string SortMode { get; set; }

        public bool? UseUrpParticlesMaterial { get; set; }
    }
}
