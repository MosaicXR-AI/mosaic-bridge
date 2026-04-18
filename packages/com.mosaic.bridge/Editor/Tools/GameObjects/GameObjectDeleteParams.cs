using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectDeleteParams
    {
        /// <summary>Name of the GameObject to delete. Required unless InstanceId is provided.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the GameObject to delete. Takes priority over Name if both are set.</summary>
        public int? InstanceId { get; set; }
    }
}
