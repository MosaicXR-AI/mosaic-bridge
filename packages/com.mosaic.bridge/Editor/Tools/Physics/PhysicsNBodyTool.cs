using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsNBodyTool
    {
        static readonly string[] ValidIntegrators = { "verlet", "leapfrog", "rk4" };

        [MosaicTool("physics/nbody",
                    "Generates a Barnes-Hut N-body gravitational simulation MonoBehaviour with octree spatial subdivision (verlet/leapfrog/rk4 integrators)",
                    isReadOnly: false, category: "physics", Context = ToolContext.Both)]
        public static ToolResult<PhysicsNBodyResult> Execute(PhysicsNBodyParams p)
        {
            // -------- Validation --------
            if (p == null)
                return ToolResult<PhysicsNBodyResult>.Fail(
                    "Params required", ErrorCodes.INVALID_PARAM);

            if (p.Bodies == null || p.Bodies.Count == 0)
                return ToolResult<PhysicsNBodyResult>.Fail(
                    "Bodies is required and must contain at least one body",
                    ErrorCodes.INVALID_PARAM);

            for (int i = 0; i < p.Bodies.Count; i++)
            {
                var b = p.Bodies[i];
                if (b == null)
                    return ToolResult<PhysicsNBodyResult>.Fail(
                        $"Bodies[{i}] is null", ErrorCodes.INVALID_PARAM);
                if (b.Position == null || b.Position.Length != 3)
                    return ToolResult<PhysicsNBodyResult>.Fail(
                        $"Bodies[{i}].Position must be float[3]", ErrorCodes.INVALID_PARAM);
                if (!(b.Mass > 0f))
                    return ToolResult<PhysicsNBodyResult>.Fail(
                        $"Bodies[{i}].Mass must be greater than 0", ErrorCodes.INVALID_PARAM);
                if (b.Velocity != null && b.Velocity.Length != 3)
                    return ToolResult<PhysicsNBodyResult>.Fail(
                        $"Bodies[{i}].Velocity must be float[3] if provided",
                        ErrorCodes.INVALID_PARAM);
            }

            string integrator = string.IsNullOrWhiteSpace(p.Integrator)
                ? "leapfrog"
                : p.Integrator.Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidIntegrators, integrator) < 0)
                return ToolResult<PhysicsNBodyResult>.Fail(
                    $"Invalid Integrator '{p.Integrator}'. Valid: {string.Join(", ", ValidIntegrators)}",
                    ErrorCodes.INVALID_PARAM);

            float g         = p.GravitationalConstant ?? 6.674e-11f;
            float theta     = p.Theta                 ?? 0.5f;
            float softening = p.Softening             ?? 0.1f;
            float timeStep  = p.TimeStep              ?? 0.01f;

            // Clamp theta to [0, 2]
            theta = Mathf.Clamp(theta, 0f, 2f);
            if (softening < 0f) softening = 0f;
            if (timeStep <= 0f) timeStep = 0.0001f;

            // -------- SavePath --------
            var savePath = string.IsNullOrEmpty(p.SavePath)
                ? "Assets/Generated/Physics/"
                : p.SavePath;

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<PhysicsNBodyResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!savePath.EndsWith("/"))
                savePath += "/";

            // -------- Identifier / paths --------
            var rawName = string.IsNullOrWhiteSpace(p.Name) ? "NBodySystem" : p.Name;
            var sanitizedName = SanitizeIdentifier(rawName);
            var className = $"NBodySystem_{sanitizedName}";
            var scriptFileName = $"{className}.cs";
            var scriptAssetPath = savePath + scriptFileName;

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullDir = Path.Combine(projectRoot, savePath);
            Directory.CreateDirectory(fullDir);
            var fullPath = Path.Combine(projectRoot, scriptAssetPath);

            // -------- Generate script --------
            var script = BuildScript(
                className, p.Bodies, g, theta, softening, timeStep, integrator, p.BodyPrefabPath);

            File.WriteAllText(fullPath, script, Encoding.UTF8);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(scriptAssetPath);

            // -------- Create GameObject --------
            var go = new GameObject(rawName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {rawName}");

            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            // Try to attach the generated component (may be null if not yet compiled)
            var scriptType = FindTypeByName(className);
            if (scriptType != null)
                Undo.AddComponent(go, scriptType);

            return ToolResult<PhysicsNBodyResult>.Ok(new PhysicsNBodyResult
            {
                ScriptPath     = scriptAssetPath,
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                BodyCount      = p.Bodies.Count,
                Integrator     = integrator,
                Theta          = theta
            });
        }

        // ---------------------------------------------------------------
        // Script generation
        // ---------------------------------------------------------------

        private static string BuildScript(
            string className,
            System.Collections.Generic.List<PhysicsNBodyParams.Body> bodies,
            float g, float theta, float softening, float timeStep, string integrator,
            string bodyPrefabPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// Generated by Mosaic Bridge - physics/nbody");
            sb.AppendLine("// Barnes-Hut N-body gravitational simulation (Barnes & Hut 1986)");
            sb.AppendLine();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    [Header(\"N-Body Parameters\")]");
            sb.AppendLine($"    public float gravitationalConstant = {FloatLit(g)};");
            sb.AppendLine($"    public float theta = {FloatLit(theta)};");
            sb.AppendLine($"    public float softening = {FloatLit(softening)};");
            sb.AppendLine($"    public float timeStep = {FloatLit(timeStep)};");
            sb.AppendLine($"    public string integrator = \"{integrator}\";");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Rendering\")]");
            if (!string.IsNullOrEmpty(bodyPrefabPath))
                sb.AppendLine($"    public GameObject bodyPrefab; // Configure in Inspector or load from '{EscapeString(bodyPrefabPath)}'");
            else
                sb.AppendLine("    public GameObject bodyPrefab; // Optional; falls back to sphere primitive");
            sb.AppendLine();

            sb.AppendLine("    // Per-body state");
            sb.AppendLine("    private struct NBody");
            sb.AppendLine("    {");
            sb.AppendLine("        public Transform transform;");
            sb.AppendLine("        public float     mass;");
            sb.AppendLine("        public Vector3   position;");
            sb.AppendLine("        public Vector3   previousPosition; // for Verlet");
            sb.AppendLine("        public Vector3   velocity;");
            sb.AppendLine("        public Vector3   acceleration;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private NBody[] _bodies;");
            sb.AppendLine();

            // Initial data arrays
            sb.AppendLine("    // Initial body data (position, mass, velocity)");
            sb.Append("    private static readonly Vector3[] _initialPositions = new Vector3[] { ");
            for (int i = 0; i < bodies.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var pos = bodies[i].Position;
                sb.Append($"new Vector3({FloatLit(pos[0])}, {FloatLit(pos[1])}, {FloatLit(pos[2])})");
            }
            sb.AppendLine(" };");

            sb.Append("    private static readonly float[] _initialMasses = new float[] { ");
            for (int i = 0; i < bodies.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(FloatLit(bodies[i].Mass));
            }
            sb.AppendLine(" };");

            sb.Append("    private static readonly Vector3[] _initialVelocities = new Vector3[] { ");
            for (int i = 0; i < bodies.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var v = bodies[i].Velocity;
                if (v != null && v.Length == 3)
                    sb.Append($"new Vector3({FloatLit(v[0])}, {FloatLit(v[1])}, {FloatLit(v[2])})");
                else
                    sb.Append("Vector3.zero");
            }
            sb.AppendLine(" };");
            sb.AppendLine();

            // Octree node
            sb.AppendLine("    // Barnes-Hut octree node");
            sb.AppendLine("    private class OctNode");
            sb.AppendLine("    {");
            sb.AppendLine("        public Vector3 center;   // region center");
            sb.AppendLine("        public float   size;     // region edge length");
            sb.AppendLine("        public Vector3 com;      // center of mass");
            sb.AppendLine("        public float   mass;     // total mass");
            sb.AppendLine("        public int     bodyIndex; // -1 if internal, else single body index");
            sb.AppendLine("        public OctNode[] children; // null if leaf");
            sb.AppendLine("        public bool    isLeaf => children == null;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Awake: instantiate bodies
            sb.AppendLine("    void Awake()");
            sb.AppendLine("    {");
            sb.AppendLine("        int n = _initialPositions.Length;");
            sb.AppendLine("        _bodies = new NBody[n];");
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            GameObject go;");
            sb.AppendLine("            if (bodyPrefab != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                go = Instantiate(bodyPrefab, transform);");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);");
            sb.AppendLine("                go.transform.SetParent(transform, false);");
            sb.AppendLine("            }");
            sb.AppendLine("            go.name = \"Body_\" + i;");
            sb.AppendLine("            Vector3 pos = transform.position + _initialPositions[i];");
            sb.AppendLine("            go.transform.position = pos;");
            sb.AppendLine("            // Size by mass (cube root scaling)");
            sb.AppendLine("            float scale = Mathf.Pow(Mathf.Max(0.0001f, _initialMasses[i]), 1f / 3f);");
            sb.AppendLine("            go.transform.localScale = Vector3.one * scale;");
            sb.AppendLine();
            sb.AppendLine("            _bodies[i].transform = go.transform;");
            sb.AppendLine("            _bodies[i].mass = _initialMasses[i];");
            sb.AppendLine("            _bodies[i].position = pos;");
            sb.AppendLine("            _bodies[i].velocity = _initialVelocities[i];");
            sb.AppendLine("            _bodies[i].previousPosition = pos - _initialVelocities[i] * timeStep;");
            sb.AppendLine("            _bodies[i].acceleration = Vector3.zero;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Update
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_bodies == null || _bodies.Length == 0) return;");
            sb.AppendLine();
            sb.AppendLine("        OctNode root = BuildOctree();");
            sb.AppendLine();
            sb.AppendLine("        // Compute accelerations via Barnes-Hut traversal");
            sb.AppendLine("        for (int i = 0; i < _bodies.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            _bodies[i].acceleration = ComputeAcceleration(root, i);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Integrate");
            sb.AppendLine("        switch (integrator)");
            sb.AppendLine("        {");
            sb.AppendLine("            case \"verlet\":   IntegrateVerlet();   break;");
            sb.AppendLine("            case \"rk4\":      IntegrateRK4();      break;");
            sb.AppendLine("            case \"leapfrog\":");
            sb.AppendLine("            default:          IntegrateLeapfrog(); break;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Push to transforms");
            sb.AppendLine("        for (int i = 0; i < _bodies.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_bodies[i].transform != null)");
            sb.AppendLine("                _bodies[i].transform.position = _bodies[i].position;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Octree build
            sb.AppendLine("    OctNode BuildOctree()");
            sb.AppendLine("    {");
            sb.AppendLine("        // Compute bounding cube");
            sb.AppendLine("        Vector3 min = _bodies[0].position, max = _bodies[0].position;");
            sb.AppendLine("        for (int i = 1; i < _bodies.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            min = Vector3.Min(min, _bodies[i].position);");
            sb.AppendLine("            max = Vector3.Max(max, _bodies[i].position);");
            sb.AppendLine("        }");
            sb.AppendLine("        Vector3 center = (min + max) * 0.5f;");
            sb.AppendLine("        Vector3 extents = max - min;");
            sb.AppendLine("        float size = Mathf.Max(Mathf.Max(extents.x, extents.y), extents.z) + 1e-4f;");
            sb.AppendLine();
            sb.AppendLine("        var root = new OctNode { center = center, size = size, bodyIndex = -1 };");
            sb.AppendLine("        for (int i = 0; i < _bodies.Length; i++)");
            sb.AppendLine("            InsertBody(root, i, 0);");
            sb.AppendLine("        ComputeMassDistribution(root);");
            sb.AppendLine("        return root;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void InsertBody(OctNode node, int bodyIdx, int depth)");
            sb.AppendLine("    {");
            sb.AppendLine("        const int MaxDepth = 24;");
            sb.AppendLine("        if (node.isLeaf)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (node.bodyIndex == -1 && node.mass == 0f)");
            sb.AppendLine("            {");
            sb.AppendLine("                node.bodyIndex = bodyIdx;");
            sb.AppendLine("                node.mass = _bodies[bodyIdx].mass;");
            sb.AppendLine("                node.com = _bodies[bodyIdx].position;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            // Subdivide: push existing body down, then insert new");
            sb.AppendLine("            if (depth >= MaxDepth) { /* coincident; accumulate */ return; }");
            sb.AppendLine("            int existing = node.bodyIndex;");
            sb.AppendLine("            node.children = new OctNode[8];");
            sb.AppendLine("            node.bodyIndex = -1;");
            sb.AppendLine("            if (existing >= 0) InsertBody(node, existing, depth);");
            sb.AppendLine("        }");
            sb.AppendLine("        // Internal node");
            sb.AppendLine("        int octant = GetOctant(node.center, _bodies[bodyIdx].position);");
            sb.AppendLine("        if (node.children[octant] == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            float half = node.size * 0.5f;");
            sb.AppendLine("            Vector3 childCenter = node.center + OctantOffset(octant) * (half * 0.5f);");
            sb.AppendLine("            node.children[octant] = new OctNode { center = childCenter, size = half, bodyIndex = -1 };");
            sb.AppendLine("        }");
            sb.AppendLine("        InsertBody(node.children[octant], bodyIdx, depth + 1);");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    static int GetOctant(Vector3 center, Vector3 p)");
            sb.AppendLine("    {");
            sb.AppendLine("        int o = 0;");
            sb.AppendLine("        if (p.x >= center.x) o |= 1;");
            sb.AppendLine("        if (p.y >= center.y) o |= 2;");
            sb.AppendLine("        if (p.z >= center.z) o |= 4;");
            sb.AppendLine("        return o;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    static Vector3 OctantOffset(int o)");
            sb.AppendLine("    {");
            sb.AppendLine("        return new Vector3(");
            sb.AppendLine("            ((o & 1) != 0) ? 1f : -1f,");
            sb.AppendLine("            ((o & 2) != 0) ? 1f : -1f,");
            sb.AppendLine("            ((o & 4) != 0) ? 1f : -1f);");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void ComputeMassDistribution(OctNode node)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (node.isLeaf) return;");
            sb.AppendLine("        float totalMass = 0f;");
            sb.AppendLine("        Vector3 weighted = Vector3.zero;");
            sb.AppendLine("        for (int i = 0; i < 8; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var c = node.children[i];");
            sb.AppendLine("            if (c == null) continue;");
            sb.AppendLine("            ComputeMassDistribution(c);");
            sb.AppendLine("            totalMass += c.mass;");
            sb.AppendLine("            weighted += c.com * c.mass;");
            sb.AppendLine("        }");
            sb.AppendLine("        node.mass = totalMass;");
            sb.AppendLine("        node.com = totalMass > 0f ? weighted / totalMass : node.center;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    Vector3 ComputeAcceleration(OctNode node, int bodyIdx)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (node == null || node.mass <= 0f) return Vector3.zero;");
            sb.AppendLine("        if (node.isLeaf)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (node.bodyIndex == bodyIdx) return Vector3.zero;");
            sb.AppendLine("            return PairAccel(_bodies[bodyIdx].position, node.com, node.mass);");
            sb.AppendLine("        }");
            sb.AppendLine("        Vector3 delta = node.com - _bodies[bodyIdx].position;");
            sb.AppendLine("        float dist = delta.magnitude;");
            sb.AppendLine("        if (dist > 0f && (node.size / dist) < theta)");
            sb.AppendLine("        {");
            sb.AppendLine("            return PairAccel(_bodies[bodyIdx].position, node.com, node.mass);");
            sb.AppendLine("        }");
            sb.AppendLine("        Vector3 acc = Vector3.zero;");
            sb.AppendLine("        for (int i = 0; i < 8; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (node.children[i] != null)");
            sb.AppendLine("                acc += ComputeAcceleration(node.children[i], bodyIdx);");
            sb.AppendLine("        }");
            sb.AppendLine("        return acc;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    Vector3 PairAccel(Vector3 from, Vector3 to, float mass)");
            sb.AppendLine("    {");
            sb.AppendLine("        Vector3 r = to - from;");
            sb.AppendLine("        float distSq = r.sqrMagnitude + softening * softening;");
            sb.AppendLine("        float inv = 1f / Mathf.Sqrt(distSq);");
            sb.AppendLine("        float inv3 = inv * inv * inv;");
            sb.AppendLine("        return gravitationalConstant * mass * r * inv3;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Integrators
            sb.AppendLine("    void IntegrateLeapfrog()");
            sb.AppendLine("    {");
            sb.AppendLine("        // Kick-drift: v += a*dt; p += v*dt");
            sb.AppendLine("        for (int i = 0; i < _bodies.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            _bodies[i].velocity += _bodies[i].acceleration * timeStep;");
            sb.AppendLine("            _bodies[i].position += _bodies[i].velocity * timeStep;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void IntegrateVerlet()");
            sb.AppendLine("    {");
            sb.AppendLine("        // p(t+dt) = 2*p(t) - p(t-dt) + a*dt^2");
            sb.AppendLine("        float dt2 = timeStep * timeStep;");
            sb.AppendLine("        for (int i = 0; i < _bodies.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            Vector3 curr = _bodies[i].position;");
            sb.AppendLine("            Vector3 next = 2f * curr - _bodies[i].previousPosition + _bodies[i].acceleration * dt2;");
            sb.AppendLine("            _bodies[i].previousPosition = curr;");
            sb.AppendLine("            _bodies[i].position = next;");
            sb.AppendLine("            _bodies[i].velocity = (next - curr) / timeStep;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void IntegrateRK4()");
            sb.AppendLine("    {");
            sb.AppendLine("        // Classic 4-stage RK4 on (position, velocity). Accel treated as f(p) from current tree.");
            sb.AppendLine("        int n = _bodies.Length;");
            sb.AppendLine("        var p0 = new Vector3[n];");
            sb.AppendLine("        var v0 = new Vector3[n];");
            sb.AppendLine("        var k1p = new Vector3[n]; var k1v = new Vector3[n];");
            sb.AppendLine("        var k2p = new Vector3[n]; var k2v = new Vector3[n];");
            sb.AppendLine("        var k3p = new Vector3[n]; var k3v = new Vector3[n];");
            sb.AppendLine("        var k4p = new Vector3[n]; var k4v = new Vector3[n];");
            sb.AppendLine();
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            p0[i] = _bodies[i].position;");
            sb.AppendLine("            v0[i] = _bodies[i].velocity;");
            sb.AppendLine("            k1p[i] = v0[i];");
            sb.AppendLine("            k1v[i] = _bodies[i].acceleration;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // k2");
            sb.AppendLine("        for (int i = 0; i < n; i++) _bodies[i].position = p0[i] + k1p[i] * (timeStep * 0.5f);");
            sb.AppendLine("        var tree2 = BuildOctree();");
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            k2v[i] = ComputeAcceleration(tree2, i);");
            sb.AppendLine("            k2p[i] = v0[i] + k1v[i] * (timeStep * 0.5f);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // k3");
            sb.AppendLine("        for (int i = 0; i < n; i++) _bodies[i].position = p0[i] + k2p[i] * (timeStep * 0.5f);");
            sb.AppendLine("        var tree3 = BuildOctree();");
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            k3v[i] = ComputeAcceleration(tree3, i);");
            sb.AppendLine("            k3p[i] = v0[i] + k2v[i] * (timeStep * 0.5f);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // k4");
            sb.AppendLine("        for (int i = 0; i < n; i++) _bodies[i].position = p0[i] + k3p[i] * timeStep;");
            sb.AppendLine("        var tree4 = BuildOctree();");
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            k4v[i] = ComputeAcceleration(tree4, i);");
            sb.AppendLine("            k4p[i] = v0[i] + k3v[i] * timeStep;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Combine");
            sb.AppendLine("        float sixth = timeStep / 6f;");
            sb.AppendLine("        for (int i = 0; i < n; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            _bodies[i].position = p0[i] + sixth * (k1p[i] + 2f * k2p[i] + 2f * k3p[i] + k4p[i]);");
            sb.AppendLine("            _bodies[i].velocity = v0[i] + sixth * (k1v[i] + 2f * k2v[i] + 2f * k3v[i] + k4v[i]);");
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
            return f.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        private static string EscapeString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
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
