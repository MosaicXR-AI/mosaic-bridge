using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsSetGravityParams
    {
        /// <summary>New gravity vector [x,y,z].</summary>
        [Required] public float[] Gravity { get; set; }
    }
}
