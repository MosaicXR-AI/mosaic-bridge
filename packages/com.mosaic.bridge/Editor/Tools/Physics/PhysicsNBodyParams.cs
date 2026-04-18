using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsNBodyParams
    {
        public sealed class Body
        {
            [Required] public float[] Position { get; set; }
            [Required] public float   Mass     { get; set; }
            public float[]            Velocity { get; set; }
        }

        [Required] public List<Body> Bodies               { get; set; }
        public float?                GravitationalConstant { get; set; }
        public float?                Theta                 { get; set; }
        public float?                Softening             { get; set; }
        public string                Integrator            { get; set; }
        public float?                TimeStep              { get; set; }
        public string                BodyPrefabPath        { get; set; }
        public string                Name                  { get; set; }
        public float[]               Position              { get; set; }
        public string                SavePath              { get; set; }
    }
}
