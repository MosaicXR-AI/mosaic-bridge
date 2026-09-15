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

        /// <summary>Asset path to a mesh, for RenderMode=Mesh.</summary>
        public string MeshPath { get; set; }
        /// <summary>Asset path to a material applied to Trails-module trails.</summary>
        public string TrailMaterialPath { get; set; }
        /// <summary>Sorting layer name (2D courses).</summary>
        public string SortingLayer { get; set; }
        public int? SortingOrder { get; set; }
    }
}
