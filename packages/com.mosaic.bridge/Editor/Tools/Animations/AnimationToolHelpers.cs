using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Animations
{
    /// <summary>
    /// Shared helpers for animation tools: loading controllers, finding states, etc.
    /// </summary>
    internal static class AnimationToolHelpers
    {
        /// <summary>Load an AnimatorController asset from an asset path.</summary>
        internal static UnityEditor.Animations.AnimatorController LoadController(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(assetPath);
        }

        /// <summary>Load an AnimationClip asset from an asset path. LoadAssetAtPath&lt;T&gt;
        /// silently returns the FIRST matching sub-asset — for a multi-clip FBX (several takes
        /// embedded in one imported file) that is always the same clip regardless of which take
        /// is actually wanted. Pass clipName to pick a specific one by its own name instead.</summary>
        internal static AnimationClip LoadClip(string assetPath, string clipName = null)
        {
            if (string.IsNullOrEmpty(clipName))
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (obj is AnimationClip clip && clip.name == clipName)
                    return clip;
            return null;
        }

        /// <summary>Every AnimationClip sub-asset name at assetPath — used to build a helpful
        /// error when a requested ClipName isn't among them.</summary>
        internal static string[] ListClipNames(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Select(c => c.name)
                .ToArray();
        }

        /// <summary>
        /// Find a state by name within a specific layer of an AnimatorController.
        /// Returns null if not found.
        /// </summary>
        internal static UnityEditor.Animations.AnimatorState FindState(
            UnityEditor.Animations.AnimatorController controller, string stateName, int layerIndex = 0)
        {
            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return null;

            var stateMachine = controller.layers[layerIndex].stateMachine;
            return FindStateInMachine(stateMachine, stateName);
        }

        /// <summary>Recursively search a state machine (including sub-state-machines) for a named state.</summary>
        internal static UnityEditor.Animations.AnimatorState FindStateInMachine(
            UnityEditor.Animations.AnimatorStateMachine machine, string stateName)
        {
            // Direct children
            var match = machine.states
                .FirstOrDefault(cs => cs.state.name == stateName);
            if (match.state != null)
                return match.state;

            // Sub-state machines
            foreach (var sub in machine.stateMachines)
            {
                var found = FindStateInMachine(sub.stateMachine, stateName);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>Walks "/"-separated sub-state-machine names (each level's own name) starting
        /// from the layer's root, e.g. "Combat/Melee". Empty/null resolves to the root itself.
        /// AnimatorStateMachine has no public parent-machine link and no reverse lookup, so
        /// placing a new state/sub-machine inside a specific nested machine — rather than always
        /// at the layer root, where every generated state used to land — needs an explicit path.</summary>
        internal static UnityEditor.Animations.AnimatorStateMachine ResolveStateMachineByPath(
            UnityEditor.Animations.AnimatorStateMachine root, string path, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path)) return root;

            var current = root;
            foreach (var segment in path.Split('/'))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                var match = current.stateMachines.FirstOrDefault(cs => cs.stateMachine.name == segment);
                if (match.stateMachine == null)
                {
                    error = $"No sub-state-machine named '{segment}' under ParentStateMachinePath '{path}'.";
                    return null;
                }
                current = match.stateMachine;
            }
            return current;
        }

        /// <summary>Recursively finds the immediate AnimatorStateMachine that directly owns
        /// <paramref name="state"/> (its states array contains it) — used to report IsDefault
        /// against the state's own parent machine's defaultState rather than assuming the layer
        /// root, now that states can live inside sub-machines.</summary>
        internal static UnityEditor.Animations.AnimatorStateMachine FindOwningMachine(
            UnityEditor.Animations.AnimatorStateMachine machine, UnityEditor.Animations.AnimatorState state)
        {
            if (machine.states.Any(cs => cs.state == state))
                return machine;
            foreach (var sub in machine.stateMachines)
            {
                var found = FindOwningMachine(sub.stateMachine, state);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Finds a StateMachineBehaviour-derived type by bare or full name across every
        /// loaded assembly — the same reflection-search pattern UIToolHelpers.ResolveComponentType
        /// uses for Component, needed here because AnimatorController.AddEffectiveStateMachineBehaviour
        /// takes a System.Type, not a compile-time generic (the type is a course's own script,
        /// unknown until runtime). An exact FullName match wins over a same-named type elsewhere.</summary>
        internal static System.Type ResolveStateMachineBehaviourType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var direct = System.Type.GetType(typeName);
            if (direct != null && typeof(StateMachineBehaviour).IsAssignableFrom(direct)) return direct;

            var candidates = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return System.Type.EmptyTypes; } })
                .Where(t => typeof(StateMachineBehaviour).IsAssignableFrom(t) && (t.Name == typeName || t.FullName == typeName))
                .ToList();
            if (candidates.Count == 0) return null;
            return candidates.FirstOrDefault(t => t.FullName == typeName) ?? candidates[0];
        }

        /// <summary>Ensure a directory exists for the given asset path.</summary>
        internal static void EnsureDirectoryExists(string assetPath)
        {
            var absoluteDir = System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "..", assetPath));
            if (!string.IsNullOrEmpty(absoluteDir))
                System.IO.Directory.CreateDirectory(absoluteDir);
        }
    }
}
