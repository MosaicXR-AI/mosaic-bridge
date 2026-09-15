using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioProjectSettingsTool
    {
        private const string AssetPath = "ProjectSettings/AudioManager.asset";

        [MosaicTool("audio/project-settings",
                    "Gets or sets project-wide audio settings (ProjectSettings/AudioManager.asset): " +
                    "Volume, RolloffScale, DopplerFactor, DefaultSpeakerMode, SampleRate, " +
                    "RequestedDspBufferSize, Virtual/RealVoiceCount, Spatializer/AmbisonicDecoderPlugin, " +
                    "DisableAudio, EnableOutputSuspension, VirtualizeEffects",
                    isReadOnly: false)]
        public static ToolResult<AudioProjectSettingsResult> Execute(AudioProjectSettingsParams p)
        {
            var target = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            if (target == null)
                return ToolResult<AudioProjectSettingsResult>.Fail(
                    $"Could not load '{AssetPath}'", ErrorCodes.NOT_FOUND);

            var so = new SerializedObject(target);
            bool changed = false;

            if (p.Volume.HasValue)
            {
                so.FindProperty("m_Volume").floatValue = Mathf.Clamp01(p.Volume.Value);
                changed = true;
            }
            if (p.RolloffScale.HasValue)
            {
                so.FindProperty("Rolloff Scale").floatValue = p.RolloffScale.Value;
                changed = true;
            }
            if (p.DopplerFactor.HasValue)
            {
                so.FindProperty("Doppler Factor").floatValue = p.DopplerFactor.Value;
                changed = true;
            }
            if (!string.IsNullOrEmpty(p.DefaultSpeakerMode))
            {
                if (!System.Enum.TryParse<AudioSpeakerMode>(p.DefaultSpeakerMode, ignoreCase: true, out var mode))
                    return ToolResult<AudioProjectSettingsResult>.Fail(
                        $"Unknown DefaultSpeakerMode '{p.DefaultSpeakerMode}'", ErrorCodes.INVALID_PARAM);
                so.FindProperty("Default Speaker Mode").intValue = (int)mode;
                changed = true;
            }
            if (p.SampleRate.HasValue)
            {
                so.FindProperty("m_SampleRate").intValue = p.SampleRate.Value;
                changed = true;
            }
            if (p.RequestedDspBufferSize.HasValue)
            {
                so.FindProperty("m_RequestedDSPBufferSize").intValue = p.RequestedDspBufferSize.Value;
                changed = true;
            }
            if (p.VirtualVoiceCount.HasValue)
            {
                so.FindProperty("m_VirtualVoiceCount").intValue = p.VirtualVoiceCount.Value;
                changed = true;
            }
            if (p.RealVoiceCount.HasValue)
            {
                so.FindProperty("m_RealVoiceCount").intValue = p.RealVoiceCount.Value;
                changed = true;
            }
            if (p.SpatializerPlugin != null)
            {
                if (p.SpatializerPlugin.Length > 0 &&
                    System.Array.IndexOf(AudioSettings.GetSpatializerPluginNames(), p.SpatializerPlugin) < 0)
                    return ToolResult<AudioProjectSettingsResult>.Fail(
                        $"Unknown SpatializerPlugin '{p.SpatializerPlugin}'. Available: " +
                        string.Join(", ", AudioSettings.GetSpatializerPluginNames()),
                        ErrorCodes.INVALID_PARAM);
                so.FindProperty("m_SpatializerPlugin").stringValue = p.SpatializerPlugin;
                changed = true;
            }
            if (p.AmbisonicDecoderPlugin != null)
            {
                so.FindProperty("m_AmbisonicDecoderPlugin").stringValue = p.AmbisonicDecoderPlugin;
                changed = true;
            }
            if (p.DisableAudio.HasValue)
            {
                so.FindProperty("m_DisableAudio").boolValue = p.DisableAudio.Value;
                changed = true;
            }
            if (p.EnableOutputSuspension.HasValue)
            {
                so.FindProperty("m_EnableOutputSuspension").boolValue = p.EnableOutputSuspension.Value;
                changed = true;
            }
            if (p.VirtualizeEffects.HasValue)
            {
                so.FindProperty("m_VirtualizeEffects").boolValue = p.VirtualizeEffects.Value;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }

            return ToolResult<AudioProjectSettingsResult>.Ok(new AudioProjectSettingsResult
            {
                Volume                     = so.FindProperty("m_Volume").floatValue,
                RolloffScale               = so.FindProperty("Rolloff Scale").floatValue,
                DopplerFactor              = so.FindProperty("Doppler Factor").floatValue,
                DefaultSpeakerMode         = ((AudioSpeakerMode)so.FindProperty("Default Speaker Mode").intValue).ToString(),
                SampleRate                 = so.FindProperty("m_SampleRate").intValue,
                RequestedDspBufferSize     = so.FindProperty("m_RequestedDSPBufferSize").intValue,
                VirtualVoiceCount          = so.FindProperty("m_VirtualVoiceCount").intValue,
                RealVoiceCount             = so.FindProperty("m_RealVoiceCount").intValue,
                SpatializerPlugin          = so.FindProperty("m_SpatializerPlugin").stringValue,
                AmbisonicDecoderPlugin     = so.FindProperty("m_AmbisonicDecoderPlugin").stringValue,
                DisableAudio               = so.FindProperty("m_DisableAudio").boolValue,
                EnableOutputSuspension     = so.FindProperty("m_EnableOutputSuspension").boolValue,
                VirtualizeEffects          = so.FindProperty("m_VirtualizeEffects").boolValue,
                AvailableSpatializerPlugins = AudioSettings.GetSpatializerPluginNames(),
                Message                    = changed ? "Audio settings updated" : "No changes (read-only query)"
            });
        }
    }
}
