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

        // -- expose-param (O4 §4.4): AudioMixerController.exposedParameters is a public property
        // but its element type ExposedAudioParameter is an internal struct {GUID guid; string name;}
        // in the same UnityEditor.Audio namespace — confirmed via UnityCsReference source (the doc's
        // own AddExposedParameter method name does not exist; direct array manipulation is correct).
        private static Type s_ExposedParamType;
        private static PropertyInfo s_ExposedParameters;
        private static FieldInfo s_ExposedParamGuidField;
        private static FieldInfo s_ExposedParamNameField;
        private static MethodInfo s_GetGUIDForVolume;
        private static MethodInfo s_GetGUIDForPitch;
        private static bool s_ExposeParamResolved;
        private static string s_ExposeParamMissing;

        // -- snapshots (O4 §4.4): AudioMixerSnapshotController : AudioMixerSnapshot has a genuinely
        // PUBLIC constructor(AudioMixer owner) and public SetValue(GUID,float)/GetValue(GUID,out
        // float) — confirmed via UnityCsReference source (the doc's cited
        // CloneNewSnapshotFromTarget does not exist anywhere in the module; a fresh snapshot with
        // default values, same as Unity's own "Add Snapshot" button, is the correct model). Because
        // AudioMixerSnapshot itself is public, a created/resolved snapshot can be handled by
        // callers as that public type directly (name, TransitionTo) — only construction and
        // SetValue/GetValue (declared on the internal subclass) need reflection.
        private static Type s_SnapshotControllerType;
        private static ConstructorInfo s_SnapshotCtor;
        private static PropertyInfo s_Snapshots;
        private static PropertyInfo s_TargetSnapshot;
        private static MethodInfo s_SnapshotSetValue;
        private static MethodInfo s_SnapshotGetValue;
        private static bool s_SnapshotResolved;
        private static string s_SnapshotMissing;

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

        internal static ProbeResult ProbeExposeParam()
        {
            ResolveExposeParam();
            return new ProbeResult(s_ExposeParamMissing == null, s_ExposeParamMissing);
        }

        private static void ResolveExposeParam()
        {
            if (s_ExposeParamResolved) return;
            s_ExposeParamResolved = true;

            Resolve();
            if (s_Missing != null) { s_ExposeParamMissing = s_Missing; return; }

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            s_ExposedParameters = s_ControllerType.GetProperty("exposedParameters", BindingFlags.Public | BindingFlags.Instance);
            if (s_ExposedParameters == null) { s_ExposeParamMissing = "AudioMixerController.exposedParameters"; return; }

            s_ExposedParamType = s_ExposedParameters.PropertyType.GetElementType();
            if (s_ExposedParamType == null) { s_ExposeParamMissing = "ExposedAudioParameter element type"; return; }

            s_ExposedParamGuidField = s_ExposedParamType.GetField("guid", all);
            if (s_ExposedParamGuidField == null) { s_ExposeParamMissing = "ExposedAudioParameter.guid"; return; }

            s_ExposedParamNameField = s_ExposedParamType.GetField("name", all);
            if (s_ExposedParamNameField == null) { s_ExposeParamMissing = "ExposedAudioParameter.name"; return; }

            s_GetGUIDForVolume = s_GroupControllerType.GetMethod("GetGUIDForVolume", BindingFlags.Public | BindingFlags.Instance);
            if (s_GetGUIDForVolume == null) { s_ExposeParamMissing = "AudioMixerGroupController.GetGUIDForVolume()"; return; }

            s_GetGUIDForPitch = s_GroupControllerType.GetMethod("GetGUIDForPitch", BindingFlags.Public | BindingFlags.Instance);
            if (s_GetGUIDForPitch == null) { s_ExposeParamMissing = "AudioMixerGroupController.GetGUIDForPitch()"; return; }

            s_ExposeParamMissing = null;
        }

        /// <summary>Appends a new exposed parameter bound to a group's volume or pitch GUID.
        /// exposedName becomes the string AudioMixer.SetFloat/GetFloat use at runtime.</summary>
        internal static bool TryExposeParameter(AudioMixer mixer, AudioMixerGroup group, string paramKind, string exposedName, out string error)
        {
            var probe = ProbeExposeParam();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                MethodInfo getGuid = paramKind == "pitch" ? s_GetGUIDForPitch : s_GetGUIDForVolume;
                object guid = getGuid.Invoke(group, null);

                var current = (Array)s_ExposedParameters.GetValue(mixer);
                var next = Array.CreateInstance(s_ExposedParamType, current.Length + 1);
                Array.Copy(current, next, current.Length);

                var entry = Activator.CreateInstance(s_ExposedParamType);
                s_ExposedParamGuidField.SetValue(entry, guid);
                s_ExposedParamNameField.SetValue(entry, exposedName);
                next.SetValue(entry, current.Length);

                s_ExposedParameters.SetValue(mixer, next);
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        /// <summary>Renames the first exposed parameter matching oldName. Returns false with a
        /// NOT_FOUND-shaped message (not a reflection error) if no entry matches.</summary>
        internal static bool TryRenameExposedParameter(AudioMixer mixer, string oldName, string newName, out string error)
        {
            var probe = ProbeExposeParam();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                var current = (Array)s_ExposedParameters.GetValue(mixer);
                for (int i = 0; i < current.Length; i++)
                {
                    var entry = current.GetValue(i);
                    if ((string)s_ExposedParamNameField.GetValue(entry) == oldName)
                    {
                        s_ExposedParamNameField.SetValue(entry, newName);
                        current.SetValue(entry, i);
                        s_ExposedParameters.SetValue(mixer, current);
                        error = null;
                        return true;
                    }
                }
                error = $"No exposed parameter named '{oldName}'.";
                return false;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryRemoveExposedParameter(AudioMixer mixer, string name, out string error)
        {
            var probe = ProbeExposeParam();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                var current = (Array)s_ExposedParameters.GetValue(mixer);
                int keepCount = 0;
                bool found = false;
                for (int i = 0; i < current.Length; i++)
                    if ((string)s_ExposedParamNameField.GetValue(current.GetValue(i)) == name) found = true;
                    else keepCount++;

                if (!found) { error = $"No exposed parameter named '{name}'."; return false; }

                var next = Array.CreateInstance(s_ExposedParamType, keepCount);
                int j = 0;
                for (int i = 0; i < current.Length; i++)
                {
                    var entry = current.GetValue(i);
                    if ((string)s_ExposedParamNameField.GetValue(entry) != name)
                        next.SetValue(entry, j++);
                }
                s_ExposedParameters.SetValue(mixer, next);
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryListExposedParameters(AudioMixer mixer, out string[] names, out string error)
        {
            names = null;
            var probe = ProbeExposeParam();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                var current = (Array)s_ExposedParameters.GetValue(mixer);
                names = new string[current.Length];
                for (int i = 0; i < current.Length; i++)
                    names[i] = (string)s_ExposedParamNameField.GetValue(current.GetValue(i));
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static ProbeResult ProbeSnapshot()
        {
            ResolveSnapshot();
            return new ProbeResult(s_SnapshotMissing == null, s_SnapshotMissing);
        }

        private static void ResolveSnapshot()
        {
            if (s_SnapshotResolved) return;
            s_SnapshotResolved = true;

            Resolve();
            if (s_Missing != null) { s_SnapshotMissing = s_Missing; return; }

            s_SnapshotControllerType = FindType("UnityEditor.Audio.AudioMixerSnapshotController");
            if (s_SnapshotControllerType == null) { s_SnapshotMissing = "type UnityEditor.Audio.AudioMixerSnapshotController"; return; }

            s_SnapshotCtor = s_SnapshotControllerType.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(AudioMixer) }, null);
            if (s_SnapshotCtor == null) { s_SnapshotMissing = "public AudioMixerSnapshotController(AudioMixer)"; return; }

            s_Snapshots = s_ControllerType.GetProperty("snapshots", BindingFlags.Public | BindingFlags.Instance);
            if (s_Snapshots == null) { s_SnapshotMissing = "AudioMixerController.snapshots"; return; }

            s_TargetSnapshot = s_ControllerType.GetProperty("TargetSnapshot", BindingFlags.Public | BindingFlags.Instance);
            if (s_TargetSnapshot == null) { s_SnapshotMissing = "AudioMixerController.TargetSnapshot"; return; }

            s_SnapshotSetValue = s_SnapshotControllerType.GetMethod("SetValue", BindingFlags.Public | BindingFlags.Instance);
            if (s_SnapshotSetValue == null) { s_SnapshotMissing = "AudioMixerSnapshotController.SetValue(GUID, float)"; return; }

            s_SnapshotGetValue = s_SnapshotControllerType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Instance);
            if (s_SnapshotGetValue == null) { s_SnapshotMissing = "AudioMixerSnapshotController.GetValue(GUID, out float)"; return; }

            s_SnapshotMissing = null;
        }

        internal static bool TryCreateSnapshot(AudioMixer mixer, string name, out AudioMixerSnapshot snapshot, out string error)
        {
            snapshot = null;
            var probe = ProbeSnapshot();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                var created = s_SnapshotCtor.Invoke(new object[] { mixer });
                snapshot = (UnityEngine.Object)created as AudioMixerSnapshot;
                if (snapshot == null) { error = "Created snapshot is not an AudioMixerSnapshot."; return false; }
                snapshot.name = name;

                var current = (Array)s_Snapshots.GetValue(mixer);
                var next = Array.CreateInstance(s_SnapshotControllerType, current.Length + 1);
                Array.Copy(current, next, current.Length);
                next.SetValue(created, current.Length);
                s_Snapshots.SetValue(mixer, next);

                UnityEditor.AssetDatabase.AddObjectToAsset(snapshot, mixer);
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryGetSnapshots(AudioMixer mixer, out AudioMixerSnapshot[] snapshots, out string error)
        {
            snapshots = null;
            var probe = ProbeSnapshot();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                var current = (Array)s_Snapshots.GetValue(mixer);
                snapshots = new AudioMixerSnapshot[current.Length];
                for (int i = 0; i < current.Length; i++)
                    snapshots[i] = (UnityEngine.Object)current.GetValue(i) as AudioMixerSnapshot;
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TrySetTargetSnapshot(AudioMixer mixer, AudioMixerSnapshot snapshot, out string error)
        {
            var probe = ProbeSnapshot();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                s_TargetSnapshot.SetValue(mixer, snapshot);
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        /// <summary>guid comes from GetGUIDForVolume/GetGUIDForPitch (a group's exposed value) —
        /// the same GUID mixer-expose-param already resolves.</summary>
        internal static bool TrySetSnapshotValue(AudioMixerSnapshot snapshot, object guid, float value, out string error)
        {
            var probe = ProbeSnapshot();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                s_SnapshotSetValue.Invoke(snapshot, new[] { guid, (object)value });
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        internal static bool TryGetSnapshotValue(AudioMixerSnapshot snapshot, object guid, out float value, out string error)
        {
            value = 0f;
            var probe = ProbeSnapshot();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                var args = new[] { guid, (object)0f };
                var found = (bool)s_SnapshotGetValue.Invoke(snapshot, args);
                value = (float)args[1];
                if (!found) { error = "GetValue returned false (no override for this parameter in this snapshot)."; return false; }
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
        }

        /// <summary>Public accessor to GetGUIDForVolume/GetGUIDForPitch — shared by mixer-expose-param
        /// and mixer-set-value so both resolve the same GUID for a given group+kind.</summary>
        internal static bool TryGetParamGuid(AudioMixerGroup group, string paramKind, out object guid, out string error)
        {
            guid = null;
            var probe = ProbeExposeParam();
            if (!probe.Ok) { error = NotReachable(probe.Missing); return false; }
            try
            {
                MethodInfo getGuid = paramKind == "pitch" ? s_GetGUIDForPitch : s_GetGUIDForVolume;
                guid = getGuid.Invoke(group, null);
                error = null;
                return true;
            }
            catch (TargetInvocationException e) { error = (e.InnerException ?? e).Message; return false; }
            catch (Exception e) { error = e.Message; return false; }
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
