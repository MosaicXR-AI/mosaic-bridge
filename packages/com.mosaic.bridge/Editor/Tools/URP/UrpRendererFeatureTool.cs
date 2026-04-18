#if MOSAIC_HAS_URP
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.URP
{
    public static class UrpRendererFeatureTool
    {
        [MosaicTool("urp/renderer_feature",
                    "Add, remove, or list URP renderer features on the active URP renderer",
                    isReadOnly: false,
                    category: "urp")]
        public static ToolResult<UrpRendererFeatureResult> Execute(UrpRendererFeatureParams p)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                return ToolResult<UrpRendererFeatureResult>.Fail(
                    "No active URP pipeline asset found. Ensure URP is configured in Graphics Settings.",
                    ErrorCodes.NOT_FOUND);

            // Get the default renderer via SerializedObject
            var pipelineSo = new SerializedObject(pipeline);
            var rendererListProp = pipelineSo.FindProperty("m_RendererDataList");
            if (rendererListProp == null || rendererListProp.arraySize == 0)
                return ToolResult<UrpRendererFeatureResult>.Fail(
                    "No renderer data found on the URP pipeline asset.",
                    ErrorCodes.NOT_FOUND);

            var rendererData = rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            if (rendererData == null)
                return ToolResult<UrpRendererFeatureResult>.Fail(
                    "Default renderer data is null.",
                    ErrorCodes.NOT_FOUND);

            var action = p.Action?.ToLowerInvariant();

            switch (action)
            {
                case "list":
                    return ListFeatures(rendererData);

                case "add":
                    if (string.IsNullOrEmpty(p.FeatureType))
                        return ToolResult<UrpRendererFeatureResult>.Fail(
                            "FeatureType is required for 'add' action.",
                            ErrorCodes.INVALID_PARAM);
                    return AddFeature(rendererData, p.FeatureType, p.Name);

                case "remove":
                    if (string.IsNullOrEmpty(p.FeatureType) && string.IsNullOrEmpty(p.Name))
                        return ToolResult<UrpRendererFeatureResult>.Fail(
                            "FeatureType or Name is required for 'remove' action.",
                            ErrorCodes.INVALID_PARAM);
                    return RemoveFeature(rendererData, p.FeatureType, p.Name);

                default:
                    return ToolResult<UrpRendererFeatureResult>.Fail(
                        $"Invalid action '{p.Action}'. Valid: add, remove, list.",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<UrpRendererFeatureResult> ListFeatures(ScriptableRendererData rendererData)
        {
            var features = rendererData.rendererFeatures;
            var infos = new List<UrpRendererFeatureInfo>();

            foreach (var feature in features)
            {
                if (feature == null) continue;
                infos.Add(new UrpRendererFeatureInfo
                {
                    Name = feature.name,
                    Type = feature.GetType().FullName,
                    IsActive = feature.isActive
                });
            }

            return ToolResult<UrpRendererFeatureResult>.Ok(new UrpRendererFeatureResult
            {
                Action = "list",
                Features = infos.ToArray(),
                RendererName = rendererData.name
            });
        }

        private static ToolResult<UrpRendererFeatureResult> AddFeature(
            ScriptableRendererData rendererData, string featureType, string name)
        {
            var type = FindFeatureType(featureType);
            if (type == null)
                return ToolResult<UrpRendererFeatureResult>.Fail(
                    $"Renderer feature type '{featureType}' not found. Provide the full type name.",
                    ErrorCodes.NOT_FOUND);

            var feature = ScriptableObject.CreateInstance(type) as ScriptableRendererFeature;
            if (feature == null)
                return ToolResult<UrpRendererFeatureResult>.Fail(
                    $"Type '{featureType}' is not a ScriptableRendererFeature.",
                    ErrorCodes.INVALID_PARAM);

            feature.name = !string.IsNullOrEmpty(name) ? name : type.Name;

            Undo.RecordObject(rendererData, "Mosaic: URP Add Renderer Feature");

            // Add via SerializedObject to properly update the renderer data
            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

            // Add as sub-asset
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            return ToolResult<UrpRendererFeatureResult>.Ok(new UrpRendererFeatureResult
            {
                Action = "add",
                FeatureName = feature.name,
                FeatureType = type.FullName,
                RendererName = rendererData.name
            });
        }

        private static ToolResult<UrpRendererFeatureResult> RemoveFeature(
            ScriptableRendererData rendererData, string featureType, string name)
        {
            Undo.RecordObject(rendererData, "Mosaic: URP Remove Renderer Feature");

            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");

            for (int i = featuresProp.arraySize - 1; i >= 0; i--)
            {
                var featureRef = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                if (featureRef == null) continue;

                bool matchType = !string.IsNullOrEmpty(featureType) &&
                    featureRef.GetType().FullName == featureType;
                bool matchName = !string.IsNullOrEmpty(name) &&
                    featureRef.name == name;

                if (matchType || matchName)
                {
                    // Remove the sub-asset
                    Undo.DestroyObjectImmediate(featureRef);
                    featuresProp.DeleteArrayElementAtIndex(i);
                    // DeleteArrayElementAtIndex on object ref first sets to null, second call removes
                    if (i < featuresProp.arraySize &&
                        featuresProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        featuresProp.DeleteArrayElementAtIndex(i);

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rendererData);
                    AssetDatabase.SaveAssets();

                    return ToolResult<UrpRendererFeatureResult>.Ok(new UrpRendererFeatureResult
                    {
                        Action = "remove",
                        FeatureName = name ?? featureType,
                        RendererName = rendererData.name
                    });
                }
            }

            return ToolResult<UrpRendererFeatureResult>.Fail(
                $"No renderer feature matching type='{featureType}' name='{name}' found.",
                ErrorCodes.NOT_FOUND);
        }

        private static Type FindFeatureType(string typeName)
        {
            // Try direct lookup first
            var type = Type.GetType(typeName);
            if (type != null && typeof(ScriptableRendererFeature).IsAssignableFrom(type))
                return type;

            // Search all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null && typeof(ScriptableRendererFeature).IsAssignableFrom(type))
                    return type;
            }

            // Try partial match on simple name
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == typeName && typeof(ScriptableRendererFeature).IsAssignableFrom(t))
                        return t;
                }
            }

            return null;
        }
    }
}
#endif
