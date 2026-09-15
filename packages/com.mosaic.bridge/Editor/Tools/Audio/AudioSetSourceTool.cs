using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioSetSourceTool
    {
        // O4 §4.4 (G4): audio/create-source only ever set clip/volume/pitch/spatialBlend/loop/
        // playOnAwake and always created a NEW source; this configures an EXISTING one and covers
        // the rest of AudioSource's public surface the course's own objective list names
        // (priority, mute, bypass*, panStereo, reverbZoneMix, spatialize). Only fields the caller
        // actually provided are touched, same as audio/set-spatial — avoids the L16 class of bug
        // (assuming a property name/prefix instead of using the real one).
        [MosaicTool("audio/set-source",
                    "Configures an existing AudioSource's properties (clip, resource, volume, pitch, loop, " +
                    "playOnAwake, priority, mute, bypassEffects, bypassListenerEffects, bypassReverbZones, " +
                    "panStereo, reverbZoneMix, spatialize, spatializePostEffects). ResourcePath (e.g. an " +
                    "AudioRandomContainer from audio/create-random-container) sets AudioSource.resource. " +
                    "Only provided fields are changed. Use audio/create-source to add a new AudioSource, " +
                    "audio/set-spatial for 3D distance/rolloff/doppler/spread.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AudioSetSourceResult> Execute(AudioSetSourceParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioSetSourceResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var source = AudioToolHelpers.ResolveAudioSource(p.InstanceId, p.Name, out var go);
            if (go == null)
                return ToolResult<AudioSetSourceResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')", ErrorCodes.NOT_FOUND);
            if (source == null)
                return ToolResult<AudioSetSourceResult>.Fail(
                    $"No AudioSource found on GameObject '{go.name}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(source, "Mosaic: Set Audio Source");

            if (!string.IsNullOrEmpty(p.ClipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p.ClipPath);
                if (clip == null)
                    return ToolResult<AudioSetSourceResult>.Fail(
                        $"AudioClip not found at path: '{p.ClipPath}'", ErrorCodes.NOT_FOUND);
                source.clip = clip;
            }

            if (!string.IsNullOrEmpty(p.ResourcePath))
            {
                var resource = AssetDatabase.LoadAssetAtPath<AudioResource>(p.ResourcePath);
                if (resource == null)
                    return ToolResult<AudioSetSourceResult>.Fail(
                        $"AudioResource not found at path: '{p.ResourcePath}'", ErrorCodes.NOT_FOUND);
                source.resource = resource;
            }

            if (p.Volume.HasValue) source.volume = Mathf.Clamp01(p.Volume.Value);
            if (p.Pitch.HasValue) source.pitch = p.Pitch.Value;
            if (p.Loop.HasValue) source.loop = p.Loop.Value;
            if (p.PlayOnAwake.HasValue) source.playOnAwake = p.PlayOnAwake.Value;
            if (p.Priority.HasValue) source.priority = Mathf.Clamp(p.Priority.Value, 0, 256);
            if (p.Mute.HasValue) source.mute = p.Mute.Value;
            if (p.BypassEffects.HasValue) source.bypassEffects = p.BypassEffects.Value;
            if (p.BypassListenerEffects.HasValue) source.bypassListenerEffects = p.BypassListenerEffects.Value;
            if (p.BypassReverbZones.HasValue) source.bypassReverbZones = p.BypassReverbZones.Value;
            if (p.PanStereo.HasValue) source.panStereo = Mathf.Clamp(p.PanStereo.Value, -1f, 1f);
            if (p.ReverbZoneMix.HasValue) source.reverbZoneMix = Mathf.Clamp(p.ReverbZoneMix.Value, 0f, 1.1f);
            if (p.Spatialize.HasValue) source.spatialize = p.Spatialize.Value;
            if (p.SpatializePostEffects.HasValue) source.spatializePostEffects = p.SpatializePostEffects.Value;

            return ToolResult<AudioSetSourceResult>.Ok(new AudioSetSourceResult
            {
                InstanceId = UnityIds.Of(go),
                GameObjectName = go.name,
                ClipName = source.clip != null ? source.clip.name : null,
                ResourceName = source.resource != null ? source.resource.name : null,
                Volume = source.volume,
                Pitch = source.pitch,
                Loop = source.loop,
                PlayOnAwake = source.playOnAwake,
                Priority = source.priority,
                Mute = source.mute,
                BypassEffects = source.bypassEffects,
                BypassListenerEffects = source.bypassListenerEffects,
                BypassReverbZones = source.bypassReverbZones,
                PanStereo = source.panStereo,
                ReverbZoneMix = source.reverbZoneMix,
                Spatialize = source.spatialize,
                SpatializePostEffects = source.spatializePostEffects,
            });
        }
    }
}
