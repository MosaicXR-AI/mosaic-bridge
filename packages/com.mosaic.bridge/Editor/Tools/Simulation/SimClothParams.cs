namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimClothParams
    {
        public int?    Width                { get; set; }
        public int?    Height               { get; set; }
        public float?  Spacing              { get; set; }
        public float?  Gravity              { get; set; }
        public float?  Stiffness            { get; set; }
        public float?  Damping              { get; set; }
        public int?    ConstraintIterations { get; set; }
        public string  OutputDirectory      { get; set; }
    }
}
