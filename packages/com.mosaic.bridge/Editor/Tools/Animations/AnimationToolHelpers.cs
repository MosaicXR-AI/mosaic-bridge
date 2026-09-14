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
