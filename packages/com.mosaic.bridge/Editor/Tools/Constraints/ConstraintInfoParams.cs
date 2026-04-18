namespace Mosaic.Bridge.Tools.Constraints
{
    public sealed class ConstraintInfoParams
    {
        /// <summary>Name of the target GameObject. Required unless InstanceId is provided.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over Name if both are set.</summary>
        public int? InstanceId { get; set; }
    }
}
