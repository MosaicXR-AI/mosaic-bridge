using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Models
{
    public static class ModelSetImportSettingsTool
    {
        [MosaicTool("model/set-import-settings",
                    "Sets FBX/model import settings: animation type + avatar setup (Mixamo FBX as " +
                    "Humanoid), global scale (fix for '100x too big/small' models), material import " +
                    "mode (fix for 'pink model' — unresolved materials), mesh compression, readability, " +
                    "colliders, secondary UVs, and splitting one long take into named clips with loop " +
                    "flags (Clips[]). ExtractTextures/SearchAndRemapMaterials run last.",
                    isReadOnly: false)]
        public static ToolResult<ModelSetImportSettingsResult> SetImportSettings(ModelSetImportSettingsParams p)
        {
            var importer = AssetImporter.GetAtPath(p.AssetPath) as ModelImporter;
            if (importer == null)
                return ToolResult<ModelSetImportSettingsResult>.Fail(
                    $"No model found at '{p.AssetPath}'. Ensure the path is a valid model asset (FBX, etc.).",
                    ErrorCodes.NOT_FOUND);

            if (!string.IsNullOrEmpty(p.AnimationType))
            {
                if (!TryParseAnimationType(p.AnimationType, out var animationType))
                    return ToolResult<ModelSetImportSettingsResult>.Fail(
                        $"Unknown AnimationType '{p.AnimationType}'. Valid: None, Legacy, Generic, Human",
                        ErrorCodes.INVALID_PARAM);
                importer.animationType = animationType;
            }

            if (!string.IsNullOrEmpty(p.AvatarSetup))
            {
                if (!TryParseAvatarSetup(p.AvatarSetup, out var avatarSetup))
                    return ToolResult<ModelSetImportSettingsResult>.Fail(
                        $"Unknown AvatarSetup '{p.AvatarSetup}'. Valid: NoAvatar, CreateFromThisModel, CopyFromOther",
                        ErrorCodes.INVALID_PARAM);
                importer.avatarSetup = avatarSetup;
            }

            if (!string.IsNullOrEmpty(p.SourceAvatarPath))
            {
                var sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(p.SourceAvatarPath);
                if (sourceAvatar == null)
                    return ToolResult<ModelSetImportSettingsResult>.Fail(
                        $"No Avatar found at '{p.SourceAvatarPath}'.", ErrorCodes.NOT_FOUND);
                importer.sourceAvatar = sourceAvatar;
            }

            if (p.ImportAnimation.HasValue)
                importer.importAnimation = p.ImportAnimation.Value;

            if (p.GlobalScale.HasValue)
                importer.globalScale = p.GlobalScale.Value;

            if (p.UseFileScale.HasValue)
                importer.useFileScale = p.UseFileScale.Value;

            if (!string.IsNullOrEmpty(p.MaterialImportMode))
            {
                if (!TryParseMaterialImportMode(p.MaterialImportMode, out var materialImportMode))
                    return ToolResult<ModelSetImportSettingsResult>.Fail(
                        $"Unknown MaterialImportMode '{p.MaterialImportMode}'. " +
                        "Valid: None, ImportStandard, ImportViaMaterialDescription",
                        ErrorCodes.INVALID_PARAM);
                importer.materialImportMode = materialImportMode;
            }

            if (p.IsReadable.HasValue)
                importer.isReadable = p.IsReadable.Value;

            if (!string.IsNullOrEmpty(p.MeshCompression))
            {
                if (!TryParseMeshCompression(p.MeshCompression, out var meshCompression))
                    return ToolResult<ModelSetImportSettingsResult>.Fail(
                        $"Unknown MeshCompression '{p.MeshCompression}'. Valid: Off, Low, Medium, High",
                        ErrorCodes.INVALID_PARAM);
                importer.meshCompression = meshCompression;
            }

            if (p.AddCollider.HasValue)
                importer.addCollider = p.AddCollider.Value;

            if (p.GenerateSecondaryUV.HasValue)
                importer.generateSecondaryUV = p.GenerateSecondaryUV.Value;

            if (p.Clips != null)
            {
                var defaultTakeName = importer.defaultClipAnimations != null && importer.defaultClipAnimations.Length > 0
                    ? importer.defaultClipAnimations[0].takeName
                    : null;

                importer.clipAnimations = p.Clips.Select(c => new ModelImporterClipAnimation
                {
                    name = c.Name,
                    takeName = string.IsNullOrEmpty(c.TakeName) ? defaultTakeName : c.TakeName,
                    firstFrame = c.FirstFrame,
                    lastFrame = c.LastFrame,
                    loopTime = c.LoopTime,
                    loopPose = c.LoopPose,
                    keepOriginalOrientation = c.KeepOriginalOrientation,
                    keepOriginalPositionY = c.KeepOriginalPositionY,
                    keepOriginalPositionXZ = c.KeepOriginalPositionXZ,
                    heightFromFeet = c.HeightFromFeet,
                    mirror = c.Mirror,
                }).ToArray();
            }

            importer.SaveAndReimport();

            var texturesExtracted = false;
            if (p.ExtractTextures)
            {
                var folder = string.IsNullOrEmpty(p.ExtractTexturesPath)
                    ? System.IO.Path.GetDirectoryName(p.AssetPath)
                    : p.ExtractTexturesPath;
                texturesExtracted = importer.ExtractTextures(folder);
            }

            var materialsRemapped = false;
            if (p.SearchAndRemapMaterials)
                materialsRemapped = importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);

            if (texturesExtracted || materialsRemapped)
            {
                importer.SaveAndReimport();
            }

            return ToolResult<ModelSetImportSettingsResult>.Ok(new ModelSetImportSettingsResult
            {
                AssetPath           = p.AssetPath,
                AnimationType       = importer.animationType.ToString(),
                AvatarSetup         = importer.avatarSetup.ToString(),
                ImportAnimation     = importer.importAnimation,
                GlobalScale         = importer.globalScale,
                UseFileScale        = importer.useFileScale,
                MaterialImportMode  = importer.materialImportMode.ToString(),
                IsReadable          = importer.isReadable,
                MeshCompression     = importer.meshCompression.ToString(),
                AddCollider         = importer.addCollider,
                GenerateSecondaryUV = importer.generateSecondaryUV,
                ClipCount           = importer.clipAnimations?.Length ?? 0,
                TexturesExtracted   = texturesExtracted,
                MaterialsRemapped   = materialsRemapped,
            });
        }

        private static bool TryParseAnimationType(string value, out ModelImporterAnimationType result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "none":    result = ModelImporterAnimationType.None;    return true;
                case "legacy":  result = ModelImporterAnimationType.Legacy;  return true;
                case "generic": result = ModelImporterAnimationType.Generic; return true;
                case "human":   result = ModelImporterAnimationType.Human;   return true;
                default:        result = ModelImporterAnimationType.None;    return false;
            }
        }

        private static bool TryParseAvatarSetup(string value, out ModelImporterAvatarSetup result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "noavatar":             result = ModelImporterAvatarSetup.NoAvatar;             return true;
                case "createfromthismodel":  result = ModelImporterAvatarSetup.CreateFromThisModel;  return true;
                case "copyfromother":        result = ModelImporterAvatarSetup.CopyFromOther;        return true;
                default:                     result = ModelImporterAvatarSetup.NoAvatar;             return false;
            }
        }

        private static bool TryParseMaterialImportMode(string value, out ModelImporterMaterialImportMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "none":                        result = ModelImporterMaterialImportMode.None;                        return true;
                case "importstandard":              result = ModelImporterMaterialImportMode.ImportStandard;              return true;
                case "importviamaterialdescription": result = ModelImporterMaterialImportMode.ImportViaMaterialDescription; return true;
                default:                            result = ModelImporterMaterialImportMode.None;                        return false;
            }
        }

        private static bool TryParseMeshCompression(string value, out ModelImporterMeshCompression result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "off":    result = ModelImporterMeshCompression.Off;    return true;
                case "low":    result = ModelImporterMeshCompression.Low;    return true;
                case "medium": result = ModelImporterMeshCompression.Medium; return true;
                case "high":   result = ModelImporterMeshCompression.High;   return true;
                default:       result = ModelImporterMeshCompression.Off;    return false;
            }
        }
    }
}
