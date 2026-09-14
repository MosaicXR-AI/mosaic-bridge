using System;
using System.Reflection;
using UnityEngine.Audio;

namespace Mosaic.Bridge.Tools.Audio
{
    /// <summary>
    /// O4 §4.4 (G4): AudioMixer has no public creation path — Unity's own AudioMixerGroup docs say
    /// "create your groups in the editor." Everything authoring-side lives on
    /// UnityEditor.Audio.AudioMixerController / AudioMixerGroupController, both internal. This
    /// follows the "probe / pin / degrade" reflection contract docs/B5-GENERATION-SPIKE.md already
    /// established for Unity.AI.Generators.Tools: resolve every required member once, report
    /// exactly what's missing if the internal API has moved, and never let a reflection failure
    /// surface as an unhandled exception mid-build.
    ///
    /// The base types (AudioMixer, AudioMixerGroup) ARE public, so every method here accepts and
    /// returns them — the internal AudioMixerController/AudioMixerGroupController types are only
    /// ever named as strings, resolved via reflection, and used purely as Invoke targets/args. A
    /// caller never needs to know they exist.
    ///
    /// Signatures are reported byte-identical across UnityCsReference's 2022.3, 6000.0 and master
    /// branches (O4's own research), which is what makes this the lower-risk reflection job versus
    /// the still-prerelease Unity AI Assistant internal surface B5 documents. What this CANNOT
    /// verify, in an environment with no live Unity Editor session: that these calls actually
    /// produce a correct mixer hierarchy at runtime, only that they compile and that the probe
    /// finds every member it expects. Runtime behavior needs exercising in a real Editor.
    /// </summary>
    internal static class AudioMixerReflection
    {
        private const string ControllerTypeName = "UnityEditor.Audio.AudioMixerController";
        private const string GroupControllerTypeName = "UnityEditor.Audio.AudioMixerGroupController";

        private static Type s_ControllerType;
        private static Type s_GroupControllerType;
        private static MethodInfo s_CreateMixerControllerAtPath;
        private static PropertyInfo s_MasterGroup;
        private static MethodInfo s_CreateNewGroup;
        private static MethodInfo s_AddChildToParent;
        private static MethodInfo s_DeleteGroups;
        private static PropertyInfo s_Children;
        private static bool s_Resolved;
        private static string s_Missing;

        internal readonly struct ProbeResult
        {
            public readonly bool Ok;
            public readonly string Missing;
            public ProbeResult(bool ok, string missing) { Ok = ok; Missing = missing; }
        }

        /// <summary>Never throws. Ok=false means every mutating method below will also fail
        /// cleanly with the same Missing description rather than attempting the reflection call.</summary>
        internal static ProbeResult Probe()
        {
            Resolve();
            return new ProbeResult(s_Missing == null, s_Missing);
        }

        private static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;

            s_ControllerType = FindType(ControllerTypeName);
            if (s_ControllerType == null)
            {
                s_Missing = $"type {ControllerTypeName} (UnityEditor.Audio internal API may have moved)";
                return;
            }

            s_GroupControllerType = FindType(GroupControllerTypeName);
            if (s_GroupControllerType == null) { s_Missing = $"type {GroupControllerTypeName}"; return; }

            s_CreateMixerControllerAtPath = s_ControllerType.GetMethod("CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (s_CreateMixerControllerAtPath == null)
            {
                s_Missing = "static AudioMixerController.CreateMixerControllerAtPath(string)";
                return;
            }

            s_MasterGroup = s_ControllerType.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.Instance);
            if (s_MasterGroup == null) { s_Missing = "instance property AudioMixerController.masterGroup"; return; }

            s_CreateNewGroup = s_ControllerType.GetMethod("CreateNewGroup",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(bool) }, null);
            if (s_CreateNewGroup == null)
            {
                s_Missing = "instance method AudioMixerController.CreateNewGroup(string, bool)";
                return;
            }

