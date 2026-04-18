namespace Mosaic.Bridge.Tools.Prefabs
{
    public sealed class PrefabGetOverridesParams
    {
        /// <summary>Instance ID of the prefab instance GameObject.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of the prefab instance GameObject (used if InstanceId is not provided).</summary>
        public string GameObjectName { get; set; }
    }
}
