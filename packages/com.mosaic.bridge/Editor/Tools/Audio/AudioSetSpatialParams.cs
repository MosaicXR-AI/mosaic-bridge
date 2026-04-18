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
    }
}
