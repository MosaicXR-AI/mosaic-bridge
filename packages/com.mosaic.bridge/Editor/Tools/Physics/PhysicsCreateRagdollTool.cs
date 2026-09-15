using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsCreateRagdollTool
    {
        // The classic Unity Ragdoll Wizard's 13-transform slot set (Pelvis, Left/Right Hips-Knee-
        // Foot, Left/Right Arm-Elbow, Middle Spine, Head), expressed via the PUBLIC HumanBodyBones
        // enum since RagdollBuilder itself is a sealed/internal ScriptableWizard with no public API
        // (docs.unity3d.com/Manual/wizard-RagdollWizard.html). Mass fractions are approximate
        // biomechanical body-segment proportions, normalized to TotalMass.
        private static readonly (HumanBodyBones bone, HumanBodyBones? parent, HumanBodyBones? child, float massFraction)[] Chain =
        {
            (HumanBodyBones.Hips,          null,                          null,                        0.15f),
            (HumanBodyBones.Spine,         HumanBodyBones.Hips,           HumanBodyBones.Head,         0.10f),
            (HumanBodyBones.Head,          HumanBodyBones.Spine,          null,                        0.07f),
            (HumanBodyBones.LeftUpperArm,  HumanBodyBones.Spine,          HumanBodyBones.LeftLowerArm, 0.05f),
            (HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftUpperArm,   HumanBodyBones.LeftHand,     0.04f),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.Spine,          HumanBodyBones.RightLowerArm,0.05f),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm,  HumanBodyBones.RightHand,    0.04f),
            (HumanBodyBones.LeftUpperLeg,  HumanBodyBones.Hips,           HumanBodyBones.LeftLowerLeg, 0.12f),
            (HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftUpperLeg,   HumanBodyBones.LeftFoot,     0.08f),
            (HumanBodyBones.LeftFoot,      HumanBodyBones.LeftLowerLeg,   null,                        0.03f),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips,           HumanBodyBones.RightLowerLeg,0.12f),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg,  HumanBodyBones.RightFoot,    0.08f),
            (HumanBodyBones.RightFoot,     HumanBodyBones.RightLowerLeg,  null,                        0.03f),
        };

        [MosaicTool("physics/create-ragdoll",
                    "Builds a physics ragdoll (Rigidbody + Collider + CharacterJoint chain) on a Humanoid " +
                    "Animator's 13 canonical bones (the classic Ragdoll Wizard's slot set — Unity's own " +
                    "wizard has no public/scriptable API). Colliders auto-fit from bone-to-child-bone " +
                    "distances; TotalMass is distributed by body-segment proportion; Strength scales joint " +
                    "limit-spring stiffness (0 = fully limp).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsCreateRagdollResult> Execute(PhysicsCreateRagdollParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsCreateRagdollResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var animator = go.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
                return ToolResult<PhysicsCreateRagdollResult>.Fail(
                    $"'{go.name}' needs an Animator with a Humanoid avatar to build a ragdoll.", ErrorCodes.INVALID_PARAM);

            // Some "child" references (e.g. LeftHand/RightHand, used only to size the forearm
            // capsule's direction/length) are not themselves ragdoll bones in Chain — resolve the
            // union of every bone AND child reference, not just Chain's own bone column.
            var neededBones = new HashSet<HumanBodyBones>(Chain.Select(c => c.bone));
            foreach (var (_, _, child, _) in Chain)
                if (child.HasValue) neededBones.Add(child.Value);

            var transforms = new Dictionary<HumanBodyBones, Transform>();
            foreach (var bone in neededBones)
            {
                var t = animator.GetBoneTransform(bone);
                if (t == null)
                    return ToolResult<PhysicsCreateRagdollResult>.Fail(
                        $"Humanoid avatar on '{go.name}' has no mapped bone for {bone}.", ErrorCodes.INVALID_PARAM);
                transforms[bone] = t;
            }

            float massSum = Chain.Sum(c => c.massFraction);
            var rigidbodies = new Dictionary<HumanBodyBones, Rigidbody>();

            foreach (var (bone, _, _, massFraction) in Chain)
            {
                var t = transforms[bone];
                var rb = GetOrAddComponent<Rigidbody>(t.gameObject);
                rb.mass = p.TotalMass * (massFraction / massSum);
                rigidbodies[bone] = rb;
            }

            var boneInfos = new List<RagdollBoneInfo>();
            foreach (var (bone, parent, child, massFraction) in Chain)
            {
                var t = transforms[bone];
                string colliderType = AddColliderFor(t, child.HasValue ? transforms[child.Value] : null, bone);

                bool hasJoint = false;
                if (parent.HasValue)
                {
                    var joint = GetOrAddComponent<CharacterJoint>(t.gameObject);
                    joint.connectedBody = rigidbodies[parent.Value];
                    ConfigureJointLimits(joint, p.Strength);
                    hasJoint = true;
                }

                boneInfos.Add(new RagdollBoneInfo
                {
                    BoneName     = bone.ToString(),
                    Mass         = rigidbodies[bone].mass,
                    ColliderType = colliderType,
                    HasJoint     = hasJoint,
                });
            }

            return ToolResult<PhysicsCreateRagdollResult>.Ok(new PhysicsCreateRagdollResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                Bones          = boneInfos.ToArray(),
                Message        = $"Built a {Chain.Length}-bone ragdoll on '{go.name}' ({p.TotalMass}kg total).",
            });
        }

        private static string AddColliderFor(Transform bone, Transform child, HumanBodyBones kind)
        {
            if (bone.GetComponent<Collider>() != null)
                return bone.GetComponent<Collider>().GetType().Name;

            if (kind == HumanBodyBones.Head)
            {
                var sphere = AddComponentWithUndo<SphereCollider>(bone.gameObject);
                sphere.radius = 0.12f;
                return "SphereCollider";
            }

            if (child == null)
            {
                // Feet and any other childless bone in the chain — a small fixed box, since there
                // is no distal reference point to size a capsule from.
                var box = AddComponentWithUndo<BoxCollider>(bone.gameObject);
                box.size = new Vector3(0.1f, 0.05f, 0.2f);
                return "BoxCollider";
            }

            var localToChild = bone.InverseTransformPoint(child.position);
            float length = localToChild.magnitude;
            if (length < 0.001f)
            {
                var fallback = AddComponentWithUndo<SphereCollider>(bone.gameObject);
                fallback.radius = 0.08f;
                return "SphereCollider";
            }

            int axis = AbsMaxAxis(localToChild);
            var capsule = AddComponentWithUndo<CapsuleCollider>(bone.gameObject);
            capsule.direction = axis;
            capsule.height = length;
            capsule.radius = length * 0.2f;
            capsule.center = localToChild * 0.5f;
            return "CapsuleCollider";
        }

        // The classic UnityEngine.Object "fake null" trap: t.GetComponent<T>() ?? AddComponent...
        // uses C#'s ?? operator, which checks for a genuine null REFERENCE and bypasses Object's
        // overloaded == — so GetComponent<T>() returning a destroyed-but-non-null-reference
        // Component (which reports as null via == but not via ??) makes ?? silently keep that
        // fake-null value instead of ever calling AddComponent. Reproduced consistently building a
        // 13-bone ragdoll chain; an explicit != null check (which DOES use the overload) is the fix.
        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;

            var component = go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(component, $"Mosaic: Add {typeof(T).Name}");
            return component;
        }

        private static T AddComponentWithUndo<T>(GameObject go) where T : Component
        {
            var component = go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(component, $"Mosaic: Add {typeof(T).Name}");
            return component;
        }

        private static int AbsMaxAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return 0;
            if (ay >= ax && ay >= az) return 1;
            return 2;
        }

        private static void ConfigureJointLimits(CharacterJoint joint, float strength)
        {
            const float twistDegrees = 20f;
            const float swingDegrees = 40f;
            var spring = new SoftJointLimitSpring { spring = strength * 1000f, damper = strength * 10f };

            var low = joint.lowTwistLimit; low.limit = -twistDegrees; joint.lowTwistLimit = low;
            var high = joint.highTwistLimit; high.limit = twistDegrees; joint.highTwistLimit = high;
            var swing1 = joint.swing1Limit; swing1.limit = swingDegrees; joint.swing1Limit = swing1;
            var swing2 = joint.swing2Limit; swing2.limit = swingDegrees; joint.swing2Limit = swing2;
            joint.twistLimitSpring = spring;
            joint.swingLimitSpring = spring;
            joint.enableProjection = true;
        }
    }
}
