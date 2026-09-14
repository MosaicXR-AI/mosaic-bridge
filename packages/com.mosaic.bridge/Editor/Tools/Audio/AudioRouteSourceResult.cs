namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioRouteSourceResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string MixerName { get; set; }

        /// <summary>The matched group's own name. AudioMixerGroup exposes no public full-path
        /// property — audio/mixer-info (internal-API reflection) is where a hierarchy path comes from.</summary>
        public string GroupName { get; set; }
    }
}
