using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Generates an exploded view of an assembly for visualization: moves each child part
    /// outward from a common origin so internal structure is visible. Supports radial,
    /// axis-constrained, and custom directions, multiple part-selection strategies, and
    /// optional animation via a generated MonoBehaviour.
    /// </summary>
    public static class ViewExplodeTool
    {
        static readonly HashSet<string> ValidDirections = new HashSet<string>
        {
            "radial", "axis_x", "axis_y", "axis_z", "custom"
        };

        static readonly HashSet<string> ValidStrategies = new HashSet<string>
        {
            "direct_children", "all_renderers", "by_layer"
        };

        [MosaicTool("view/explode",
                    "Generates an exploded view of an assembly by translating parts outward from a common origin. Supports radial/axis/custom directions, multiple selection strategies (direct_children, all_renderers, by_layer), and optional animated tweening via a generated MonoBehaviour.",
                    isReadOnly: false, category: "view", Context = ToolContext.Both)]
        public static ToolResult<ViewExplodeResult> Execute(ViewExplodeParams p)
        {
            if (p == null)
                return ToolResult<ViewExplodeResult>.Fail(
                    "Params are required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrWhiteSpace(p.RootGameObject))
                return ToolResult<ViewExplodeResult>.Fail(
                    "RootGameObject is required", ErrorCodes.INVALID_PARAM);

            var direction = string.IsNullOrEmpty(p.Direction) ? "radial" : p.Direction.ToLowerInvariant();
            var strategy  = string.IsNullOrEmpty(p.Strategy)  ? "direct_children" : p.Strategy.ToLowerInvariant();

            if (!ValidDirections.Contains(direction))
                return ToolResult<ViewExplodeResult>.Fail(
                    $"Invalid Direction '{p.Direction}'. Valid: {string.Join(", ", ValidDirections)}",
                    ErrorCodes.INVALID_PARAM);

            if (!ValidStrategies.Contains(strategy))
                return ToolResult<ViewExplodeResult>.Fail(
                    $"Invalid Strategy '{p.Strategy}'. Valid: {string.Join(", ", ValidStrategies)}",
                    ErrorCodes.INVALID_PARAM);

            Vector3 customDir = Vector3.right;
            if (direction == "custom")
            {
                if (p.CustomDirection == null || p.CustomDirection.Length != 3)
                    return ToolResult<ViewExplodeResult>.Fail(
                        "CustomDirection must be a float[3] when Direction == 'custom'",
                        ErrorCodes.INVALID_PARAM);
                customDir = new Vector3(p.CustomDirection[0], p.CustomDirection[1], p.CustomDirection[2]);
                if (customDir.sqrMagnitude < 1e-8f)
                    return ToolResult<ViewExplodeResult>.Fail(
                        "CustomDirection must be non-zero", ErrorCodes.INVALID_PARAM);
                customDir = customDir.normalized;
            }

            if (p.Duration <= 0f && p.Animate)
                return ToolResult<ViewExplodeResult>.Fail(
                    "Duration must be > 0 when Animate is true", ErrorCodes.INVALID_PARAM);

            var root = GameObject.Find(p.RootGameObject);
            if (root == null)
                return ToolResult<ViewExplodeResult>.Fail(
                    $"RootGameObject '{p.RootGameObject}' not found", ErrorCodes.NOT_FOUND);

            // -------------------------------------------------------
            // 1. Gather parts according to the selected strategy.
            // -------------------------------------------------------
            var parts = GatherParts(root, strategy);

            // -------------------------------------------------------
            // 2. Compute origin (bounds center or transform.position).
            // -------------------------------------------------------
            var combinedBounds = ComputeCombinedBounds(root, parts);
            Vector3 origin = p.UseBoundsCenter && combinedBounds.HasValue
                ? combinedBounds.Value.center
                : root.transform.position;

            float boundsScale = combinedBounds.HasValue && combinedBounds.Value.size.magnitude > 1e-4f
                ? combinedBounds.Value.size.magnitude
                : 1f;

            // -------------------------------------------------------
            // 3. Compute displacements per part.
            // -------------------------------------------------------
            var originals = new List<Vector3>(parts.Count);
            var targets   = new List<Vector3>(parts.Count);

            for (int i = 0; i < parts.Count; i++)
            {
                var t = parts[i];
                Vector3 orig = t.position;
                Vector3 dir = ComputeDirection(direction, orig, origin, customDir);
                Vector3 disp = dir * boundsScale * p.ExplosionFactor;
                Vector3 target = orig + disp;

                originals.Add(orig);
                targets.Add(target);
            }

            // -------------------------------------------------------
            // 4. Apply or animate.
            // -------------------------------------------------------
            string scriptPath = null;

            if (!p.Animate)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    Undo.RecordObject(parts[i], "Mosaic: Explode Part");
                    parts[i].position = targets[i];
                }
            }
            else
            {
                scriptPath = GenerateAnimatorScript(root, p, originals, targets, parts);
            }

            return ToolResult<ViewExplodeResult>.Ok(new ViewExplodeResult
            {
                GameObjectName    = root.name,
                AffectedPartCount = parts.Count,
                ExplosionFactor   = p.ExplosionFactor,
                Strategy          = strategy,
                ScriptPath        = scriptPath
            });
        }

        // -----------------------------------------------------------
        // Part gathering
        // -----------------------------------------------------------
        static List<Transform> GatherParts(GameObject root, string strategy)
        {
            var parts = new List<Transform>();
            switch (strategy)
            {
                case "direct_children":
                {
                    for (int i = 0; i < root.transform.childCount; i++)
                        parts.Add(root.transform.GetChild(i));
                    break;
                }
                case "all_renderers":
                {
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    var seen = new HashSet<Transform>();
                    foreach (var r in renderers)
                    {
                        if (r == null || r.transform == root.transform) continue;
                        if (seen.Add(r.transform))
                            parts.Add(r.transform);
                    }
                    break;
                }
                case "by_layer":
                {
                    // Group direct children (and deeper renderers) by layer, then flatten.
                    // Ordering preserved per group to keep results deterministic.
                    var groups = new Dictionary<int, List<Transform>>();
                    var order = new List<int>();
                    var allT = new List<Transform>();
                    for (int i = 0; i < root.transform.childCount; i++)
                        allT.Add(root.transform.GetChild(i));
                    foreach (var t in allT)
                    {
                        int layer = t.gameObject.layer;
                        if (!groups.TryGetValue(layer, out var list))
                        {
                            list = new List<Transform>();
                            groups[layer] = list;
                            order.Add(layer);
                        }
                        list.Add(t);
                    }
                    foreach (var layer in order)
                    {
                        foreach (var t in groups[layer])
                            parts.Add(t);
                    }
                    break;
                }
            }
            return parts;
        }

        // -----------------------------------------------------------
        // Bounds + direction helpers
        // -----------------------------------------------------------
        static Bounds? ComputeCombinedBounds(GameObject root, List<Transform> parts)
        {
            bool any = false;
            Bounds b = new Bounds();

            // Prefer renderers under each part; fall back to part.position if none.
            foreach (var t in parts)
            {
                var renderers = t.GetComponentsInChildren<Renderer>(true);
                if (renderers != null && renderers.Length > 0)
                {
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        if (!any) { b = r.bounds; any = true; }
                        else      { b.Encapsulate(r.bounds); }
                    }
                }
                else
                {
                    if (!any) { b = new Bounds(t.position, Vector3.zero); any = true; }
                    else      { b.Encapsulate(t.position); }
                }
            }

            if (!any)
            {
                // Fall back to root renderers only.
                var rs = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rs)
                {
                    if (r == null) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else      { b.Encapsulate(r.bounds); }
                }
            }

            return any ? (Bounds?)b : null;
        }

        static Vector3 ComputeDirection(string direction, Vector3 partPos, Vector3 origin, Vector3 customDir)
        {
            switch (direction)
            {
                case "radial":
                {
                    var d = partPos - origin;
                    if (d.sqrMagnitude < 1e-8f) return Vector3.up; // fallback for coincident origin
                    return d.normalized;
                }
                case "axis_x":
                {
                    float sign = Mathf.Sign(partPos.x - origin.x);
                    if (Mathf.Approximately(partPos.x, origin.x)) sign = 1f;
                    return new Vector3(sign, 0f, 0f);
                }
                case "axis_y":
                {
                    float sign = Mathf.Sign(partPos.y - origin.y);
                    if (Mathf.Approximately(partPos.y, origin.y)) sign = 1f;
                    return new Vector3(0f, sign, 0f);
                }
                case "axis_z":
                {
                    float sign = Mathf.Sign(partPos.z - origin.z);
                    if (Mathf.Approximately(partPos.z, origin.z)) sign = 1f;
                    return new Vector3(0f, 0f, sign);
                }
                case "custom":
                    return customDir;
            }
            return Vector3.zero;
        }

        // -----------------------------------------------------------
        // Animator script generation
        // -----------------------------------------------------------
        static string GenerateAnimatorScript(GameObject root, ViewExplodeParams p,
            List<Vector3> originals, List<Vector3> targets, List<Transform> parts)
        {
            var savePath = string.IsNullOrEmpty(p.SavePath)
                ? "Assets/Generated/DataViz/"
                : p.SavePath;
            if (!savePath.StartsWith("Assets/")) savePath = "Assets/Generated/DataViz/";
            if (!savePath.EndsWith("/")) savePath += "/";

            const string className = "ExplodedViewAnimator";
            var scriptFileName = className + ".cs";
            var scriptAssetPath = savePath + scriptFileName;

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, scriptAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            if (!File.Exists(fullPath))
            {
                File.WriteAllText(fullPath, BuildAnimatorSource(className), Encoding.UTF8);
                AssetDatabase.ImportAsset(scriptAssetPath);
            }

            // Try to attach the component immediately (may be unavailable on first generation
            // until domain reload completes).
            var animatorType = FindTypeByName(className);
            if (animatorType != null)
            {
                Undo.RegisterCompleteObjectUndo(root, "Add ExplodedViewAnimator");
                var comp = root.GetComponent(animatorType);
                if (comp == null) comp = root.AddComponent(animatorType);

                TrySetField(comp, "duration", p.Duration);
                TrySetField(comp, "parts", parts.ToArray());
                TrySetField(comp, "originalPositions", originals.ToArray());
                TrySetField(comp, "targetPositions", targets.ToArray());
            }

            return scriptAssetPath;
        }

        static string BuildAnimatorSource(string className)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Auto-generated by Mosaic view/explode tool.");
            sb.AppendLine("/// Tweens an assembly between its original positions and exploded positions over 'duration' seconds.");
            sb.AppendLine("/// Call Play() to start; call Reset() to snap back.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    public Transform[] parts;");
            sb.AppendLine("    public Vector3[] originalPositions;");
            sb.AppendLine("    public Vector3[] targetPositions;");
            sb.AppendLine("    public float duration = 1.5f;");
            sb.AppendLine("    public bool playOnStart = true;");
            sb.AppendLine();
            sb.AppendLine("    private float _t;");
            sb.AppendLine("    private bool _playing;");
            sb.AppendLine();
            sb.AppendLine("    void Start() { if (playOnStart) Play(); }");
            sb.AppendLine();
            sb.AppendLine("    public void Play()");
            sb.AppendLine("    {");
            sb.AppendLine("        _t = 0f;");
            sb.AppendLine("        _playing = true;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public void ResetPositions()");
            sb.AppendLine("    {");
            sb.AppendLine("        _playing = false;");
            sb.AppendLine("        _t = 0f;");
            sb.AppendLine("        if (parts == null || originalPositions == null) return;");
            sb.AppendLine("        int n = Mathf.Min(parts.Length, originalPositions.Length);");
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("            if (parts[i] != null) parts[i].position = originalPositions[i];");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (!_playing) return;");
            sb.AppendLine("        if (parts == null || originalPositions == null || targetPositions == null) { _playing = false; return; }");
            sb.AppendLine("        _t += Time.deltaTime;");
            sb.AppendLine("        float k = duration > 0f ? Mathf.Clamp01(_t / duration) : 1f;");
            sb.AppendLine("        float e = Mathf.SmoothStep(0f, 1f, k);");
            sb.AppendLine("        int n = Mathf.Min(parts.Length, Mathf.Min(originalPositions.Length, targetPositions.Length));");
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("            if (parts[i] != null) parts[i].position = Vector3.LerpUnclamped(originalPositions[i], targetPositions[i], e);");
            sb.AppendLine("        if (k >= 1f) _playing = false;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        static void TrySetField(Component comp, string fieldName, object value)
        {
            if (comp == null) return;
            var f = comp.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(comp, value);
        }
    }
}
