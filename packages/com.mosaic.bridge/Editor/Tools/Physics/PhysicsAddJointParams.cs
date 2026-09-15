using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddJointParams
    {
        /// <summary>Name of the target GameObject (the joint host). Used if InstanceId is not set.</summary>
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>"Hinge", "Fixed", "Spring", "Character", or "Configurable".</summary>
        [Required] public string JointType { get; set; }

        /// <summary>Name of the GameObject whose Rigidbody this joint connects to. Null = connect to the world.</summary>
        public string ConnectedBodyName { get; set; }

        public float[] Anchor { get; set; }
        public float[] ConnectedAnchor { get; set; }
        /// <summary>When true, Unity computes ConnectedAnchor automatically from Anchor's world position.</summary>
        public bool? AutoConfigureConnectedAnchor { get; set; }

        /// <summary>Hinge/Spring/Character/Configurable rotation/constraint axis.</summary>
        public float[] Axis { get; set; }

        public float? BreakForce { get; set; }
        public float? BreakTorque { get; set; }
        public bool? EnableCollision { get; set; }

        // -- Hinge --
        public bool? UseLimits { get; set; }
        public float? LimitMin { get; set; }
        public float? LimitMax { get; set; }
        public float? LimitBounciness { get; set; }
        public bool? UseMotor { get; set; }
        public float? MotorForce { get; set; }
        public float? MotorTargetVelocity { get; set; }
        public bool? MotorFreeSpin { get; set; }
        public bool? UseSpring { get; set; }

        // -- Hinge spring / Spring joint (shared field names, different joint types) --
        public float? SpringForce { get; set; }
        public float? SpringDamper { get; set; }
        /// <summary>Hinge spring target angle in degrees.</summary>
        public float? SpringTargetPosition { get; set; }

        // -- Spring joint --
        public float? MinDistance { get; set; }
        public float? MaxDistance { get; set; }
        public float? Tolerance { get; set; }

        // -- Character joint --
        public float[] SwingAxis { get; set; }
        public float? TwistLimitMin { get; set; }
        public float? TwistLimitMax { get; set; }
        public float? Swing1Limit { get; set; }
        public float? Swing2Limit { get; set; }

        // -- Configurable joint --
        /// <summary>"Free", "Limited", or "Locked".</summary>
        public string XMotion { get; set; }
        public string YMotion { get; set; }
        public string ZMotion { get; set; }
        public string AngularXMotion { get; set; }
        public string AngularYMotion { get; set; }
        public string AngularZMotion { get; set; }
        public float? LinearLimit { get; set; }
        public float? LowAngularXLimit { get; set; }
        public float? HighAngularXLimit { get; set; }
        public float? AngularYLimit { get; set; }
        public float? AngularZLimit { get; set; }
        /// <summary>[positionSpring, positionDamper, maximumForce].</summary>
        public float[] XDrive { get; set; }
        public float[] YDrive { get; set; }
        public float[] ZDrive { get; set; }
        public float[] AngularXDrive { get; set; }
        public float[] AngularYZDrive { get; set; }
    }
}
