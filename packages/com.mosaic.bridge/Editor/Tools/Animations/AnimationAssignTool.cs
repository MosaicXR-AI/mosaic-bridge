using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;
using Mosaic.Bridge.Core.Scenes;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationAssignTool
    {
        [MosaicTool("animation/assign",
                    "Assigns an AnimatorController and Avatar to a GameObject's Animator component (creating " +
                    "one if it doesn't exist), plus ApplyRootMotion/UpdateMode/CullingMode. Set Legacy=true to " +
                    "target the deprecated UnityEngine.Animation component instead, assigning ClipPath/ClipName " +
                    "as its clip. Avatar is typically an FBX sub-asset — pass the model's own asset path.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AnimationAssignResult> Execute(AnimationAssignParams p)
        {
            if (!GameObjectResolver.TryResolve(p.InstanceId, p.GameObjectName, out var go, out var resolveError))
                return ToolResult<AnimationAssignResult>.Fail(resolveError, ErrorCodes.NOT_FOUND);

            if (p.Legacy)
                return AssignLegacy(go, p);
            return AssignAnimator(go, p);
        }

        private static ToolResult<AnimationAssignResult> AssignAnimator(GameObject go, AnimationAssignParams p)
        {
            UnityEditor.Animations.AnimatorController controller = null;
            if (!string.IsNullOrEmpty(p.ControllerPath))
            {
                controller = AnimationToolHelpers.LoadController(p.ControllerPath);
                if (controller == null)
                    return ToolResult<AnimationAssignResult>.Fail(
                        $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);
            }

            Avatar avatar = null;
            if (!string.IsNullOrEmpty(p.AvatarPath))
            {
                avatar = AssetDatabase.LoadAssetAtPath<Avatar>(p.AvatarPath);
                if (avatar == null)
                    return ToolResult<AnimationAssignResult>.Fail(
                        $"Avatar not found at '{p.AvatarPath}' — for an FBX, the Avatar is a sub-asset created " +
                        "by the model importer (see model/set-import-settings).", ErrorCodes.NOT_FOUND);
            }

            AnimatorUpdateMode? updateMode = null;
            if (!string.IsNullOrEmpty(p.UpdateMode))
            {
                if (!System.Enum.TryParse<AnimatorUpdateMode>(p.UpdateMode, true, out var parsed))
                    return ToolResult<AnimationAssignResult>.Fail(
                        $"Unknown UpdateMode '{p.UpdateMode}'. Valid: Normal, Fixed, UnscaledTime " +
                        "(Fixed replaces the deprecated AnimatePhysics)",
                        ErrorCodes.INVALID_PARAM);
                updateMode = parsed;
            }

            AnimatorCullingMode? cullingMode = null;
            if (!string.IsNullOrEmpty(p.CullingMode))
            {
                if (!System.Enum.TryParse<AnimatorCullingMode>(p.CullingMode, true, out var parsed))
                    return ToolResult<AnimationAssignResult>.Fail(
                        $"Unknown CullingMode '{p.CullingMode}'. Valid: AlwaysAnimate, CullUpdateTransforms, CullCompletely",
                        ErrorCodes.INVALID_PARAM);
                cullingMode = parsed;
            }

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(go);
            else
                Undo.RecordObject(animator, "Mosaic: Assign Animator");

            if (controller != null) animator.runtimeAnimatorController = controller;
            if (avatar != null) animator.avatar = avatar;
            if (p.ApplyRootMotion.HasValue) animator.applyRootMotion = p.ApplyRootMotion.Value;
            if (updateMode.HasValue) animator.updateMode = updateMode.Value;
            if (cullingMode.HasValue) animator.cullingMode = cullingMode.Value;

            EditorUtility.SetDirty(animator);

            return ToolResult<AnimationAssignResult>.Ok(new AnimationAssignResult
            {
                GameObjectName = go.name,
                InstanceId = UnityIds.Of(go),
                Legacy = false,
                ControllerPath = controller != null ? p.ControllerPath : (animator.runtimeAnimatorController != null
                    ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) : null),
                AvatarPath = avatar != null ? p.AvatarPath : (animator.avatar != null
                    ? AssetDatabase.GetAssetPath(animator.avatar) : null),
                ApplyRootMotion = animator.applyRootMotion,
                UpdateMode = animator.updateMode.ToString(),
                CullingMode = animator.cullingMode.ToString(),
            });
        }

        private static ToolResult<AnimationAssignResult> AssignLegacy(GameObject go, AnimationAssignParams p)
        {
            if (string.IsNullOrEmpty(p.ClipPath))
                return ToolResult<AnimationAssignResult>.Fail(
                    "ClipPath is required when Legacy is true", ErrorCodes.INVALID_PARAM);

            var clip = AnimationToolHelpers.LoadClip(p.ClipPath, p.ClipName);
            if (clip == null)
            {
                if (!string.IsNullOrEmpty(p.ClipName))
                {
                    var names = AnimationToolHelpers.ListClipNames(p.ClipPath);
                    return ToolResult<AnimationAssignResult>.Fail(
                        names.Length == 0
                            ? $"No AnimationClip found at '{p.ClipPath}'."
                            : $"No clip named '{p.ClipName}' at '{p.ClipPath}'. Available: {string.Join(", ", names)}",
                        ErrorCodes.NOT_FOUND);
                }
                return ToolResult<AnimationAssignResult>.Fail(
                    $"AnimationClip not found at '{p.ClipPath}'", ErrorCodes.NOT_FOUND);
            }

            var animation = go.GetComponent<Animation>();
            if (animation == null)
                animation = Undo.AddComponent<Animation>(go);
            else
                Undo.RecordObject(animation, "Mosaic: Assign Legacy Animation");

            animation.clip = clip;
            AnimationUtility.SetAnimationClips(animation, new[] { clip });
            EditorUtility.SetDirty(animation);

            return ToolResult<AnimationAssignResult>.Ok(new AnimationAssignResult
            {
                GameObjectName = go.name,
                InstanceId = UnityIds.Of(go),
                Legacy = true,
                ClipPath = p.ClipPath,
                ClipName = clip.name,
            });
        }
    }
}
