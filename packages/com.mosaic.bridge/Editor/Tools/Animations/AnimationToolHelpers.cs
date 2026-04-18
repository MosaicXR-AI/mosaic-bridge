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

        /// <summary>Load an AnimationClip asset from an asset path.</summary>
        internal static AnimationClip LoadClip(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
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
