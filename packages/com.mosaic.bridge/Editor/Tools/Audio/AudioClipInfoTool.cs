using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioClipInfoTool
    {
        [MosaicTool("audio/clip-info",
                    "Reports AudioClip runtime data (length, frequency, channels, samples, load type) and " +
                    "AudioImporter settings for the given Platform tab (default Standalone)",
                    isReadOnly: true)]
        public static ToolResult<AudioClipInfoResult> Execute(AudioClipInfoParams p)
        {
            var importer = AssetImporter.GetAtPath(p.AssetPath) as AudioImporter;
            if (importer == null)
                return ToolResult<AudioClipInfoResult>.Fail(
                    $"No audio clip found at '{p.AssetPath}'. Ensure the path is a valid audio asset.",
                    ErrorCodes.NOT_FOUND);

            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(p.AssetPath);
            if (clip == null)
                return ToolResult<AudioClipInfoResult>.Fail(
                    $"AudioClip could not be loaded at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            string platform = string.IsNullOrEmpty(p.Platform) ? "Standalone" : p.Platform;
            bool hasOverride = importer.ContainsSampleSettingsOverride(platform);
            var settings = hasOverride ? importer.GetOverrideSampleSettings(platform) : importer.defaultSampleSettings;

            return ToolResult<AudioClipInfoResult>.Ok(new AudioClipInfoResult
            {
                AssetPath          = p.AssetPath,
                Length             = clip.length,
                Frequency          = clip.frequency,
                Channels           = clip.channels,
                Samples            = clip.samples,
                LoadType           = clip.loadType.ToString(),
                Ambisonic          = clip.ambisonic,
                PreloadAudioData   = clip.preloadAudioData,
                ForceToMono        = importer.forceToMono,
                LoadInBackground   = importer.loadInBackground,
                Platform           = platform,
                HasOverride        = hasOverride,
                CompressionFormat  = settings.compressionFormat.ToString(),
                SampleRateSetting  = settings.sampleRateSetting.ToString(),
                SampleRateOverride = settings.sampleRateOverride,
                Quality            = settings.quality,
            });
        }
    }
}
