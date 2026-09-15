using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsAddJointTool
    {
        [MosaicTool("physics/add-joint",
                    "Adds a physics joint (Hinge, Fixed, Spring, Character, or Configurable) between a " +
                    "GameObject and an optional ConnectedBodyName (null connects to the world). Auto-adds " +
                    "a Rigidbody to the host if missing. Hinge: UseLimits/UseMotor/UseSpring + their params. " +
                    "Spring: MinDistance/MaxDistance/SpringForce/SpringDamper. Character: SwingAxis + " +
                    "twist/swing limits. Configurable: per-axis Motion (Free/Limited/Locked) + limits/drives.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsAddJointResult> Execute(PhysicsAddJointParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsAddJointResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            Rigidbody connectedBody = null;
            if (!string.IsNullOrEmpty(p.ConnectedBodyName))
            {
                var connectedGo = GameObject.Find(p.ConnectedBodyName);
                if (connectedGo == null)
                    return ToolResult<PhysicsAddJointResult>.Fail(
                        $"ConnectedBodyName GameObject '{p.ConnectedBodyName}' not found", ErrorCodes.NOT_FOUND);
                connectedBody = connectedGo.GetComponent<Rigidbody>();
                if (connectedBody == null)
                    return ToolResult<PhysicsAddJointResult>.Fail(
                        $"'{p.ConnectedBodyName}' has no Rigidbody — a joint needs one to connect to " +
                        "(or omit ConnectedBodyName to connect to the world).", ErrorCodes.INVALID_PARAM);
            }

            bool hostRigidbodyAdded = false;
            if (go.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(go);
                hostRigidbodyAdded = true;
            }

            Joint joint;
            switch (p.JointType?.ToLowerInvariant())
            {
                case "hinge":
                {
                    var hinge = Undo.AddComponent<HingeJoint>(go);
                    ApplyHinge(hinge, p);
                    joint = hinge;
                    break;
                }
                case "fixed":
                    joint = Undo.AddComponent<FixedJoint>(go);
                    break;
                case "spring":
                {
                    var spring = Undo.AddComponent<SpringJoint>(go);
                    ApplySpring(spring, p);
                    joint = spring;
                    break;
                }
                case "character":
                {
                    var character = Undo.AddComponent<CharacterJoint>(go);
                    ApplyCharacter(character, p);
                    joint = character;
                    break;
                }
                case "configurable":
                {
                    var configurable = Undo.AddComponent<ConfigurableJoint>(go);
                    if (!ApplyConfigurable(configurable, p, out var configError))
                        return ToolResult<PhysicsAddJointResult>.Fail(configError, ErrorCodes.INVALID_PARAM);
                    joint = configurable;
                    break;
                }
                default:
                    return ToolResult<PhysicsAddJointResult>.Fail(
                        $"Unknown JointType '{p.JointType}'. Valid: Hinge, Fixed, Spring, Character, Configurable",
                        ErrorCodes.INVALID_PARAM);
            }

            joint.connectedBody = connectedBody;
            if (p.Anchor != null)
            {
                if (p.Anchor.Length != 3)
                    return ToolResult<PhysicsAddJointResult>.Fail("Anchor requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                joint.anchor = new Vector3(p.Anchor[0], p.Anchor[1], p.Anchor[2]);
            }
            if (p.AutoConfigureConnectedAnchor.HasValue)
                joint.autoConfigureConnectedAnchor = p.AutoConfigureConnectedAnchor.Value;
            if (p.ConnectedAnchor != null)
            {
                if (p.ConnectedAnchor.Length != 3)
                    return ToolResult<PhysicsAddJointResult>.Fail("ConnectedAnchor requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                joint.connectedAnchor = new Vector3(p.ConnectedAnchor[0], p.ConnectedAnchor[1], p.ConnectedAnchor[2]);
                joint.autoConfigureConnectedAnchor = false;
            }
            if (p.Axis != null)
            {
                if (p.Axis.Length != 3)
                    return ToolResult<PhysicsAddJointResult>.Fail("Axis requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                joint.axis = new Vector3(p.Axis[0], p.Axis[1], p.Axis[2]);
            }
            if (p.BreakForce.HasValue) joint.breakForce = p.BreakForce.Value;
            if (p.BreakTorque.HasValue) joint.breakTorque = p.BreakTorque.Value;
            if (p.EnableCollision.HasValue) joint.enableCollision = p.EnableCollision.Value;

            return ToolResult<PhysicsAddJointResult>.Ok(new PhysicsAddJointResult
            {
                GameObjectName     = go.name,
                InstanceId         = UnityIds.Of(go),
                JointType          = p.JointType,
                ConnectedBodyName  = p.ConnectedBodyName,
                HostRigidbodyAdded = hostRigidbodyAdded,
                Anchor             = PhysicsToolHelpers.ToFloatArray(joint.anchor),
                ConnectedAnchor    = PhysicsToolHelpers.ToFloatArray(joint.connectedAnchor),
                Message            = $"Added {p.JointType} joint to '{go.name}'" +
                                      (connectedBody != null ? $", connected to '{p.ConnectedBodyName}'." : ", connected to the world."),
            });
        }

        private static void ApplyHinge(HingeJoint hinge, PhysicsAddJointParams p)
        {
            if (p.UseLimits.HasValue) hinge.useLimits = p.UseLimits.Value;
            if (p.LimitMin.HasValue || p.LimitMax.HasValue || p.LimitBounciness.HasValue)
            {
                var limits = hinge.limits;
                if (p.LimitMin.HasValue) limits.min = p.LimitMin.Value;
                if (p.LimitMax.HasValue) limits.max = p.LimitMax.Value;
                if (p.LimitBounciness.HasValue) limits.bounciness = p.LimitBounciness.Value;
                hinge.limits = limits;
            }

            if (p.UseMotor.HasValue) hinge.useMotor = p.UseMotor.Value;
            if (p.MotorForce.HasValue || p.MotorTargetVelocity.HasValue || p.MotorFreeSpin.HasValue)
            {
                var motor = hinge.motor;
                if (p.MotorForce.HasValue) motor.force = p.MotorForce.Value;
                if (p.MotorTargetVelocity.HasValue) motor.targetVelocity = p.MotorTargetVelocity.Value;
                if (p.MotorFreeSpin.HasValue) motor.freeSpin = p.MotorFreeSpin.Value;
                hinge.motor = motor;
            }

            if (p.UseSpring.HasValue) hinge.useSpring = p.UseSpring.Value;
            if (p.SpringForce.HasValue || p.SpringDamper.HasValue || p.SpringTargetPosition.HasValue)
            {
                var spring = hinge.spring;
                if (p.SpringForce.HasValue) spring.spring = p.SpringForce.Value;
                if (p.SpringDamper.HasValue) spring.damper = p.SpringDamper.Value;
                if (p.SpringTargetPosition.HasValue) spring.targetPosition = p.SpringTargetPosition.Value;
                hinge.spring = spring;
            }
        }

        private static void ApplySpring(SpringJoint spring, PhysicsAddJointParams p)
        {
            if (p.SpringForce.HasValue) spring.spring = p.SpringForce.Value;
            if (p.SpringDamper.HasValue) spring.damper = p.SpringDamper.Value;
            if (p.MinDistance.HasValue) spring.minDistance = p.MinDistance.Value;
            if (p.MaxDistance.HasValue) spring.maxDistance = p.MaxDistance.Value;
            if (p.Tolerance.HasValue) spring.tolerance = p.Tolerance.Value;
        }

        private static void ApplyCharacter(CharacterJoint character, PhysicsAddJointParams p)
        {
            if (p.SwingAxis != null && p.SwingAxis.Length == 3)
                character.swingAxis = new Vector3(p.SwingAxis[0], p.SwingAxis[1], p.SwingAxis[2]);

            if (p.TwistLimitMin.HasValue)
            {
                var low = character.lowTwistLimit;
                low.limit = p.TwistLimitMin.Value;
                character.lowTwistLimit = low;
            }
            if (p.TwistLimitMax.HasValue)
            {
                var high = character.highTwistLimit;
                high.limit = p.TwistLimitMax.Value;
                character.highTwistLimit = high;
            }
            if (p.Swing1Limit.HasValue)
            {
                var swing1 = character.swing1Limit;
                swing1.limit = p.Swing1Limit.Value;
                character.swing1Limit = swing1;
            }
            if (p.Swing2Limit.HasValue)
            {
                var swing2 = character.swing2Limit;
                swing2.limit = p.Swing2Limit.Value;
                character.swing2Limit = swing2;
            }
        }

        private static bool ApplyConfigurable(ConfigurableJoint configurable, PhysicsAddJointParams p, out string error)
        {
            error = null;

            if (!TryApplyMotion(p.XMotion, m => configurable.xMotion = m, out error)) return false;
            if (!TryApplyMotion(p.YMotion, m => configurable.yMotion = m, out error)) return false;
            if (!TryApplyMotion(p.ZMotion, m => configurable.zMotion = m, out error)) return false;
            if (!TryApplyMotion(p.AngularXMotion, m => configurable.angularXMotion = m, out error)) return false;
            if (!TryApplyMotion(p.AngularYMotion, m => configurable.angularYMotion = m, out error)) return false;
            if (!TryApplyMotion(p.AngularZMotion, m => configurable.angularZMotion = m, out error)) return false;

            if (p.LinearLimit.HasValue)
            {
                var limit = configurable.linearLimit;
                limit.limit = p.LinearLimit.Value;
                configurable.linearLimit = limit;
            }
            if (p.LowAngularXLimit.HasValue)
            {
                var limit = configurable.lowAngularXLimit;
                limit.limit = p.LowAngularXLimit.Value;
                configurable.lowAngularXLimit = limit;
            }
            if (p.HighAngularXLimit.HasValue)
            {
                var limit = configurable.highAngularXLimit;
                limit.limit = p.HighAngularXLimit.Value;
                configurable.highAngularXLimit = limit;
            }
            if (p.AngularYLimit.HasValue)
            {
                var limit = configurable.angularYLimit;
                limit.limit = p.AngularYLimit.Value;
                configurable.angularYLimit = limit;
            }
            if (p.AngularZLimit.HasValue)
            {
                var limit = configurable.angularZLimit;
                limit.limit = p.AngularZLimit.Value;
                configurable.angularZLimit = limit;
            }

            if (!TryApplyDrive(p.XDrive, d => configurable.xDrive = d, out error)) return false;
            if (!TryApplyDrive(p.YDrive, d => configurable.yDrive = d, out error)) return false;
            if (!TryApplyDrive(p.ZDrive, d => configurable.zDrive = d, out error)) return false;
            if (!TryApplyDrive(p.AngularXDrive, d => configurable.angularXDrive = d, out error)) return false;
            if (!TryApplyDrive(p.AngularYZDrive, d => configurable.angularYZDrive = d, out error)) return false;

            return true;
        }

        private static bool TryApplyMotion(string value, System.Action<ConfigurableJointMotion> apply, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(value)) return true;
            if (!System.Enum.TryParse<ConfigurableJointMotion>(value, ignoreCase: true, out var motion))
            {
                error = $"Unknown motion '{value}'. Valid: Free, Limited, Locked";
                return false;
            }
            apply(motion);
            return true;
        }

        private static bool TryApplyDrive(float[] values, System.Action<JointDrive> apply, out string error)
        {
            error = null;
            if (values == null) return true;
            if (values.Length != 3)
            {
                error = "A Drive requires exactly [positionSpring, positionDamper, maximumForce]";
                return false;
            }
            apply(new JointDrive { positionSpring = values[0], positionDamper = values[1], maximumForce = values[2] });
            return true;
        }
    }
}
