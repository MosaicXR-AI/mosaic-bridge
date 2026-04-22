using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsVerletCreateTool
    {
        static readonly string[] ValidTypes = { "rope", "cloth", "chain", "softbody" };

        [MosaicTool("physics/verlet-create",
                    "Generates a Verlet integration MonoBehaviour (rope, cloth, chain, softbody) with constraint solver and visualization",
                    isReadOnly: false, category: "physics", Context = ToolContext.Both)]
        public static ToolResult<PhysicsVerletCreateResult> Execute(PhysicsVerletCreateParams p)
        {
            // -------- Validation --------
            if (string.IsNullOrWhiteSpace(p.Type))
                return ToolResult<PhysicsVerletCreateResult>.Fail(
                    "Type is required (rope, cloth, chain, softbody)",
                    ErrorCodes.INVALID_PARAM);

            var type = p.Type.Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidTypes, type) < 0)
                return ToolResult<PhysicsVerletCreateResult>.Fail(
                    $"Invalid Type '{p.Type}'. Valid: {string.Join(", ", ValidTypes)}",
                    ErrorCodes.INVALID_PARAM);

            int pointCount       = p.PointCount       ?? 20;
            float segmentLength  = p.SegmentLength    ?? 0.5f;
            float stiffness      = p.Stiffness        ?? 0.8f;
            float damping        = p.Damping          ?? 0.01f;
            float gravity        = p.Gravity          ?? -9.81f;
            int solverIterations = p.SolverIterations ?? 5;
            float collisionRadius = p.CollisionRadius ?? 0.05f;

            // Clamp to sane ranges
            if (pointCount < 2) pointCount = 2;
            if (segmentLength <= 0f) segmentLength = 0.01f;
            stiffness        = Mathf.Clamp01(stiffness);
            damping          = Mathf.Clamp(damping, 0f, 1f);
            if (solverIterations < 1) solverIterations = 1;
            if (collisionRadius < 0f) collisionRadius = 0f;

            // Cap cloth totals
            if (type == "cloth" && pointCount * pointCount > 65536)
                return ToolResult<PhysicsVerletCreateResult>.Fail(
                    "Cloth PointCount*PointCount must not exceed 65536",
                    ErrorCodes.INVALID_PARAM);

            // Pinned point defaults: rope/chain pin [0] if not specified
            int[] pinPoints = p.PinPoints;
            if (pinPoints == null && (type == "rope" || type == "chain"))
                pinPoints = new[] { 0 };
            if (pinPoints == null)
                pinPoints = new int[0];

            int totalPoints = type == "cloth" ? pointCount * pointCount : pointCount;
            // Final pointCount reported in result
            int reportedPointCount = totalPoints;

            // -------- SavePath --------
            var savePath = string.IsNullOrEmpty(p.SavePath)
                ? "Assets/Generated/Physics/"
                : p.SavePath;

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<PhysicsVerletCreateResult>.Fail(
                    "SavePath must start with 'Assets/'",
                    ErrorCodes.INVALID_PARAM);

            if (!savePath.EndsWith("/"))
                savePath += "/";

            // -------- Identifier / paths --------
            var rawName = string.IsNullOrWhiteSpace(p.Name) ? "VerletSystem" : p.Name;
            var sanitizedName = SanitizeIdentifier(rawName);
            var className = $"VerletSystem_{sanitizedName}";
            var scriptFileName = $"{className}.cs";
            var scriptAssetPath = savePath + scriptFileName;

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            // Do NOT strip "Assets/" — combine project root + full Assets-relative path
            AssetDatabaseHelper.EnsureFolder(savePath);
            var fullDir = Path.Combine(projectRoot, savePath);
            var fullPath = Path.Combine(projectRoot, scriptAssetPath);

            // -------- Generate script --------
            var pinCsv = BuildIntArrayLiteral(pinPoints);
            var script = BuildScript(
                className, type, pointCount, segmentLength, stiffness, damping, gravity,
                pinCsv, solverIterations, collisionRadius);

            File.WriteAllText(fullPath, script, Encoding.UTF8);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(scriptAssetPath);

            // -------- Create GameObject --------
            var go = new GameObject(rawName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {rawName}");

            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            // Attach to parent if requested
            if (!string.IsNullOrEmpty(p.AttachTo))
            {
                var parent = GameObject.Find(p.AttachTo);
                if (parent != null)
                {
                    Undo.SetTransformParent(go.transform, parent.transform, $"Parent {rawName}");
                }
            }

            // Add visualization component (LineRenderer for rope/chain, MeshFilter+Renderer for cloth/softbody)
            if (type == "rope" || type == "chain")
            {
                var lr = Undo.AddComponent<LineRenderer>(go);
                lr.positionCount = pointCount;
                lr.widthMultiplier = Mathf.Max(0.01f, collisionRadius * 2f);
                lr.useWorldSpace = true;
            }
            else
            {
                Undo.AddComponent<MeshFilter>(go);
                Undo.AddComponent<MeshRenderer>(go);
            }

            // Try to attach the generated component (may be null if not yet compiled)
            var scriptType = FindTypeByName(className);
            if (scriptType != null)
            {
                Undo.AddComponent(go, scriptType);
            }

            return ToolResult<PhysicsVerletCreateResult>.Ok(new PhysicsVerletCreateResult
            {
                ScriptPath     = scriptAssetPath,
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                Type           = type,
                PointCount     = reportedPointCount
            });
        }

        // ---------------------------------------------------------------
        // Script generation
        // ---------------------------------------------------------------

        private static string BuildScript(
            string className, string type,
            int pointCount, float segmentLength, float stiffness, float damping,
            float gravity, string pinPointsCsv, int solverIterations, float collisionRadius)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// Generated by Mosaic Bridge - physics/verlet-create");
            sb.AppendLine("// Verlet integration (Jakobsen 2001) - customize as needed");
            sb.AppendLine();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine($"    [Header(\"Verlet Parameters\")]");
            sb.AppendLine($"    public string systemType = \"{type}\";");
            sb.AppendLine($"    public int pointCount = {pointCount};");
            sb.AppendLine($"    public float segmentLength = {FloatLit(segmentLength)};");
            sb.AppendLine($"    [Range(0f,1f)] public float stiffness = {FloatLit(stiffness)};");
            sb.AppendLine($"    [Range(0f,1f)] public float damping = {FloatLit(damping)};");
            sb.AppendLine($"    public float gravity = {FloatLit(gravity)};");
            sb.AppendLine($"    public int solverIterations = {solverIterations};");
            sb.AppendLine($"    public float collisionRadius = {FloatLit(collisionRadius)};");
            sb.AppendLine($"    public int[] pinPoints = new int[] {{ {pinPointsCsv} }};");
            sb.AppendLine();
            sb.AppendLine("    // Per-point state");
            sb.AppendLine("    private Vector3[] positions;");
            sb.AppendLine("    private Vector3[] previousPositions;");
            sb.AppendLine("    private Vector3[] originalPositions;");
            sb.AppendLine("    private bool[] isPinned;");
            sb.AppendLine();
            sb.AppendLine("    // Cloth grid edge length (for cloth/softbody)");
            sb.AppendLine("    private int gridSize;");
            sb.AppendLine("    private bool isCloth;");
            sb.AppendLine();
            sb.AppendLine("    // Visualization");
            sb.AppendLine("    private LineRenderer _lineRenderer;");
            sb.AppendLine("    private Mesh _mesh;");
            sb.AppendLine("    private Vector3[] _meshVertices;");
            sb.AppendLine();
            sb.AppendLine("    void Start()");
            sb.AppendLine("    {");
            sb.AppendLine("        isCloth = (systemType == \"cloth\" || systemType == \"softbody\");");
            sb.AppendLine("        int total;");
            sb.AppendLine("        if (isCloth)");
            sb.AppendLine("        {");
            sb.AppendLine("            gridSize = pointCount;");
            sb.AppendLine("            total = gridSize * gridSize;");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");
            sb.AppendLine("            total = pointCount;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        positions         = new Vector3[total];");
            sb.AppendLine("        previousPositions = new Vector3[total];");
            sb.AppendLine("        originalPositions = new Vector3[total];");
            sb.AppendLine("        isPinned          = new bool[total];");
            sb.AppendLine();
            sb.AppendLine("        Vector3 origin = transform.position;");
            sb.AppendLine("        if (isCloth)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int y = 0; y < gridSize; y++)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int x = 0; x < gridSize; x++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int idx = y * gridSize + x;");
            sb.AppendLine("                    Vector3 p = origin + new Vector3(x * segmentLength, 0f, y * segmentLength);");
            sb.AppendLine("                    positions[idx] = p;");
            sb.AppendLine("                    previousPositions[idx] = p;");
            sb.AppendLine("                    originalPositions[idx] = p;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int i = 0; i < total; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                Vector3 p = origin + new Vector3(0f, -i * segmentLength, 0f);");
            sb.AppendLine("                positions[i] = p;");
            sb.AppendLine("                previousPositions[i] = p;");
            sb.AppendLine("                originalPositions[i] = p;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        if (pinPoints != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            foreach (var idx in pinPoints)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (idx >= 0 && idx < total) isPinned[idx] = true;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        SetupVisualization();");
            sb.AppendLine("    }");
            sb.AppendLine();

            // SetupVisualization
            sb.AppendLine("    void SetupVisualization()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (!isCloth)");
            sb.AppendLine("        {");
            sb.AppendLine("            _lineRenderer = GetComponent<LineRenderer>();");
            sb.AppendLine("            if (_lineRenderer != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                _lineRenderer.positionCount = positions.Length;");
            sb.AppendLine("                _lineRenderer.useWorldSpace = true;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");
            sb.AppendLine("            var mf = GetComponent<MeshFilter>();");
            sb.AppendLine("            if (mf == null) return;");
            sb.AppendLine("            _mesh = new Mesh();");
            sb.AppendLine("            _mesh.indexFormat = positions.Length > 65535");
            sb.AppendLine("                ? UnityEngine.Rendering.IndexFormat.UInt32");
            sb.AppendLine("                : UnityEngine.Rendering.IndexFormat.UInt16;");
            sb.AppendLine("            _meshVertices = new Vector3[positions.Length];");
            sb.AppendLine("            var uv = new Vector2[positions.Length];");
            sb.AppendLine("            for (int y = 0; y < gridSize; y++)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int x = 0; x < gridSize; x++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int idx = y * gridSize + x;");
            sb.AppendLine("                    _meshVertices[idx] = positions[idx] - transform.position;");
            sb.AppendLine("                    uv[idx] = new Vector2((float)x / Mathf.Max(1, gridSize - 1), (float)y / Mathf.Max(1, gridSize - 1));");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            _mesh.vertices = _meshVertices;");
            sb.AppendLine("            _mesh.uv = uv;");
            sb.AppendLine();
            sb.AppendLine("            var tris = new List<int>();");
            sb.AppendLine("            for (int y = 0; y < gridSize - 1; y++)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int x = 0; x < gridSize - 1; x++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int i0 = y * gridSize + x;");
            sb.AppendLine("                    int i1 = i0 + 1;");
            sb.AppendLine("                    int i2 = i0 + gridSize;");
            sb.AppendLine("                    int i3 = i2 + 1;");
            sb.AppendLine("                    tris.Add(i0); tris.Add(i2); tris.Add(i1);");
            sb.AppendLine("                    tris.Add(i1); tris.Add(i2); tris.Add(i3);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            _mesh.triangles = tris.ToArray();");
            sb.AppendLine("            _mesh.RecalculateNormals();");
            sb.AppendLine("            mf.mesh = _mesh;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Integrate
            sb.AppendLine("    void FixedUpdate()");
            sb.AppendLine("    {");
            sb.AppendLine("        float dt = Time.fixedDeltaTime;");
            sb.AppendLine("        Vector3 g = new Vector3(0f, gravity, 0f);");
            sb.AppendLine();
            sb.AppendLine("        // Verlet integration");
            sb.AppendLine("        for (int i = 0; i < positions.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (isPinned[i]) continue;");
            sb.AppendLine("            Vector3 temp = positions[i];");
            sb.AppendLine("            positions[i] += (positions[i] - previousPositions[i]) * (1f - damping) + g * dt * dt;");
            sb.AppendLine("            previousPositions[i] = temp;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Constraint solver");
            sb.AppendLine("        for (int iter = 0; iter < solverIterations; iter++)");
            sb.AppendLine("        {");
            sb.AppendLine("            SolveConstraints();");
            sb.AppendLine("            // Re-pin");
            sb.AppendLine("            for (int i = 0; i < positions.Length; i++)");
            sb.AppendLine("                if (isPinned[i]) positions[i] = originalPositions[i];");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        UpdateVisualization();");
            sb.AppendLine("    }");
            sb.AppendLine();

            // SolveConstraints
            sb.AppendLine("    void SolveConstraints()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (isCloth)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Structural + shear constraints");
            sb.AppendLine("            float shearRest = segmentLength * 1.41421356f;");
            sb.AppendLine("            for (int y = 0; y < gridSize; y++)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int x = 0; x < gridSize; x++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int i0 = y * gridSize + x;");
            sb.AppendLine("                    if (x + 1 < gridSize) SatisfyConstraint(i0, i0 + 1, segmentLength);");
            sb.AppendLine("                    if (y + 1 < gridSize) SatisfyConstraint(i0, i0 + gridSize, segmentLength);");
            sb.AppendLine("                    if (x + 1 < gridSize && y + 1 < gridSize)");
            sb.AppendLine("                        SatisfyConstraint(i0, i0 + gridSize + 1, shearRest);");
            sb.AppendLine("                    if (x > 0 && y + 1 < gridSize)");
            sb.AppendLine("                        SatisfyConstraint(i0, i0 + gridSize - 1, shearRest);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int i = 0; i < positions.Length - 1; i++)");
            sb.AppendLine("                SatisfyConstraint(i, i + 1, segmentLength);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // SatisfyConstraint
            sb.AppendLine("    void SatisfyConstraint(int a, int b, float restLength)");
            sb.AppendLine("    {");
            sb.AppendLine("        Vector3 delta = positions[b] - positions[a];");
            sb.AppendLine("        float dist = delta.magnitude;");
            sb.AppendLine("        if (dist < 1e-6f) return;");
            sb.AppendLine("        float diff = (dist - restLength) / dist;");
            sb.AppendLine("        Vector3 correction = delta * 0.5f * diff * stiffness;");
            sb.AppendLine("        if (!isPinned[a]) positions[a] += correction;");
            sb.AppendLine("        if (!isPinned[b]) positions[b] -= correction;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // UpdateVisualization
            sb.AppendLine("    void UpdateVisualization()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (!isCloth && _lineRenderer != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int i = 0; i < positions.Length; i++)");
            sb.AppendLine("                _lineRenderer.SetPosition(i, positions[i]);");
            sb.AppendLine("        }");
            sb.AppendLine("        else if (isCloth && _mesh != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int i = 0; i < positions.Length; i++)");
            sb.AppendLine("                _meshVertices[i] = positions[i] - transform.position;");
            sb.AppendLine("            _mesh.vertices = _meshVertices;");
            sb.AppendLine("            _mesh.RecalculateNormals();");
            sb.AppendLine("            _mesh.RecalculateBounds();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static string FloatLit(float f)
        {
            return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f";
        }

        private static string BuildIntArrayLiteral(int[] arr)
        {
            if (arr == null || arr.Length == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(arr[i]);
            }
            return sb.ToString();
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_') sb.Append(ch);
            }
            var result = sb.ToString();
            if (result.Length == 0 || char.IsDigit(result[0]))
                result = "_" + result;
            return result;
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
