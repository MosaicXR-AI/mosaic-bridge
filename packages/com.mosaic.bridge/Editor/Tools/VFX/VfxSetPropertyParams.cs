using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.VFX
{
    public sealed class VfxSetPropertyParams
    {
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        [Required] public string PropertyName { get; set; }

        /// <summary>"float", "int", "bool", "vector2", "vector3", "vector4", "texture", "gradient",
        /// "curve", or "mesh".</summary>
        [Required] public string ValueType { get; set; }

        public float? FloatValue { get; set; }
        public int? IntValue { get; set; }
        public bool? BoolValue { get; set; }
        /// <summary>[x,y] / [x,y,z] / [x,y,z,w] depending on ValueType.</summary>
        public float[] VectorValue { get; set; }
        public string TexturePath { get; set; }
        public string MeshPath { get; set; }

        /// <summary>Gradient: flattened [r,g,b, ...] triples, parallel to GradientColorKeyTimes.</summary>
        public float[] GradientColorKeyTimes { get; set; }
        public float[] GradientColorKeyColors { get; set; }
        public float[] GradientAlphaKeyTimes { get; set; }
        public float[] GradientAlphaKeyValues { get; set; }

        public float[] CurveKeyTimes { get; set; }
        public float[] CurveKeyValues { get; set; }
    }
}
