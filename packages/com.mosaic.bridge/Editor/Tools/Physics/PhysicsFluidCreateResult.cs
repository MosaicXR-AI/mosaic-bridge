namespace Mosaic.Bridge.Tools.Physics
{
    /// <summary>Result for <c>physics/fluid-create</c>.</summary>
    public sealed class PhysicsFluidCreateResult
    {
        /// <summary>Asset path of the generated MonoBehaviour script (starts with "Assets/").</summary>
        public string ScriptPath { get; set; }

        /// <summary>Name of the spawned GameObject.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Unity InstanceID of the spawned GameObject.</summary>
        public int InstanceId { get; set; }

        /// <summary>Resolved fluid type ("smoke", "liquid", or "fire").</summary>
        public string Type { get; set; }

        /// <summary>Resolved grid resolution (after clamping).</summary>
        public int Resolution { get; set; }
    }
}
