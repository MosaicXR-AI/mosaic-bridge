using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Constraints
{
    public sealed class ConstraintAddParams
    {
        /// <summary>Name of the target GameObject. Required unless InstanceId is provided.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over Name if both are set.</summary>
        public int? InstanceId { get; set; }
        /// <summary>Constraint type: Position, Rotation, Scale, Aim, or Parent.</summary>
        [Required] public string Type { get; set; }
        /// <summary>Optional instance ID of the source/target GameObject for the constraint.</summary>
        public int? SourceInstanceId { get; set; }
    }
}
