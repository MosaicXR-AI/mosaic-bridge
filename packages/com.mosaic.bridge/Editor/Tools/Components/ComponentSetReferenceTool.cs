using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentSetReferenceTool
    {
        [MosaicTool("component/set_reference",
                    "Sets an object-reference serialized property on a component to an asset or scene GameObject",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComponentSetReferenceResult> Execute(ComponentSetReferenceParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.ComponentType))
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "ComponentType is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.PropertyPath))
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "PropertyPath is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.TargetObjectPath))
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "TargetObjectPath is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var type = ResolveType(p.ComponentType);
            if (type == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"Component type not found: {p.ComponentType}", ErrorCodes.NOT_FOUND);

            var component = go.GetComponent(type);
            if (component == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"Component '{p.ComponentType}' not found on '{p.GameObjectName}'", ErrorCodes.NOT_FOUND);

            var so = new SerializedObject(component);
            var prop = so.FindProperty(p.PropertyPath);
            if (prop == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"Property '{p.PropertyPath}' not found on component '{p.ComponentType}'", ErrorCodes.NOT_FOUND);

            // Resolve target object
            UnityEngine.Object resolvedTarget = null;
            bool tryAsset      = string.IsNullOrEmpty(p.TargetType) || p.TargetType == "Asset";
            bool tryGameObject = string.IsNullOrEmpty(p.TargetType) || p.TargetType == "GameObject";

            if (tryAsset)
            {
                resolvedTarget = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.TargetObjectPath);

                // Asset may have been created recently — refresh and retry
                if (resolvedTarget == null && p.TargetObjectPath.StartsWith("Assets/"))
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    resolvedTarget = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.TargetObjectPath);
                }

                // Try loading as sub-asset (e.g. ComputeShader from .compute file)
                if (resolvedTarget == null && p.TargetObjectPath.StartsWith("Assets/"))
                {
                    var allAtPath = AssetDatabase.LoadAllAssetsAtPath(p.TargetObjectPath);
                    if (allAtPath != null && allAtPath.Length > 0)
                        resolvedTarget = allAtPath[0];
                }
            }

            if (resolvedTarget == null && tryGameObject)
                resolvedTarget = GameObject.Find(p.TargetObjectPath);

            if (resolvedTarget == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"Target object not found: '{p.TargetObjectPath}'", ErrorCodes.NOT_FOUND);

            prop.objectReferenceValue = resolvedTarget;
            so.ApplyModifiedProperties();

            return ToolResult<ComponentSetReferenceResult>.Ok(new ComponentSetReferenceResult
            {
                GameObjectName = go.name,
                ComponentType  = type.FullName,
                PropertyPath   = p.PropertyPath,
                ResolvedTarget = resolvedTarget.name
            });
        }

        private static Type ResolveType(string typeName)
        {
            var t = Type.GetType(typeName);
            if (t != null) return t;
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
        }
    }
}
