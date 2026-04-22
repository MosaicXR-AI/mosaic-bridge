namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleCreateParams
    {
        public string Name { get; set; }          // defaults to "Particle System"
        public float[] Position { get; set; }     // [x,y,z] — null defaults to [0,0,0]
        public string Preset { get; set; }        // fire, smoke, sparks, rain, snow, explosion — null = default

        // If provided, instantiate this prefab path instead of creating from scratch.
        // Use asset/list first to find particle prefabs from installed packs.
        public string PrefabPath { get; set; }

        // When true (default), auto-search the project for a matching particle prefab
        // before falling back to the built-in preset. Set false to always use presets.
        public bool UseExistingPrefab { get; set; } = true;
    }
}
