namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimBoidsParams
    {
        public int?    BoidCount        { get; set; }
        public float?  SeparationWeight { get; set; }
        public float?  AlignmentWeight  { get; set; }
        public float?  CohesionWeight   { get; set; }
        public float?  MaxSpeed         { get; set; }
        public float?  ViewRadius       { get; set; }
        public float?  AvoidRadius      { get; set; }
        public float?  BoundarySize     { get; set; }
        public string  OutputDirectory  { get; set; }
    }
}
