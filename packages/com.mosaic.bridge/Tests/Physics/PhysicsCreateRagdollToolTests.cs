using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Physics
{
    // O4 §4.7: physics/create-ragdoll — "death -> ragdoll" lesson, physics dummies. No fixture
    // Humanoid rig exists anywhere in this repo, so this builds one programmatically via
    // AvatarBuilder.BuildHumanAvatar (every HumanTrait-required bone, in a rough T-pose) purely to
    // give the tool a real Humanoid Animator to work against — the only way to exercise it for real.
    [TestFixture]
    [Category("Physics")]
    public class PhysicsCreateRagdollToolTests
    {
        private GameObject _root;
        private Dictionary<HumanBodyBones, GameObject> _bones;

        [SetUp]
        public void SetUp()
        {
            (_root, _bones) = BuildSyntheticHumanoid();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void CreateRagdoll_OnValidHumanoid_BuildsAllThirteenBones()
        {
            var result = PhysicsCreateRagdollTool.Execute(new PhysicsCreateRagdollParams
            {
                Name = "RagdollTestRoot", TotalMass = 30f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(13, result.Data.Bones.Length);

            var hips = _bones[HumanBodyBones.Hips];
            Assert.IsNotNull(hips.GetComponent<Rigidbody>());
            Assert.IsNull(hips.GetComponent<CharacterJoint>(), "the root bone must not have a joint");

            var head = _bones[HumanBodyBones.Head];
            Assert.IsNotNull(head.GetComponent<SphereCollider>());
            Assert.IsNotNull(head.GetComponent<CharacterJoint>());

            var upperArm = _bones[HumanBodyBones.LeftUpperArm];
            Assert.IsNotNull(upperArm.GetComponent<CapsuleCollider>());
            Assert.IsNotNull(upperArm.GetComponent<CharacterJoint>());
            Assert.AreEqual(_bones[HumanBodyBones.Spine].GetComponent<Rigidbody>(),
                upperArm.GetComponent<CharacterJoint>().connectedBody);
        }

        [Test]
        public void CreateRagdoll_MassDistribution_SumsToTotalMass()
        {
            var result = PhysicsCreateRagdollTool.Execute(new PhysicsCreateRagdollParams
            {
                Name = "RagdollTestRoot", TotalMass = 30f,
            });

            Assert.IsTrue(result.Success, result.Error);
            float sum = result.Data.Bones.Sum(b => b.Mass);
            Assert.AreEqual(30f, sum, 0.01f);
        }

        [Test]
        public void CreateRagdoll_NonHumanoidAnimator_ReturnsInvalidParam()
        {
            var nonHumanoid = new GameObject("NonHumanoidRoot");
            nonHumanoid.AddComponent<Animator>();

            try
            {
                var result = PhysicsCreateRagdollTool.Execute(new PhysicsCreateRagdollParams
                {
                    Name = "NonHumanoidRoot",
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(nonHumanoid);
            }
        }

        [Test]
        public void CreateRagdoll_ZeroStrength_ZeroSpring()
        {
            var result = PhysicsCreateRagdollTool.Execute(new PhysicsCreateRagdollParams
            {
                Name = "RagdollTestRoot", Strength = 0f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var joint = _bones[HumanBodyBones.Head].GetComponent<CharacterJoint>();
            Assert.AreEqual(0f, joint.twistLimitSpring.spring, 0.001f);
        }

        private static (GameObject root, Dictionary<HumanBodyBones, GameObject> bones) BuildSyntheticHumanoid()
        {
            var root = new GameObject("RagdollTestRoot");

            var bones = new Dictionary<HumanBodyBones, GameObject>();
            GameObject Bone(string name, GameObject parent, Vector3 localPos)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = localPos;
                return go;
            }

            var hips = Bone("Hips", root, new Vector3(0, 1.0f, 0));
            var spine = Bone("Spine", hips, new Vector3(0, 0.15f, 0));
            var chest = Bone("Chest", spine, new Vector3(0, 0.15f, 0));
            var neck = Bone("Neck", chest, new Vector3(0, 0.15f, 0));
            var head = Bone("Head", neck, new Vector3(0, 0.1f, 0));

            var leftShoulder = Bone("LeftShoulder", chest, new Vector3(-0.1f, 0.1f, 0));
            var leftUpperArm = Bone("LeftUpperArm", leftShoulder, new Vector3(-0.15f, 0, 0));
            var leftLowerArm = Bone("LeftLowerArm", leftUpperArm, new Vector3(-0.25f, 0, 0));
            var leftHand = Bone("LeftHand", leftLowerArm, new Vector3(-0.2f, 0, 0));

            var rightShoulder = Bone("RightShoulder", chest, new Vector3(0.1f, 0.1f, 0));
            var rightUpperArm = Bone("RightUpperArm", rightShoulder, new Vector3(0.15f, 0, 0));
            var rightLowerArm = Bone("RightLowerArm", rightUpperArm, new Vector3(0.25f, 0, 0));
            var rightHand = Bone("RightHand", rightLowerArm, new Vector3(0.2f, 0, 0));

            var leftUpperLeg = Bone("LeftUpperLeg", hips, new Vector3(-0.1f, -0.05f, 0));
            var leftLowerLeg = Bone("LeftLowerLeg", leftUpperLeg, new Vector3(0, -0.4f, 0));
            var leftFoot = Bone("LeftFoot", leftLowerLeg, new Vector3(0, -0.4f, 0));

            var rightUpperLeg = Bone("RightUpperLeg", hips, new Vector3(0.1f, -0.05f, 0));
            var rightLowerLeg = Bone("RightLowerLeg", rightUpperLeg, new Vector3(0, -0.4f, 0));
            var rightFoot = Bone("RightFoot", rightLowerLeg, new Vector3(0, -0.4f, 0));

            bones[HumanBodyBones.Hips] = hips;
            bones[HumanBodyBones.Spine] = spine;
            bones[HumanBodyBones.Chest] = chest;
            bones[HumanBodyBones.Neck] = neck;
            bones[HumanBodyBones.Head] = head;
            bones[HumanBodyBones.LeftShoulder] = leftShoulder;
            bones[HumanBodyBones.LeftUpperArm] = leftUpperArm;
            bones[HumanBodyBones.LeftLowerArm] = leftLowerArm;
            bones[HumanBodyBones.LeftHand] = leftHand;
            bones[HumanBodyBones.RightShoulder] = rightShoulder;
            bones[HumanBodyBones.RightUpperArm] = rightUpperArm;
            bones[HumanBodyBones.RightLowerArm] = rightLowerArm;
            bones[HumanBodyBones.RightHand] = rightHand;
            bones[HumanBodyBones.LeftUpperLeg] = leftUpperLeg;
            bones[HumanBodyBones.LeftLowerLeg] = leftLowerLeg;
            bones[HumanBodyBones.LeftFoot] = leftFoot;
            bones[HumanBodyBones.RightUpperLeg] = rightUpperLeg;
            bones[HumanBodyBones.RightLowerLeg] = rightLowerLeg;
            bones[HumanBodyBones.RightFoot] = rightFoot;

            var humanBones = new List<HumanBone>();
            var boneNames = HumanTrait.BoneName;
            for (int i = 0; i < HumanTrait.BoneCount; i++)
            {
                if (!HumanTrait.RequiredBone(i)) continue;
                var humanBodyBone = (HumanBodyBones)i;
                if (!bones.TryGetValue(humanBodyBone, out var go))
                    continue; // optional-but-required-by-some-versions bone we didn't create; skip
                humanBones.Add(new HumanBone
                {
                    humanName = boneNames[i],
                    boneName = go.name,
                    limit = new HumanLimit { useDefaultValues = true },
                });
            }

            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            var skeletonBones = allTransforms.Select(t => new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale,
            }).ToArray();

            var description = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeletonBones,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f,
                feetSpacing = 0f,
            };

            var avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            Assert.IsTrue(avatar != null && avatar.isValid, "synthetic Humanoid avatar failed to build/validate");

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            Assert.IsTrue(animator.isHuman, "Animator did not report isHuman after assigning the built avatar");

            return (root, bones);
        }
    }
}
