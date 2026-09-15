using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingSettingsTool
    {
        [MosaicTool("lighting/settings",
                    "create: makes a new LightingSettings asset at AssetPath (AssignToActiveScene " +
                    "also assigns it to the active scene). get: reads AssetPath, or the active " +
                    "scene's currently assigned settings when AssetPath is omitted (fails clearly if " +
                    "the scene has none). set: applies given fields to AssetPath, or the active " +
                    "scene's assigned settings when omitted. Fields: Lightmapper, LightmapResolution, " +
                    "LightmapMaxSize, Ao, MixedBakeMode, BakedGI, RealtimeGI, IndirectResolution, " +
                    "AoMaxDistance, Direct/IndirectSampleCount, Min/MaxBounces, FilteringMode.",
                    isReadOnly: false)]
        public static ToolResult<LightingSettingsResult> Execute(LightingSettingsParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create": return Create(p);
                case "get":    return GetOrSet(p, applyChanges: false);
                case "set":    return GetOrSet(p, applyChanges: true);
                default:
                    return ToolResult<LightingSettingsResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: create, get, set", ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<LightingSettingsResult> Create(LightingSettingsParams p)
        {
            if (string.IsNullOrEmpty(p.AssetPath))
                return ToolResult<LightingSettingsResult>.Fail("AssetPath is required for create", ErrorCodes.INVALID_PARAM);

            var settings = new LightingSettings();
            if (!ApplyFields(p, settings, out var error))
                return ToolResult<LightingSettingsResult>.Fail(error, ErrorCodes.INVALID_PARAM);

            var dir = System.IO.Path.GetDirectoryName(p.AssetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(settings, p.AssetPath);
            AssetDatabase.SaveAssets();

            if (p.AssignToActiveScene)
                Lightmapping.SetLightingSettingsForScene(SceneManager.GetActiveScene(), settings);

            return ToolResult<LightingSettingsResult>.Ok(ToResult("create", p.AssetPath, settings));
        }

        private static ToolResult<LightingSettingsResult> GetOrSet(LightingSettingsParams p, bool applyChanges)
        {
            LightingSettings settings;
            if (!string.IsNullOrEmpty(p.AssetPath))
            {
                settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(p.AssetPath);
                if (settings == null)
                    return ToolResult<LightingSettingsResult>.Fail(
                        $"No LightingSettings asset found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);
            }
            else
            {
                settings = Lightmapping.lightingSettings;
                if (settings == null)
                    return ToolResult<LightingSettingsResult>.Fail(
                        "The active scene has no LightingSettings assigned. Pass AssetPath, or use " +
                        "Action=create with AssignToActiveScene=true first.", ErrorCodes.NOT_FOUND);
            }

            if (applyChanges)
            {
                if (!ApplyFields(p, settings, out var error))
                    return ToolResult<LightingSettingsResult>.Fail(error, ErrorCodes.INVALID_PARAM);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            return ToolResult<LightingSettingsResult>.Ok(ToResult(applyChanges ? "set" : "get", p.AssetPath, settings));
        }

        private static bool ApplyFields(LightingSettingsParams p, LightingSettings settings, out string error)
        {
            error = null;

            if (!string.IsNullOrEmpty(p.Lightmapper))
            {
                if (!TryParseLightmapper(p.Lightmapper, out var lightmapper))
                {
                    error = $"Unknown Lightmapper '{p.Lightmapper}'. Valid: ProgressiveCPU, ProgressiveGPU";
                    return false;
                }
                settings.lightmapper = lightmapper;
            }

            if (p.LightmapResolution.HasValue) settings.lightmapResolution = p.LightmapResolution.Value;
            if (p.LightmapMaxSize.HasValue) settings.lightmapMaxSize = p.LightmapMaxSize.Value;
            if (p.Ao.HasValue) settings.ao = p.Ao.Value;

            if (!string.IsNullOrEmpty(p.MixedBakeMode))
            {
                if (!TryParseMixedBakeMode(p.MixedBakeMode, out var mode))
                {
                    error = $"Unknown MixedBakeMode '{p.MixedBakeMode}'. Valid: IndirectOnly, Shadowmask, Subtractive";
                    return false;
                }
                settings.mixedBakeMode = mode;
            }

            if (p.BakedGI.HasValue) settings.bakedGI = p.BakedGI.Value;
            if (p.RealtimeGI.HasValue) settings.realtimeGI = p.RealtimeGI.Value;
            if (p.IndirectResolution.HasValue) settings.indirectResolution = p.IndirectResolution.Value;
            if (p.AoMaxDistance.HasValue) settings.aoMaxDistance = p.AoMaxDistance.Value;
            if (p.DirectSampleCount.HasValue) settings.directSampleCount = p.DirectSampleCount.Value;
            if (p.IndirectSampleCount.HasValue) settings.indirectSampleCount = p.IndirectSampleCount.Value;
            if (p.MinBounces.HasValue) settings.minBounces = p.MinBounces.Value;
            if (p.MaxBounces.HasValue) settings.maxBounces = p.MaxBounces.Value;

            if (!string.IsNullOrEmpty(p.FilteringMode))
            {
                if (!TryParseFilterMode(p.FilteringMode, out var filterMode))
                {
                    error = $"Unknown FilteringMode '{p.FilteringMode}'. Valid: None, Auto, Advanced";
                    return false;
                }
                settings.filteringMode = filterMode;
            }

            return true;
        }

        private static LightingSettingsResult ToResult(string action, string assetPath, LightingSettings settings) =>
            new LightingSettingsResult
            {
                Action = action,
                AssetPath = assetPath,
                Lightmapper = settings.lightmapper.ToString(),
                LightmapResolution = settings.lightmapResolution,
                LightmapMaxSize = settings.lightmapMaxSize,
                Ao = settings.ao,
                MixedBakeMode = settings.mixedBakeMode.ToString(),
                BakedGI = settings.bakedGI,
                RealtimeGI = settings.realtimeGI,
                IndirectResolution = settings.indirectResolution,
                AoMaxDistance = settings.aoMaxDistance,
                DirectSampleCount = settings.directSampleCount,
                IndirectSampleCount = settings.indirectSampleCount,
                MinBounces = settings.minBounces,
                MaxBounces = settings.maxBounces,
                FilteringMode = settings.filteringMode.ToString(),
            };

        private static bool TryParseLightmapper(string value, out LightingSettings.Lightmapper result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "progressivecpu": result = LightingSettings.Lightmapper.ProgressiveCPU; return true;
                case "progressivegpu": result = LightingSettings.Lightmapper.ProgressiveGPU; return true;
                default:               result = LightingSettings.Lightmapper.ProgressiveCPU; return false;
            }
        }

        private static bool TryParseMixedBakeMode(string value, out MixedLightingMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "indirectonly": result = MixedLightingMode.IndirectOnly; return true;
                case "shadowmask":   result = MixedLightingMode.Shadowmask;   return true;
                case "subtractive":  result = MixedLightingMode.Subtractive;  return true;
                default:             result = MixedLightingMode.IndirectOnly; return false;
            }
        }

        private static bool TryParseFilterMode(string value, out LightingSettings.FilterMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "none":     result = LightingSettings.FilterMode.None;     return true;
                case "auto":     result = LightingSettings.FilterMode.Auto;     return true;
                case "advanced": result = LightingSettings.FilterMode.Advanced; return true;
                default:         result = LightingSettings.FilterMode.Auto;     return false;
            }
        }
    }
}
