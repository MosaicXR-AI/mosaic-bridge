using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public static class PrefabCreateVariantTool
    {
        [MosaicTool("prefab/create-variant",
                    "Creates a prefab variant from an existing prefab asset",
                    isReadOnly: false)]
        public static ToolResult<PrefabCreateVariantResult> Execute(PrefabCreateVariantParams p)
        {
            if (string.IsNullOrEmpty(p.SourcePrefabPath))
                return ToolResult<PrefabCreateVariantResult>.Fail(
                    "SourcePrefabPath is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.VariantPath))
                return ToolResult<PrefabCreateVariantResult>.Fail(
                    "VariantPath is required", ErrorCodes.INVALID_PARAM);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(p.SourcePrefabPath);
            if (source == null)
                return ToolResult<PrefabCreateVariantResult>.Fail(
                    $"Source prefab not found at '{p.SourcePrefabPath}'", ErrorCodes.NOT_FOUND);

            if (PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.NotAPrefab)
                return ToolResult<PrefabCreateVariantResult>.Fail(
                    $"Asset at '{p.SourcePrefabPath}' is not a prefab", ErrorCodes.INVALID_PARAM);

            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null)
                return ToolResult<PrefabCreateVariantResult>.Fail(
                    "Failed to instantiate source prefab", ErrorCodes.INTERNAL_ERROR);

            var variantAsset = PrefabUtility.SaveAsPrefabAsset(instance, p.VariantPath);
            Object.DestroyImmediate(instance);

            if (variantAsset == null)
                return ToolResult<PrefabCreateVariantResult>.Fail(
                    $"Failed to save variant at '{p.VariantPath}'", ErrorCodes.INTERNAL_ERROR);

            var guid = AssetDatabase.AssetPathToGUID(p.VariantPath);

            return ToolResult<PrefabCreateVariantResult>.Ok(new PrefabCreateVariantResult
            {
                VariantPath      = p.VariantPath,
                SourcePrefabPath = p.SourcePrefabPath,
                Guid             = guid
            });
        }
    }
}
