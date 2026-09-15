namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSetSpatialParams
    {
        /// <summary>InstanceId of the target GameObject.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of the target GameObject.</summary>
        public string Name { get; set; }

        /// <summary>Minimum distance for 3D sound attenuation.</summary>
        public float? MinDistance { get; set; }

        /// <summary>Maximum distance for 3D sound attenuation.</summary>
        public float? MaxDistance { get; set; }

        /// <summary>Rolloff mode: "Logarithmic", "Linear", or "Custom".</summary>
        public string RolloffMode { get; set; }

        /// <summary>Doppler effect level (0-5). Defaults to 1.</summary>
        public float? DopplerLevel { get; set; }

        /// <summary>Spread angle of 3D sound in degrees (0-360).</summary>
        public float? Spread { get; set; }

        /// <summary>Keyframe times (parallel to CustomRolloffValues) for the custom volume-rolloff curve.
        /// Requires RolloffMode="Custom". Sets AudioSource.SetCustomCurve(CustomRolloff, ...).</summary>
        public float[] CustomRolloffTimes { get; set; }
        public float[] CustomRolloffValues { get; set; }

        /// <summary>Keyframe times/values for the custom spatial-blend curve.</summary>
        public float[] SpatialBlendCurveTimes { get; set; }
        public float[] SpatialBlendCurveValues { get; set; }

        /// <summary>Keyframe times/values for the custom reverb-zone-mix curve.</summary>
        public float[] ReverbZoneMixCurveTimes { get; set; }
        public float[] ReverbZoneMixCurveValues { get; set; }

        /// <summary>Keyframe times/values for the custom spread curve.</summary>
        public float[] SpreadCurveTimes { get; set; }
        public float[] SpreadCurveValues { get; set; }
    }
}
