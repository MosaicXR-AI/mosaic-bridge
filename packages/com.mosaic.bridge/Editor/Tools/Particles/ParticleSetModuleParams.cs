using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetModuleParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }

        /// <summary>"colorOverLifetime", "sizeOverLifetime", "velocityOverLifetime", "limitVelocity",
        /// "rotationOverLifetime", "noise", "forceOverLifetime", "collision", "subEmitters",
        /// "trails", "lights", or "textureSheetAnimation".</summary>
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

        // -- collision (Dampen is shared with limitVelocity above) --
        /// <summary>"Planes" or "World".</summary>
        public string CollisionType { get; set; }
        /// <summary>"Collision3D" or "Collision2D".</summary>
        public string CollisionMode { get; set; }
        public float? Bounce { get; set; }
        public float? LifetimeLoss { get; set; }
        public float? MinKillSpeed { get; set; }
        public float? MaxKillSpeed { get; set; }
        /// <summary>Comma-separated layer names this system's particles collide with (World mode).</summary>
        public string CollidesWithLayers { get; set; }
        public bool? SendCollisionMessages { get; set; }
        public float? RadiusScale { get; set; }

        // -- subEmitters --
        /// <summary>Name of a GameObject carrying the child ParticleSystem to add as a sub-emitter.</summary>
        public string SubEmitterName { get; set; }
        /// <summary>"Birth", "Collision", "Death", "Trigger", or "Manual".</summary>
        public string SubEmitterType { get; set; }
        /// <summary>Comma-separated: InheritNothing, InheritEverything, InheritColor, InheritSize,
        /// InheritRotation, InheritLifetime, InheritDuration. Defaults to InheritNothing.</summary>
        public string SubEmitterProperties { get; set; }
        public float? SubEmitterEmitProbability { get; set; }

        // -- trails --
        public float? TrailRatio { get; set; }
        public float? TrailMinVertexDistance { get; set; }
        public bool? TrailWorldSpace { get; set; }
        public bool? TrailDieWithParticles { get; set; }
        public bool? TrailSizeAffectsWidth { get; set; }
        public float? TrailLifetimeConstant { get; set; }
        public float[] TrailLifetimeCurveTimes { get; set; }
        public float[] TrailLifetimeCurveValues { get; set; }

        // -- lights --
        /// <summary>Asset path to a prefab carrying the Light component the module instantiates per particle.</summary>
        public string LightPrefabPath { get; set; }
        public float? LightRatio { get; set; }
        public bool? LightUseRandomDistribution { get; set; }
        public bool? LightUseParticleColor { get; set; }
        public bool? LightSizeAffectsRange { get; set; }
        public bool? LightAlphaAffectsIntensity { get; set; }
        public int? LightMaxLights { get; set; }

        // -- textureSheetAnimation --
        public int? TilesX { get; set; }
        public int? TilesY { get; set; }
        /// <summary>"WholeSheet" or "SingleRow".</summary>
        public string TsaAnimation { get; set; }
        public float? Fps { get; set; }
        public int? CycleCount { get; set; }
        public float? TsaStartFrameConstant { get; set; }
    }
}
