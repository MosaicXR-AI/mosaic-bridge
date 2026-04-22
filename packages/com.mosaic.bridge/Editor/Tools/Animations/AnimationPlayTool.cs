using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationPlayTool
    {
        private const string ValidActions = "play, stop, sample";

        [MosaicTool("animation/play",
                    "Preview and sample animations in the editor: play, stop, sample at a normalized time",
                    isReadOnly: false)]
        public static ToolResult<AnimationPlayResult> Execute(AnimationPlayParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "play":   return Play(p);
                case "stop":   return Stop(p);
                case "sample": return Sample(p);
                default:
                    return ToolResult<AnimationPlayResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: {ValidActions}",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AnimationPlayResult> Play(AnimationPlayParams p)
        {
            var go = FindGameObject(p);
            if (go == null)
                return ToolResult<AnimationPlayResult>.Fail(
                    ResolveNotFoundMessage(p), ErrorCodes.NOT_FOUND);

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return ToolResult<AnimationPlayResult>.Fail(
                    $"GameObject '{go.name}' does not have an Animator component", ErrorCodes.NOT_FOUND);

            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationPlayResult>.Fail(
                    "StateName is required for 'play' action", ErrorCodes.INVALID_PARAM);

            // In editor, we use Animator.Play to start animation preview
            animator.Play(p.StateName, p.LayerIndex, 0f);
            animator.Update(0f);

            return ToolResult<AnimationPlayResult>.Ok(new AnimationPlayResult
            {
                Action         = "play",
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                StateName      = p.StateName,
                LayerIndex     = p.LayerIndex,
                NormalizedTime = 0f,
                Message        = $"Playing state '{p.StateName}' on '{go.name}'"
            });
        }

        private static ToolResult<AnimationPlayResult> Stop(AnimationPlayParams p)
        {
            var go = FindGameObject(p);
            if (go == null)
                return ToolResult<AnimationPlayResult>.Fail(
                    ResolveNotFoundMessage(p), ErrorCodes.NOT_FOUND);

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return ToolResult<AnimationPlayResult>.Fail(
                    $"GameObject '{go.name}' does not have an Animator component", ErrorCodes.NOT_FOUND);

            // Reset to bind pose by rebinding
            animator.Rebind();
            animator.Update(0f);

            return ToolResult<AnimationPlayResult>.Ok(new AnimationPlayResult
            {
                Action         = "stop",
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                Message        = $"Stopped animation on '{go.name}' and reset to bind pose"
            });
        }

        private static ToolResult<AnimationPlayResult> Sample(AnimationPlayParams p)
        {
            var go = FindGameObject(p);
            if (go == null)
                return ToolResult<AnimationPlayResult>.Fail(
                    ResolveNotFoundMessage(p), ErrorCodes.NOT_FOUND);

            float normalizedTime = p.NormalizedTime ?? 0f;

            // If a ClipPath is provided, sample that clip directly
            if (!string.IsNullOrEmpty(p.ClipPath))
            {
                var clip = AnimationToolHelpers.LoadClip(p.ClipPath);
                if (clip == null)
                    return ToolResult<AnimationPlayResult>.Fail(
                        $"AnimationClip not found at '{p.ClipPath}'", ErrorCodes.NOT_FOUND);

                float sampleTime = normalizedTime * clip.length;
                clip.SampleAnimation(go, sampleTime);

                return ToolResult<AnimationPlayResult>.Ok(new AnimationPlayResult
                {
                    Action         = "sample",
                    GameObjectName = go.name,
                    InstanceId     = go.GetInstanceID(),
                    NormalizedTime = normalizedTime,
                    Message        = $"Sampled clip '{clip.name}' at t={sampleTime:F3}s (normalized={normalizedTime:F3})"
                });
            }

            // Otherwise use the Animator to sample a state
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return ToolResult<AnimationPlayResult>.Fail(
                    $"GameObject '{go.name}' does not have an Animator component. Provide ClipPath for direct sampling.",
                    ErrorCodes.NOT_FOUND);

            string stateName = p.StateName;
            if (string.IsNullOrEmpty(stateName))
                return ToolResult<AnimationPlayResult>.Fail(
                    "StateName or ClipPath is required for 'sample' action", ErrorCodes.INVALID_PARAM);

            animator.Play(stateName, p.LayerIndex, normalizedTime);
            animator.Update(0f);

            return ToolResult<AnimationPlayResult>.Ok(new AnimationPlayResult
            {
                Action         = "sample",
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                StateName      = stateName,
                LayerIndex     = p.LayerIndex,
                NormalizedTime = normalizedTime,
                Message        = $"Sampled state '{stateName}' at normalized time {normalizedTime:F3}"
            });
        }

        private static GameObject FindGameObject(AnimationPlayParams p)
        {
            if (p.InstanceId.HasValue)
            {
#pragma warning disable CS0618
                var obj = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value) as GameObject;
#pragma warning restore CS0618
                return obj;
            }

            if (!string.IsNullOrEmpty(p.GameObjectName))
                return GameObject.Find(p.GameObjectName);

            return null;
        }

        private static string ResolveNotFoundMessage(AnimationPlayParams p)
        {
            if (p.InstanceId.HasValue)
                return $"GameObject with InstanceId {p.InstanceId.Value} not found";
            if (!string.IsNullOrEmpty(p.GameObjectName))
                return $"GameObject '{p.GameObjectName}' not found";
            return "GameObjectName or InstanceId is required";
        }
    }
}