            s_AddChildToParent = s_ControllerType.GetMethod("AddChildToParent",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { s_GroupControllerType, s_GroupControllerType }, null);
            if (s_AddChildToParent == null)
            {
                s_Missing = "instance method AudioMixerController.AddChildToParent(AudioMixerGroupController, AudioMixerGroupController)";
                return;
            }

            var groupArrayType = s_GroupControllerType.MakeArrayType();
            s_DeleteGroups = s_ControllerType.GetMethod("DeleteGroups",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { groupArrayType }, null);
            if (s_DeleteGroups == null)
            {
                s_Missing = "instance method AudioMixerController.DeleteGroups(AudioMixerGroupController[])";
                return;
            }

            // children is on the group, not the controller — only needed for mixer-info's tree
            // mode, which degrades to flat-only (no error) if this one member is missing.
            s_Children = s_GroupControllerType.GetProperty("children", BindingFlags.Public | BindingFlags.Instance);

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

        private static string NotReachable(string missing) => "Unity AudioMixer internal API not reachable: " + missing;

        /// <summary>Creates a brand-new mixer asset. Caller is responsible for the idempotency
        /// check (nothing already at path) before calling this.</summary>
        internal static bool TryCreateMixerAtPath(string path, out AudioMixer mixer, out string error)
        {
            mixer = null;
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                mixer = s_CreateMixerControllerAtPath.Invoke(null, new object[] { path }) as AudioMixer;
                if (mixer == null) { error = "CreateMixerControllerAtPath returned null or an unexpected type."; return false; }
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryGetMasterGroup(AudioMixer mixer, out AudioMixerGroup master, out string error)
        {
            master = null;
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                master = s_MasterGroup.GetValue(mixer) as AudioMixerGroup;
                if (master == null) { error = "masterGroup returned null."; return false; }
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryCreateNewGroup(AudioMixer mixer, string name, out AudioMixerGroup group, out string error)
        {
            group = null;
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                group = s_CreateNewGroup.Invoke(mixer, new object[] { name, true }) as AudioMixerGroup;
                if (group == null) { error = "CreateNewGroup returned null or an unexpected type."; return false; }
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        /// <summary>Attaches (or, per O4's own reading of the API, re-attaches) child under
        /// parent. Used for both a freshly created group's placement and for "move" — Unity's own
        /// signature offers no separate detach step, so this is the one call for both.</summary>
        internal static bool TryAddChildToParent(AudioMixer mixer, AudioMixerGroup child, AudioMixerGroup parent, out string error)
        {
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                s_AddChildToParent.Invoke(mixer, new object[] { child, parent });
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        /// <summary>Best-effort only — used by mixer-info's tree mode. False (with a Missing-shaped
        /// error) if the "children" member isn't reachable; callers should degrade to flat mode
        /// rather than fail the whole request, per O4's own "ship flat mode even if the probe
        /// fails" note.</summary>
        internal static bool TryGetChildren(AudioMixerGroup group, out AudioMixerGroup[] children, out string error)
        {
            children = null;
            if (s_Children == null) { Resolve(); }
            if (s_Children == null) { error = NotReachable("instance property AudioMixerGroupController.children"); return false; }
            try
            {
                var raw = s_Children.GetValue(group) as System.Array;
                if (raw == null) { children = System.Array.Empty<AudioMixerGroup>(); error = null; return true; }
                children = new AudioMixerGroup[raw.Length];
                for (int i = 0; i < raw.Length; i++) children[i] = raw.GetValue(i) as AudioMixerGroup;
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryDeleteGroups(AudioMixer mixer, AudioMixerGroup[] groups, out string error)
        {
            var probe = Probe();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                var typedArray = Array.CreateInstance(s_GroupControllerType, groups.Length);
                for (int i = 0; i < groups.Length; i++) typedArray.SetValue(groups[i], i);
                s_DeleteGroups.Invoke(mixer, new object[] { typedArray });
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }
    }
}
