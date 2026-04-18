namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioInfoParams
    {
        /// <summary>InstanceId of a specific GameObject to query. If null, scans the entire scene.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of a specific GameObject to query. If null, scans the entire scene.</summary>
        public string Name { get; set; }
    }
}
