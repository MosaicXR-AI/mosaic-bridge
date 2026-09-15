using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioSetImportSettingsTool
    {
        [MosaicTool("audio/set-import-settings",
                    "Sets AudioClip import settings (ForceToMono, LoadInBackground, Ambisonic) and per-platform " +
                    "sample settings (LoadType, CompressionFormat, SampleRateSetting/Override, Quality, " +
                    "PreloadAudioData) for the given Platform tab (default Standalone). ClearOverride removes " +
                    "the platform override entirely.",
                    isReadOnly: false)]
        public static ToolResult<AudioSetImportSettingsResult> Execute(AudioSetImportSettingsParams p)
        {
            var importer = AssetImporter.GetAtPath(p.AssetPath) as AudioImporter;
            if (importer == null)
                return ToolResult<AudioSetImportSettingsResult>.Fail(
                    $"No audio clip found at '{p.AssetPath}'. Ensure the path is a valid audio asset.",
                    ErrorCodes.NOT_FOUND);

            string platform = string.IsNullOrEmpty(p.Platform) ? "Standalone" : p.Platform;

            if (p.ForceToMono.HasValue) importer.forceToMono = p.ForceToMono.Value;
            if (p.LoadInBackground.HasValue) importer.loadInBackground = p.LoadInBackground.Value;
            if (p.Ambisonic.HasValue) importer.ambisonic = p.Ambisonic.Value;

            if (p.ClearOverride == true)
                importer.ClearSampleSettingOverride(platform);

            bool wantsOverrideEdit = p.LoadType != null || p.CompressionFormat != null ||
                                      p.SampleRateSetting != null || p.SampleRateOverride.HasValue ||
                                      p.Quality.HasValue || p.PreloadAudioData.HasValue;

            if (wantsOverrideEdit)
            {
                var settings = importer.ContainsSampleSettingsOverride(platform)
                    ? importer.GetOverrideSampleSettings(platform)
                    : importer.defaultSampleSettings;

                if (!string.IsNullOrEmpty(p.LoadType))
                {
                    if (!TryParseLoadType(p.LoadType, out var loadType))
                        return ToolResult<AudioSetImportSettingsResult>.Fail(
                            $"Unknown LoadType '{p.LoadType}'. Valid: DecompressOnLoad, CompressedInMemory, Streaming",
                            ErrorCodes.INVALID_PARAM);
                    settings.loadType = loadType;
                }

                if (!string.IsNullOrEmpty(p.CompressionFormat))
                {
                    if (!System.Enum.TryParse<AudioCompressionFormat>(p.CompressionFormat, ignoreCase: true, out var format))
                        return ToolResult<AudioSetImportSettingsResult>.Fail(
                            $"Unknown CompressionFormat '{p.CompressionFormat}'", ErrorCodes.INVALID_PARAM);
                    settings.compressionFormat = format;
                }

                if (!string.IsNullOrEmpty(p.SampleRateSetting))
                {
                    if (!TryParseSampleRateSetting(p.SampleRateSetting, out var rateSetting))
                        return ToolResult<AudioSetImportSettingsResult>.Fail(
                            $"Unknown SampleRateSetting '{p.SampleRateSetting}'. Valid: PreserveSampleRate, OptimizeSampleRate, OverrideSampleRate",
                            ErrorCodes.INVALID_PARAM);
                    settings.sampleRateSetting = rateSetting;
                }

                if (p.SampleRateOverride.HasValue) settings.sampleRateOverride = p.SampleRateOverride.Value;
                if (p.Quality.HasValue) settings.quality = Mathf.Clamp01(p.Quality.Value);
                if (p.PreloadAudioData.HasValue) settings.preloadAudioData = p.PreloadAudioData.Value;

                importer.SetOverrideSampleSettings(platform, settings);
            }

            importer.SaveAndReimport();

            var finalSettings = importer.ContainsSampleSettingsOverride(platform)
                ? importer.GetOverrideSampleSettings(platform)
                : importer.defaultSampleSettings;

            return ToolResult<AudioSetImportSettingsResult>.Ok(new AudioSetImportSettingsResult
            {
                AssetPath          = p.AssetPath,
                ForceToMono        = importer.forceToMono,
                LoadInBackground   = importer.loadInBackground,
                Ambisonic          = importer.ambisonic,
                Platform           = platform,
                HasOverride        = importer.ContainsSampleSettingsOverride(platform),
                LoadType           = finalSettings.loadType.ToString(),
                CompressionFormat  = finalSettings.compressionFormat.ToString(),
                SampleRateSetting  = finalSettings.sampleRateSetting.ToString(),
                SampleRateOverride = finalSettings.sampleRateOverride,
                Quality            = finalSettings.quality,
                PreloadAudioData   = finalSettings.preloadAudioData,
            });
        }

        private static bool TryParseLoadType(string value, out AudioClipLoadType result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "decompressonload":   result = AudioClipLoadType.DecompressOnLoad;   return true;
                case "compressedinmemory": result = AudioClipLoadType.CompressedInMemory; return true;
                case "streaming":          result = AudioClipLoadType.Streaming;          return true;
                default:                   result = AudioClipLoadType.DecompressOnLoad;   return false;
            }
        }

        private static bool TryParseSampleRateSetting(string value, out AudioSampleRateSetting result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "preservesamplerate": result = AudioSampleRateSetting.PreserveSampleRate; return true;
                case "optimizesamplerate": result = AudioSampleRateSetting.OptimizeSampleRate; return true;
                case "overridesamplerate": result = AudioSampleRateSetting.OverrideSampleRate; return true;
                default:                   result = AudioSampleRateSetting.PreserveSampleRate; return false;
            }
        }
    }
}
