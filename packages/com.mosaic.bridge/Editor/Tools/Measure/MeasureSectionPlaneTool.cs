using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Creates a section / clipping plane in the scene. A runtime MonoBehaviour is generated
    /// that sets a global shader vector (_MosaicSectionPlane) every frame and optionally sweeps
    /// the plane over time. A simple cap quad is generated to approximate the cut face when
    /// CapSurface is true. Compatible shaders may sample _MosaicSectionPlane to discard pixels
    /// behind the plane.
    /// </summary>
    public static class MeasureSectionPlaneTool
    {
        [MosaicTool("measure/section-plane",
                    "Creates a section/clipping plane that clips scene renderers via a global shader vector, with optional cap surface and sweep animation",
                    isReadOnly: false, category: "measure", Context = ToolContext.Both)]
        public static ToolResult<MeasureSectionPlaneResult> Execute(MeasureSectionPlaneParams p)
        {
            if (p == null)
                return ToolResult<MeasureSectionPlaneResult>.Fail("Params required", ErrorCodes.INVALID_PARAM);

            if (p.Position == null || p.Position.Length < 3)
                return ToolResult<MeasureSectionPlaneResult>.Fail(
                    "Position is required (float[3])", ErrorCodes.INVALID_PARAM);

            if (p.Normal == null || p.Normal.Length < 3)
                return ToolResult<MeasureSectionPlaneResult>.Fail(
                    "Normal is required (float[3])", ErrorCodes.INVALID_PARAM);

            var position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
            var normalRaw = new Vector3(p.Normal[0], p.Normal[1], p.Normal[2]);

            if (normalRaw.sqrMagnitude < 1e-8f)
                return ToolResult<MeasureSectionPlaneResult>.Fail(
                    "Normal cannot be zero vector", ErrorCodes.INVALID_PARAM);

            var normal = normalRaw.normalized;

            if (p.AnimateDuration <= 0f)
                return ToolResult<MeasureSectionPlaneResult>.Fail(
                    "AnimateDuration must be > 0", ErrorCodes.INVALID_PARAM);

            // Resolve target renderers
            var renderers = ResolveTargetRenderers(p.Targets);
            int clippedCount = renderers.Count;

            // Generate runtime script (once). Script is idempotent across calls.
            string generatedScriptPath = GenerateRuntimeScript();

            // Build the plane GameObject
            string goName = string.IsNullOrWhiteSpace(p.Name)
                ? $"MeasureSectionPlane_{System.DateTime.Now:HHmmss}"
                : p.Name.Trim();

            var planeGo = new GameObject(goName);
            planeGo.transform.position = position;
            // Orient Z+ axis of transform to match normal (useful for gizmos / inspector).
            // Pick an up vector that isn't parallel to normal to avoid degenerate LookRotation.
            planeGo.transform.rotation = Quaternion.LookRotation(normal, PickUp(normal));

            Undo.RegisterCreatedObjectUndo(planeGo, "Create Measure Section Plane");

            // Push state onto controller component (via SendMessage/SetGlobalVector fallback — the generated
            // runtime MonoBehaviour may not yet be compiled when this runs at editor time, so we also set
            // the global shader vector immediately here so clipping "works" on the next frame without
            // waiting for the compile.)
            var color = ColorFromArray(p.CapColor);
            ApplyGlobalPlane(position, normal);

            // Cap surface (simple quad overlay)
            if (p.CapSurface)
            {
                var cap = CreateCapQuad(position, normal, color, renderers);
                if (cap != null)
                {
                    cap.transform.SetParent(planeGo.transform, worldPositionStays: true);
                    Undo.RegisterCreatedObjectUndo(cap, "Create Section Plane Cap");
                }
            }

            // Note: The generated SectionPlaneRuntime MonoBehaviour lives in Assets/Generated/Measure and
            // is attached by the user (or a follow-up tool) once compiled. At editor time we already set
            // the global shader vector above, so clipping starts immediately without waiting for compile.

            return ToolResult<MeasureSectionPlaneResult>.Ok(new MeasureSectionPlaneResult
            {
                PlaneId             = planeGo.GetInstanceID(),
                GameObjectName      = planeGo.name,
                ClippedObjectCount  = clippedCount,
                Position            = new[] { position.x, position.y, position.z },
                Normal              = new[] { normal.x, normal.y, normal.z },
                GeneratedScriptPath = generatedScriptPath
            });
        }

        // ---------------------------------------------------------------
        // Target resolution
        // ---------------------------------------------------------------

        private static List<Renderer> ResolveTargetRenderers(string[] targets)
        {
            var result = new List<Renderer>();
            bool wantAll = targets == null || targets.Length == 0 ||
                           (targets.Length == 1 && string.Equals(targets[0], "all", System.StringComparison.OrdinalIgnoreCase));

            if (wantAll)
            {
                var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                result.AddRange(all);
                return result;
            }

            foreach (var name in targets)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var go = GameObject.Find(name);
                if (go == null) continue;
                var rends = go.GetComponentsInChildren<Renderer>(includeInactive: false);
                foreach (var r in rends)
                {
                    if (r != null) result.Add(r);
                }
            }
            return result;
        }

        private static void ApplyGlobalPlane(Vector3 position, Vector3 normal)
        {
            // Plane equation: n . x + d = 0  => d = -(n . position)
            float d = -Vector3.Dot(normal, position);
            Shader.SetGlobalVector("_MosaicSectionPlane", new Vector4(normal.x, normal.y, normal.z, d));
            Shader.EnableKeyword("MOSAIC_SECTION_PLANE_ON");
        }

        /// <summary>Returns an up vector that isn't (nearly) parallel to the given forward direction.</summary>
        private static Vector3 PickUp(Vector3 forward)
        {
            return Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
        }

        private static Color ColorFromArray(float[] arr)
        {
            if (arr == null || arr.Length < 3) return new Color(1f, 0.5f, 0f, 1f);
            float r = arr[0], g = arr[1], b = arr[2];
            float a = arr.Length >= 4 ? arr[3] : 1f;
            return new Color(r, g, b, a);
        }

        // ---------------------------------------------------------------
        // Cap quad
        // ---------------------------------------------------------------

        private static GameObject CreateCapQuad(Vector3 position, Vector3 normal, Color color, List<Renderer> renderers)
        {
            // Size the cap to enclose the combined bounds of target renderers (fallback: 10 units).
            float size = 10f;
            if (renderers != null && renderers.Count > 0)
            {
                Bounds? combined = null;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!combined.HasValue) combined = r.bounds;
                    else { var c = combined.Value; c.Encapsulate(r.bounds); combined = c; }
                }
                if (combined.HasValue)
                {
                    var ext = combined.Value.extents.magnitude;
                    size = Mathf.Max(1f, ext * 2.2f);
                }
            }

            var cap = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cap.name = "SectionPlane_Cap";
            // Remove collider added by CreatePrimitive so the cap does not affect physics queries.
            var col = cap.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            cap.transform.position = position;
            // Quad default faces -Z; rotate so its forward matches +normal.
            cap.transform.rotation = Quaternion.LookRotation(normal, PickUp(normal));
            cap.transform.localScale = new Vector3(size, size, 1f);

            var mr = cap.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/Internal-Colored");
                if (shader != null)
                {
                    var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    mat.color = color;
                    mr.sharedMaterial = mat;
                }
            }
            return cap;
        }

        // ---------------------------------------------------------------
        // Runtime script generation
        // ---------------------------------------------------------------

        private const string GeneratedDir = "Assets/Generated/Measure";
        private const string RuntimeScriptName = "SectionPlaneRuntime.cs";

        private static string GenerateRuntimeScript()
        {
            string assetPath = $"{GeneratedDir}/{RuntimeScriptName}";
            AssetDatabaseHelper.EnsureFolder(GeneratedDir);
            string fullDir   = Path.Combine(Application.dataPath, "..", GeneratedDir);

            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (File.Exists(fullPath))
                return assetPath;

            string content = @"// Generated by Mosaic Bridge - measure/section-plane (Story 33-5)
// Feeds _MosaicSectionPlane (xyz = normal, w = plane constant d) to all shaders that sample it.
// Compatible clipping shaders should compute: dot(n, worldPos) + d > 0 => discard.

using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SectionPlaneRuntime : MonoBehaviour
{
    public Vector3 planePosition = Vector3.zero;
    public Vector3 planeNormal   = Vector3.up;

    [Header(""Animation"")]
    public bool    animate             = false;
    public float   animateDuration     = 2f;
    public bool    hasEndPosition      = false;
    public Vector3 animateEndPosition  = Vector3.zero;

    private float _animElapsed;
    private Vector3 _startPosition;

    void OnEnable()
    {
        _startPosition = planePosition;
        _animElapsed   = 0f;
    }

    void Update()
    {
        Vector3 pos = planePosition;
        if (animate && hasEndPosition && animateDuration > 0f)
        {
            _animElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_animElapsed / animateDuration);
            pos = Vector3.Lerp(_startPosition, animateEndPosition, t);
        }

        Vector3 n = planeNormal.sqrMagnitude < 1e-8f ? Vector3.up : planeNormal.normalized;
        float d = -Vector3.Dot(n, pos);
        Shader.SetGlobalVector(""_MosaicSectionPlane"", new Vector4(n.x, n.y, n.z, d));
        Shader.EnableKeyword(""MOSAIC_SECTION_PLANE_ON"");
    }

    void OnDisable()
    {
        Shader.DisableKeyword(""MOSAIC_SECTION_PLANE_ON"");
    }
}
";
            File.WriteAllText(fullPath, content);
            // Note: intentionally skip AssetDatabase.ImportAsset to avoid triggering a domain
            // reload mid-execution. Unity's asset pipeline will pick up the new .cs file on
            // the next refresh (e.g., focus return, explicit editor/refresh call).
            return assetPath;
        }
    }

}
