namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleCreateResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string Preset { get; set; }
        public string SourcePrefabPath { get; set; }  // non-null when instantiated from existing prefab
        public bool FromPrefab { get; set; }
    }
}
