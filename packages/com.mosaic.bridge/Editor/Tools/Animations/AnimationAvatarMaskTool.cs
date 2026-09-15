using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Scenes;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationAvatarMaskTool
    {
        private const string ValidActions = "create, set-body-part, add-transform-path, remove-transform-path, info";

        [MosaicTool("animation/avatar-mask",
                    "Manages AvatarMask assets, used by animation/controller's set-layer AvatarMaskPath for " +
                    "override layers ('upper-body aim while running' — mask out the legs on an additive/override " +
                    "layer). set-body-part toggles a humanoid body part (Root/Body/Head/LeftArm/...); " +
                    "add-transform-path/remove-transform-path mask specific transforms by path, resolved against " +
                    "a scene GameObject's hierarchy (RootGameObjectName + TransformPath, Transform.Find syntax) — " +
                    "typically an imported rig placed in the scene.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AnimationAvatarMaskResult> Execute(AnimationAvatarMaskParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":                return Create(p);
                case "set-body-part":         return SetBodyPart(p);
                case "add-transform-path":    return AddTransformPath(p);
                case "remove-transform-path": return RemoveTransformPath(p);
                case "info":                  return Info(p);
                default:
                    return Fail($"Unknown action '{p.Action}'. Valid actions: {ValidActions}");
            }
        }

        private static ToolResult<AnimationAvatarMaskResult> Fail(string message, string code = ErrorCodes.INVALID_PARAM) =>
            ToolResult<AnimationAvatarMaskResult>.Fail(message, code);

        private static ToolResult<AnimationAvatarMaskResult> Create(AnimationAvatarMaskParams p)
        {
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(p.MaskPath) != null)
                return Fail($"An AvatarMask already exists at '{p.MaskPath}'.", ErrorCodes.CONFLICT);

            AnimationToolHelpers.EnsureDirectoryExists(p.MaskPath);
            var mask = new AvatarMask();
            // Match the Editor's own "Create > Avatar Mask" default: every humanoid body part on.
            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                mask.SetHumanoidBodyPartActive(part, true);
            }
            AssetDatabase.CreateAsset(mask, p.MaskPath);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationAvatarMaskResult>.Ok(new AnimationAvatarMaskResult
            {
                Action = "create",
                MaskPath = p.MaskPath,
            });
        }

        private static ToolResult<AnimationAvatarMaskResult> SetBodyPart(AnimationAvatarMaskParams p)
        {
            if (string.IsNullOrEmpty(p.BodyPart))
                return Fail("BodyPart is required for 'set-body-part' action");
            if (!p.Active.HasValue)
                return Fail("Active is required for 'set-body-part' action");

            var mask = LoadMask(p.MaskPath, out var loadError);
            if (mask == null) return Fail(loadError, ErrorCodes.NOT_FOUND);

            if (!System.Enum.TryParse<AvatarMaskBodyPart>(p.BodyPart, true, out var part) ||
                part == AvatarMaskBodyPart.LastBodyPart)
                return Fail($"Unknown BodyPart '{p.BodyPart}'. Valid: " +
                    string.Join(", ", System.Enum.GetValues(typeof(AvatarMaskBodyPart)).Cast<AvatarMaskBodyPart>()
                        .Where(v => v != AvatarMaskBodyPart.LastBodyPart)));

            Undo.RecordObject(mask, "Mosaic: Set AvatarMask Body Part");
            mask.SetHumanoidBodyPartActive(part, p.Active.Value);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationAvatarMaskResult>.Ok(new AnimationAvatarMaskResult
            {
                Action = "set-body-part",
                MaskPath = p.MaskPath,
                BodyPart = part.ToString(),
                Active = p.Active,
            });
        }

        private static ToolResult<AnimationAvatarMaskResult> AddTransformPath(AnimationAvatarMaskParams p)
        {
            var mask = LoadMask(p.MaskPath, out var loadError);
            if (mask == null) return Fail(loadError, ErrorCodes.NOT_FOUND);

            if (!TryResolveTransform(p, out var transform, out var resolveError))
                return Fail(resolveError, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(mask, "Mosaic: Add AvatarMask Transform Path");
            mask.AddTransformPath(transform, p.Recursive);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationAvatarMaskResult>.Ok(new AnimationAvatarMaskResult
            {
                Action = "add-transform-path",
                MaskPath = p.MaskPath,
                TransformPath = p.TransformPath,
                TransformCount = mask.transformCount,
            });
        }

        private static ToolResult<AnimationAvatarMaskResult> RemoveTransformPath(AnimationAvatarMaskParams p)
        {
            var mask = LoadMask(p.MaskPath, out var loadError);
            if (mask == null) return Fail(loadError, ErrorCodes.NOT_FOUND);

            if (!TryResolveTransform(p, out var transform, out var resolveError))
                return Fail(resolveError, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(mask, "Mosaic: Remove AvatarMask Transform Path");
            mask.RemoveTransformPath(transform, p.Recursive);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationAvatarMaskResult>.Ok(new AnimationAvatarMaskResult
            {
                Action = "remove-transform-path",
                MaskPath = p.MaskPath,
                TransformPath = p.TransformPath,
                TransformCount = mask.transformCount,
            });
        }

        private static ToolResult<AnimationAvatarMaskResult> Info(AnimationAvatarMaskParams p)
        {
            var mask = LoadMask(p.MaskPath, out var loadError);
            if (mask == null) return Fail(loadError, ErrorCodes.NOT_FOUND);

            var bodyParts = System.Enum.GetValues(typeof(AvatarMaskBodyPart)).Cast<AvatarMaskBodyPart>()
                .Where(part => part != AvatarMaskBodyPart.LastBodyPart)
                .Select(part => new AvatarMaskBodyPartInfo
                {
                    Part = part.ToString(),
                    Active = mask.GetHumanoidBodyPartActive(part),
                }).ToArray();

            var transformPaths = new AvatarMaskTransformInfo[mask.transformCount];
            for (int i = 0; i < mask.transformCount; i++)
                transformPaths[i] = new AvatarMaskTransformInfo
                {
                    Path = mask.GetTransformPath(i),
                    Active = mask.GetTransformActive(i),
                };

            return ToolResult<AnimationAvatarMaskResult>.Ok(new AnimationAvatarMaskResult
            {
                Action = "info",
                MaskPath = p.MaskPath,
                TransformCount = mask.transformCount,
                BodyParts = bodyParts,
                TransformPaths = transformPaths,
            });
        }

        private static AvatarMask LoadMask(string path, out string error)
        {
            error = null;
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null) error = $"AvatarMask not found at '{path}'";
            return mask;
        }

        private static bool TryResolveTransform(AnimationAvatarMaskParams p, out Transform transform, out string error)
        {
            transform = null;
            if (!GameObjectResolver.TryResolve(p.RootInstanceId, p.RootGameObjectName, out var root, out error))
                return false;

            if (string.IsNullOrEmpty(p.TransformPath))
            {
                transform = root.transform;
                return true;
            }
            transform = root.transform.Find(p.TransformPath);
            if (transform == null)
            {
                error = $"'{root.name}' has no child at path '{p.TransformPath}'.";
                return false;
            }
            return true;
        }
    }
}
