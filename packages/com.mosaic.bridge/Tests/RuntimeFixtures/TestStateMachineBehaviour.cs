using UnityEngine;

namespace Mosaic.Bridge.Tests.Animations
{
    /// <summary>Test fixture for AnimationControllerTests.AddBehaviour_AttachesTheCompiledBehaviourType.
    /// StateMachineBehaviour must be compiled into an assembly that ships in player builds —
    /// AnimatorController.AddStateMachineBehaviour resolves the type's MonoScript and refuses
    /// types from Editor-only assemblies (fails with "Can't find monoscript for class ..."),
    /// so this lives in its own runtime-visible asmdef instead of the Editor-only Tests one,
    /// gated by UNITY_INCLUDE_TESTS so it never ships in a real customer build.</summary>
    public class TestStateMachineBehaviour : StateMachineBehaviour { }
}
