using System;
using System.Reflection;
using UnityEngine;

namespace Mosaic.Bridge.Tools.Audio
{
    /// <summary>
    /// O4 §4.4 (G4): UnityEditor.AudioUtil is internal, but PlayPreviewClip/StopAllPreviewClips/
    /// IsPreviewClipPlaying have been stable "extern public static" bindings for years (confirmed
    /// against UnityCsReference master) — the doc's own lowest-risk internal-API item. Same
    /// probe/pin/degrade contract as AudioMixerReflection: resolve once, fail cleanly if the
    /// binding moves.
    /// </summary>
    internal static class AudioPreviewReflection
    {
        private static MethodInfo s_PlayPreviewClip;
        private static MethodInfo s_StopAllPreviewClips;
        private static MethodInfo s_IsPreviewClipPlaying;
        private static MethodInfo s_GetPreviewClipPosition;
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

        private static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;

            var type = FindType("UnityEditor.AudioUtil");
            if (type == null) { s_Missing = "type UnityEditor.AudioUtil"; return; }

            s_PlayPreviewClip = type.GetMethod("PlayPreviewClip",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (s_PlayPreviewClip == null) { s_Missing = "static AudioUtil.PlayPreviewClip(AudioClip, int, bool)"; return; }

            s_StopAllPreviewClips = type.GetMethod("StopAllPreviewClips", BindingFlags.Public | BindingFlags.Static);
            if (s_StopAllPreviewClips == null) { s_Missing = "static AudioUtil.StopAllPreviewClips()"; return; }

            s_IsPreviewClipPlaying = type.GetMethod("IsPreviewClipPlaying", BindingFlags.Public | BindingFlags.Static);
            if (s_IsPreviewClipPlaying == null) { s_Missing = "static AudioUtil.IsPreviewClipPlaying()"; return; }

            // Best-effort — status can still report play/stop without this.
            s_GetPreviewClipPosition = type.GetMethod("GetPreviewClipPosition", BindingFlags.Public | BindingFlags.Static);

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

        private static string NotReachable(string missing) => "Unity AudioUtil internal API not reachable: " + missing;

        internal static bool TryPlay(AudioClip clip, int startSample, bool loop, out string error)
        {
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                s_PlayPreviewClip.Invoke(null, new object[] { clip, startSample, loop });
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryStop(out string error)
        {
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                s_StopAllPreviewClips.Invoke(null, null);
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryGetStatus(out bool isPlaying, out float positionSeconds, out string error)
        {
            isPlaying = false;
            positionSeconds = 0f;
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                isPlaying = (bool)s_IsPreviewClipPlaying.Invoke(null, null);
                if (s_GetPreviewClipPosition != null)
                    positionSeconds = (float)s_GetPreviewClipPosition.Invoke(null, null);
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }
    }
}
