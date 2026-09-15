using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Models
{
    public static class ModelInfoTool
    {
        [MosaicTool("model/info",
                    "Reports a model's import settings, split clips, source take names, and (when " +
                    "AnimationType=Human) the HumanDescription bone mapping. Read-only — the bone " +
                    "mapping itself cannot be changed through this tool.",
                    isReadOnly: true)]
        public static ToolResult<ModelInfoResult> Info(ModelInfoParams p)
        {
            var importer = AssetImporter.GetAtPath(p.AssetPath) as ModelImporter;
            if (importer == null)
                return ToolResult<ModelInfoResult>.Fail(
                    $"No model found at '{p.AssetPath}'. Ensure the path is a valid model asset (FBX, etc.).",
                    ErrorCodes.NOT_FOUND);

            var clips = (importer.clipAnimations ?? importer.defaultClipAnimations ?? new ModelImporterClipAnimation[0])
                .Select(c => new ModelClipInfo
                {
                    Name       = c.name,
                    TakeName   = c.takeName,
                    FirstFrame = c.firstFrame,
                    LastFrame  = c.lastFrame,
                    LoopTime   = c.loopTime,
                    LoopPose   = c.loopPose,
                })
                .ToArray();

            var humanBoneNames = importer.animationType == ModelImporterAnimationType.Human
                ? (importer.humanDescription.human ?? new HumanBone[0]).Select(b => b.humanName).ToArray()
                : new string[0];

            return ToolResult<ModelInfoResult>.Ok(new ModelInfoResult
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
                DefaultTakeNames    = (importer.defaultClipAnimations ?? new ModelImporterClipAnimation[0])
                                          .Select(c => c.takeName).Distinct().ToArray(),
                Clips               = clips,
                HumanBoneNames      = humanBoneNames,
            });
        }
    }
}
