namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleCreateParams
    {
        public string Name { get; set; }          // defaults to "Particle System"
        public float[] Position { get; set; }     // [x,y,z] — null defaults to [0,0,0]
        public string Preset { get; set; }        // fire, smoke, sparks, rain, snow, explosion — null = default
    }
}
