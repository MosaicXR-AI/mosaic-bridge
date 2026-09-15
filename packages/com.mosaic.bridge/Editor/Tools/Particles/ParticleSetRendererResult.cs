namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetRendererResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string RenderMode { get; set; }
        public float VelocityScale { get; set; }
        public float LengthScale { get; set; }
        public float MaxParticleSize { get; set; }
        public float MinParticleSize { get; set; }
        public string MaterialPath { get; set; }
        public string SortMode { get; set; }
        public string MeshPath { get; set; }
        public string TrailMaterialPath { get; set; }
        public string SortingLayer { get; set; }
        public int SortingOrder { get; set; }
    }
}
