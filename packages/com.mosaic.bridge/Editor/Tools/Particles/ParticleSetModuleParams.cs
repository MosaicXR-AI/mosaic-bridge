using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetModuleParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }

        /// <summary>"colorOverLifetime", "sizeOverLifetime", "velocityOverLifetime", "limitVelocity",
        /// "rotationOverLifetime", "noise", or "forceOverLifetime".</summary>
        [Required] public string Module { get; set; }

        /// <summary>Enables/disables the module. Defaults to true when any other field on this
        /// module is set and Enabled is not explicitly provided.</summary>
        public bool? Enabled { get; set; }

        // -- colorOverLifetime: MinMaxGradient(Gradient) --
        public float[] ColorKeyTimes { get; set; }
        /// <summary>Flattened [r,g,b, r,g,b, ...] triples, parallel to ColorKeyTimes.</summary>
        public float[] ColorKeyColors { get; set; }
        public float[] AlphaKeyTimes { get; set; }
        public float[] AlphaKeyValues { get; set; }

        // -- sizeOverLifetime / rotationOverLifetime (single-axis): MinMaxCurve(scalar, curve).
        // rotationOverLifetime.z is in RADIANS/sec even though Unity's own Inspector shows degrees.
        public float? CurveScalar { get; set; }
        public float[] CurveTimes { get; set; }
        public float[] CurveValues { get; set; }

        // -- velocityOverLifetime / forceOverLifetime (3-axis) --
        public float? XConstant { get; set; }
        public float? YConstant { get; set; }
        public float? ZConstant { get; set; }
        public float[] XCurveTimes { get; set; }
        public float[] XCurveValues { get; set; }
        public float[] YCurveTimes { get; set; }
        public float[] YCurveValues { get; set; }
        public float[] ZCurveTimes { get; set; }
        public float[] ZCurveValues { get; set; }
        /// <summary>"Local" or "World". velocityOverLifetime/forceOverLifetime only.</summary>
        public string Space { get; set; }
        /// <summary>velocityOverLifetime only — orbital/radial speed modifier curve.</summary>
        public float[] SpeedModifierCurveTimes { get; set; }
        public float[] SpeedModifierCurveValues { get; set; }
        /// <summary>forceOverLifetime only.</summary>
        public bool? Randomized { get; set; }

        // -- limitVelocity --
        public float? LimitConstant { get; set; }
        public float[] LimitCurveTimes { get; set; }
        public float[] LimitCurveValues { get; set; }
        public float? Dampen { get; set; }

        // -- noise --
        public float? NoiseStrength { get; set; }
        public float? NoiseFrequency { get; set; }
        public float? NoiseScrollSpeed { get; set; }
        public bool? NoiseDamping { get; set; }
        public int? NoiseOctaveCount { get; set; }
        public float? NoiseOctaveMultiplier { get; set; }
        public float? NoiseOctaveScale { get; set; }
    }
}
