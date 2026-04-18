using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Materials
{
    public static class MaterialAssignTool
    {
        [MosaicTool("material/assign",
                    "Assigns a material asset to a material slot on a GameObject's Renderer",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MaterialAssignResult> Execute(MaterialAssignParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<MaterialAssignResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.MaterialPath))
                return ToolResult<MaterialAssignResult>.Fail(
                    "MaterialPath is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<MaterialAssignResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null)
                return ToolResult<MaterialAssignResult>.Fail(
                    $"No Renderer found on GameObject '{p.GameObjectName}'", ErrorCodes.NOT_FOUND);

            var material = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
            if (material == null)
                return ToolResult<MaterialAssignResult>.Fail(
                    $"Material not found at '{p.MaterialPath}'", ErrorCodes.NOT_FOUND);

            var mats = renderer.sharedMaterials;
            if (p.MaterialIndex < 0 || p.MaterialIndex >= mats.Length)
                return ToolResult<MaterialAssignResult>.Fail(
                    $"MaterialIndex {p.MaterialIndex} is out of range. Renderer has {mats.Length} material slot(s).",
                    ErrorCodes.OUT_OF_RANGE);

            Undo.RecordObject(renderer, "Assign Material");
            mats[p.MaterialIndex] = material;
            renderer.sharedMaterials = mats;
            EditorUtility.SetDirty(renderer);

            return ToolResult<MaterialAssignResult>.Ok(new MaterialAssignResult
            {
                GameObjectName = go.name,
                MaterialPath   = p.MaterialPath,
                MaterialIndex  = p.MaterialIndex,
                RendererType   = renderer.GetType().Name
            });
        }
    }
}
