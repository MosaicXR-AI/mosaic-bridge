using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Core.Scenes
{
    /// <summary>
    /// O4 §3.3: GameObject.Find (used by probuilder/*, component/*, gameobject/duplicate,
    /// prefab/create, TagLayerHelpers per L18) only searches ACTIVE objects in the active scene,
    /// silently misses inactive ones, and — worse — picks the FIRST match on a duplicate name with
    /// no error, no warning, nothing to tell the caller it guessed. A course's own "Parts" build
    /// plans routinely reuse names like "Wall" across multiple objects, which is exactly the shape
    /// that makes GameObject.Find dangerous rather than merely incomplete.
    ///
    /// This resolves by InstanceId (unambiguous, always preferred when given), then by a
    /// "/"-separated hierarchy path (e.g. "Canvas/Panel/Button", scoped from any loaded scene's
    /// root), then by a bare name — searched across every loaded scene, INCLUDING inactive
    /// objects, and failing loudly with the ambiguous names listed rather than guessing when more
    /// than one match exists.
    /// </summary>
    public static class GameObjectResolver
    {
        /// <summary>InstanceId wins when given and resolves. Otherwise NameOrPath: a "/" makes it
        /// a hierarchy path (root name, then each child by name); no "/" makes it a bare name,
        /// which must resolve to exactly one GameObject across every loaded scene — 0 or 2+
        /// matches both fail with a specific reason rather than guessing.</summary>
        public static bool TryResolve(int? instanceId, string nameOrPath, out GameObject result, out string error)
        {
            result = null;
            error = null;

            if (instanceId.HasValue && instanceId.Value != 0)
            {
#pragma warning disable CS0618
                result = UnityIds.Resolve(instanceId.Value) as GameObject;
#pragma warning restore CS0618
                if (result != null) return true;
                if (string.IsNullOrEmpty(nameOrPath))
                {
                    error = $"No GameObject found with InstanceId {instanceId.Value}";
                    return false;
                }
                // InstanceId given but stale/wrong — fall through to NameOrPath rather than fail
                // outright, matching every existing ResolveGameObject helper's own precedence.
            }

            if (string.IsNullOrEmpty(nameOrPath))
            {
                error = "Either an InstanceId or a name/path is required";
                return false;
            }

            if (nameOrPath.Contains("/"))
                return TryResolvePath(nameOrPath, out result, out error);

            var matches = FindAllByName(nameOrPath);
            if (matches.Count == 0)
            {
                error = $"No GameObject named '{nameOrPath}' found in any loaded scene (including inactive objects).";
                return false;
            }
            if (matches.Count > 1)
            {
                error = $"'{nameOrPath}' matches {matches.Count} GameObjects: " +
                        $"{string.Join(", ", matches.Select(GetHierarchyPath))}. " +
                        "Use a hierarchy path (e.g. 'Parent/Child') or InstanceId to disambiguate.";
                return false;
            }
            result = matches[0];
            return true;
        }

        /// <summary>Every GameObject (active or not) across every loaded scene whose own name
        /// exactly matches. Never touches project assets/prefabs not in a loaded scene.</summary>
        public static List<GameObject> FindAllByName(string name)
        {
            var results = new List<GameObject>();
            foreach (var go in AllSceneObjects())
                if (go.name == name)
                    results.Add(go);
            return results;
        }

        /// <summary>"Canvas/Panel/Button": the first segment matches a ROOT object's name in any
        /// loaded scene, and each following segment matches an immediate child by name. Fails if
        /// any segment is missing or the root segment is itself ambiguous across scenes.</summary>
        private static bool TryResolvePath(string path, out GameObject result, out string error)
        {
            result = null;
            error = null;
            var segments = path.Split('/');
            if (segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
            {
                error = $"Invalid hierarchy path '{path}'";
                return false;
            }

            var rootCandidates = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == segments[0])
                        rootCandidates.Add(root);
            }
            if (rootCandidates.Count == 0)
            {
                error = $"No root GameObject named '{segments[0]}' found in any loaded scene (from path '{path}').";
                return false;
            }
            if (rootCandidates.Count > 1)
            {
                error = $"Root segment '{segments[0]}' of path '{path}' matches {rootCandidates.Count} objects " +
                         "across loaded scenes — this resolver does not yet disambiguate root objects by scene name.";
                return false;
            }

            var current = rootCandidates[0].transform;
            for (int i = 1; i < segments.Length; i++)
            {
                var child = current.Find(segments[i]);
                if (child == null)
                {
                    error = $"'{path}' has no child '{segments[i]}' under '{GetHierarchyPath(current.gameObject)}'.";
                    return false;
                }
                current = child;
            }
            result = current.gameObject;
            return true;
        }

        private static IEnumerable<GameObject> AllSceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        yield return t.gameObject;
            }
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var current = go.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
