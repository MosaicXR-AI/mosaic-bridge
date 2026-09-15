using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;

namespace Mosaic.Bridge.Tools.Audio
{
    /// <summary>
    /// O4 §4.4 (G4): AudioRandomContainer/AudioContainerElement (UnityEngine.AudioModule, Unity 6's
    /// "new audio" random-playlist system) have internal constructors AND every authoring property
    /// (elements, playbackMode, triggerMode, volumeRandomizationRange, ...) is internal too — the
    /// O4 doc's "public API, internal ctor" description undersold the actual surface, confirmed via
    /// a live reflection probe against 6000.3.10f1 rather than public docs (the Scripting API page
    /// 404s; the Manual only documents the Editor UI). This follows the same probe/pin/degrade
    /// contract as AudioMixerReflection: resolve every member once, fail cleanly with a description
    /// of what's missing if Unity moves this internal surface, never let reflection throw past this
    /// class. AudioRandomContainer itself IS public and derives from the public AudioResource, so
    /// callers only ever handle AudioResource/UnityEngine.Object — AudioContainerElement is only
    /// ever named as a string type, resolved and populated purely through this class.
    /// </summary>
    internal static class AudioRandomContainerReflection
    {
        private static Type s_ContainerType;
        private static Type s_ElementType;
        private static PropertyInfo s_Elements;
        private static PropertyInfo s_PlaybackMode;
        private static PropertyInfo s_TriggerMode;
        private static PropertyInfo s_VolumeRandomizationRange;
        private static PropertyInfo s_VolumeRandomizationEnabled;
        private static PropertyInfo s_ElementAudioClip;
        private static PropertyInfo s_ElementVolume;
        private static PropertyInfo s_ElementEnabled;
        private static bool s_Resolved;
        private static string s_Missing;

        internal readonly struct ProbeResult
        {
            public readonly bool Ok;
            public readonly string Missing;
            public ProbeResult(bool ok, string missing) { Ok = ok; Missing = missing; }
        }

        internal static ProbeResult Probe()
        {
            Resolve();
            return new ProbeResult(s_Missing == null, s_Missing);
        }

        internal static Type PlaybackModeEnumType => s_PlaybackMode?.PropertyType;
        internal static Type TriggerModeEnumType => s_TriggerMode?.PropertyType;

        private static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            s_ContainerType = FindType("UnityEngine.Audio.AudioRandomContainer");
            if (s_ContainerType == null) { s_Missing = "type UnityEngine.Audio.AudioRandomContainer"; return; }

            s_ElementType = FindType("UnityEngine.Audio.AudioContainerElement");
            if (s_ElementType == null) { s_Missing = "type UnityEngine.Audio.AudioContainerElement"; return; }

            s_Elements = s_ContainerType.GetProperty("elements", all);
            if (s_Elements == null) { s_Missing = "AudioRandomContainer.elements"; return; }

            s_PlaybackMode = s_ContainerType.GetProperty("playbackMode", all);
            if (s_PlaybackMode == null) { s_Missing = "AudioRandomContainer.playbackMode"; return; }

            s_TriggerMode = s_ContainerType.GetProperty("triggerMode", all);
            if (s_TriggerMode == null) { s_Missing = "AudioRandomContainer.triggerMode"; return; }

            s_VolumeRandomizationRange = s_ContainerType.GetProperty("volumeRandomizationRange", all);
            if (s_VolumeRandomizationRange == null) { s_Missing = "AudioRandomContainer.volumeRandomizationRange"; return; }

            s_VolumeRandomizationEnabled = s_ContainerType.GetProperty("volumeRandomizationEnabled", all);
            if (s_VolumeRandomizationEnabled == null) { s_Missing = "AudioRandomContainer.volumeRandomizationEnabled"; return; }

            s_ElementAudioClip = s_ElementType.GetProperty("audioClip", all);
            if (s_ElementAudioClip == null) { s_Missing = "AudioContainerElement.audioClip"; return; }

            s_ElementVolume = s_ElementType.GetProperty("volume", all);
            if (s_ElementVolume == null) { s_Missing = "AudioContainerElement.volume"; return; }

            s_ElementEnabled = s_ElementType.GetProperty("enabled", all);
            if (s_ElementEnabled == null) { s_Missing = "AudioContainerElement.enabled"; return; }

            s_Missing = null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(fullName); }
                catch { continue; }
                if (t != null) return t;
            }
            return null;
        }

        private static string NotReachable(string missing) =>
            "Unity AudioRandomContainer internal API not reachable: " + missing;

        /// <summary>Creates the container and one sub-asset element per clip, saved together at
        /// path. Caller is responsible for the idempotency check (nothing already at path).</summary>
        internal static bool TryCreate(
            string path, string[] clipPaths, float[] elementVolumes, string playbackMode, string triggerMode,
            float[] volumeRandomizationRange, out AudioResource resource, out string error)
        {
            resource = null;
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }

            try
            {
                var container = Activator.CreateInstance(s_ContainerType, nonPublic: true);

                if (!string.IsNullOrEmpty(playbackMode))
                {
                    if (!TryParseEnum(s_PlaybackMode.PropertyType, playbackMode, out var mode, out error))
                        return false;
                    s_PlaybackMode.SetValue(container, mode);
                }
                if (!string.IsNullOrEmpty(triggerMode))
                {
                    if (!TryParseEnum(s_TriggerMode.PropertyType, triggerMode, out var mode, out error))
                        return false;
                    s_TriggerMode.SetValue(container, mode);
                }
                if (volumeRandomizationRange != null)
                {
                    if (volumeRandomizationRange.Length != 2)
                    {
                        error = "VolumeRandomizationRange requires exactly [min, max]";
                        return false;
                    }
                    s_VolumeRandomizationRange.SetValue(container,
                        new Vector2(volumeRandomizationRange[0], volumeRandomizationRange[1]));
                    s_VolumeRandomizationEnabled.SetValue(container, true);
                }

                var elementsArray = Array.CreateInstance(s_ElementType, clipPaths.Length);
                for (int i = 0; i < clipPaths.Length; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(clipPaths[i]);
                    if (clip == null)
                    {
                        UnityEngine.Object.DestroyImmediate((UnityEngine.Object)container);
                        error = $"AudioClip not found at '{clipPaths[i]}'";
                        return false;
                    }

                    var element = Activator.CreateInstance(s_ElementType, nonPublic: true);
                    s_ElementAudioClip.SetValue(element, clip);
                    s_ElementVolume.SetValue(element, elementVolumes != null && i < elementVolumes.Length ? elementVolumes[i] : 0f);
                    s_ElementEnabled.SetValue(element, true);
                    elementsArray.SetValue(element, i);
                }
                s_Elements.SetValue(container, elementsArray);

                UnityEditor.AssetDatabase.CreateAsset((UnityEngine.Object)container, path);
                for (int i = 0; i < elementsArray.Length; i++)
                    UnityEditor.AssetDatabase.AddObjectToAsset((UnityEngine.Object)elementsArray.GetValue(i), (UnityEngine.Object)container);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.ImportAsset(path);

                resource = (UnityEngine.Object)container as AudioResource;
                if (resource == null) { error = "Created container is not an AudioResource."; return false; }
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        private static bool TryParseEnum(Type enumType, string value, out object result, out string error)
        {
            result = null;
            try
            {
                result = Enum.Parse(enumType, value, ignoreCase: true);
                error = null;
                return true;
            }
            catch (Exception)
            {
                error = $"Unknown value '{value}' for {enumType.Name}. Valid: {string.Join(", ", Enum.GetNames(enumType))}";
                return false;
            }
        }
    }
}
