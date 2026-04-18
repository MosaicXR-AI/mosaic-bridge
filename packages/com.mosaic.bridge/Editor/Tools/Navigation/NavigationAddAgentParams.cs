using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationAddAgentParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }
        public float? Speed { get; set; }
        public float? AngularSpeed { get; set; }
        public float? Radius { get; set; }
        public float? Height { get; set; }
        public float? StoppingDistance { get; set; }
    }
}
