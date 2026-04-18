using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    /// <summary>
    /// Generates a MonoBehaviour implementing a Hooke's Law spring-mass solver
    /// ("jelly physics"). Builds particles from either a source mesh (surface topology)
    /// or a synthesized axis-aligned lattice, emits springs from mesh edges or
    /// 6-connected neighbours, and deforms the attached mesh each frame.
    /// </summary>
    public static class PhysicsSpringMassTool
    {
        static readonly HashSet<string> ValidPresets = new HashSet<string>
        {
            "jelly", "cloth", "bounce", "hair"
        };

        static readonly HashSet<string> ValidTopologies = new HashSet<string>
        {
            "surface", "tetrahedral", "lattice"
        };

        [MosaicTool("physics/spring-mass",
                    "Generates a spring-mass MonoBehaviour (jelly/cloth/bounce/hair) with Hooke's Law integration and optional breakable springs",
                    isReadOnly: false, category: "physics", Context = ToolContext.Both)]
        public static ToolResult<PhysicsSpringMassResult> Execute(PhysicsSpringMassParams p)
        {
            if (p == null)
                return ToolResult<PhysicsSpringMassResult>.Fail(
                    "Parameters are required", ErrorCodes.INVALID_PARAM);

            // --- Validate preset ---
            string preset = string.IsNullOrEmpty(p.Preset) ? "jelly" : p.Preset.ToLowerInvariant();
            if (!ValidPresets.Contains(preset))
                return ToolResult<PhysicsSpringMassResult>.Fail(
                    $"Invalid preset '{p.Preset}'. Valid: {string.Join(", ", ValidPresets)}",
                    ErrorCodes.INVALID_PARAM);

            // --- Validate topology ---
            string topology = string.IsNullOrEmpty(p.Topology) ? "surface" : p.Topology.ToLowerInvariant();
            if (!ValidTopologies.Contains(topology))
                return ToolResult<PhysicsSpringMassResult>.Fail(
                    $"Invalid topology '{p.Topology}'. Valid: {string.Join(", ", ValidTopologies)}",
                    ErrorCodes.INVALID_PARAM);

            // --- Resolve source mesh for surface topology ---
            Mesh sourceMesh = null;
            GameObject sourceGO = null;
            string sourceName = null;

            bool needsMesh = (topology == "surface" || topology == "tetrahedral");

            if (!string.IsNullOrEmpty(p.MeshPath))
            {
                sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p.MeshPath);
                if (sourceMesh == null)
                {
                    var all = AssetDatabase.LoadAllAssetsAtPath(p.MeshPath);
                    sourceMesh = all?.OfType<Mesh>().FirstOrDefault();
                }
                if (sourceMesh == null)
                    return ToolResult<PhysicsSpringMassResult>.Fail(
                        $"Mesh not found at '{p.MeshPath}'", ErrorCodes.NOT_FOUND);
                sourceName = Path.GetFileNameWithoutExtension(p.MeshPath);
            }
            else if (!string.IsNullOrEmpty(p.GameObjectName))
            {
                sourceGO = GameObject.Find(p.GameObjectName);
                if (sourceGO == null)
                    return ToolResult<PhysicsSpringMassResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);
                var mf = sourceGO.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    if (needsMesh)
                        return ToolResult<PhysicsSpringMassResult>.Fail(
                            $"GameObject '{p.GameObjectName}' has no MeshFilter or mesh", ErrorCodes.INVALID_PARAM);
                }
                else
                {
                    sourceMesh = mf.sharedMesh;
                }
                sourceName = sourceGO.name;
            }
            else if (needsMesh)
            {
                return ToolResult<PhysicsSpringMassResult>.Fail(
                    "Either MeshPath or GameObjectName is required for surface/tetrahedral topology",
                    ErrorCodes.INVALID_PARAM);
            }

            // --- Apply preset defaults where caller left values at defaults ---
            float stiffness = p.SpringStiffness;
            float damping   = p.Damping;
            float mass      = p.Mass;
            ApplyPresetDefaults(preset, ref stiffness, ref damping, ref mass);

            // --- Resolve output path ---
            string savePath = string.IsNullOrEmpty(p.SavePath)
                ? "Assets/Generated/Physics/"
                : p.SavePath;

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<PhysicsSpringMassResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!savePath.EndsWith("/")) savePath += "/";

            // --- Resolve system name / class name ---
            string rawName = !string.IsNullOrEmpty(p.Name)
                ? p.Name
                : (!string.IsNullOrEmpty(sourceName) ? sourceName : preset);
            string safeName = SanitizeIdentifier(rawName);
            string className = "SpringMassSystem_" + safeName;
            string scriptFileName = className + ".cs";
            string scriptAssetPath = savePath + scriptFileName;

            // --- Pre-compute particle/spring counts for reporting ---
            int particleCount = 0;
            int springCount = 0;
            ComputeTopologyCounts(topology, sourceMesh, out particleCount, out springCount);

            // --- Emit generated script ---
            string scriptContent = BuildScript(
                className:   className,
                preset:      preset,
                stiffness:   stiffness,
                damping:     damping,
                mass:        mass,
                breakForce:  Mathf.Max(0f, p.BreakForce),
                topology:    topology);

            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string fullPath = Path.Combine(projectRoot, scriptAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, scriptContent, Encoding.UTF8);

            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(scriptAssetPath);

            // --- Optionally attach to existing GameObject (best effort: type may not exist
            //     until next domain reload; we still record the GO we targeted) ---
            string goName = sourceGO != null ? sourceGO.name : null;
            int instanceId = 0;

            if (sourceGO != null)
            {
                instanceId = sourceGO.GetInstanceID();
                var scriptType = FindTypeByName(className);
                if (scriptType != null)
                {
                    try
                    {
                        Undo.RegisterCompleteObjectUndo(sourceGO, $"Add {className}");
                        sourceGO.AddComponent(scriptType);
                    }
                    catch (Exception)
                    {
                        // Component attach is best-effort before domain reload
                    }
                }

                if (p.Position != null && p.Position.Length >= 3)
                {
                    Undo.RecordObject(sourceGO.transform, "Position SpringMass GO");
                    sourceGO.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
                }
            }

            return ToolResult<PhysicsSpringMassResult>.Ok(new PhysicsSpringMassResult
            {
                ScriptPath     = scriptAssetPath,
                GameObjectName = goName,
                InstanceId     = instanceId,
                Preset         = preset,
                ParticleCount  = particleCount,
                SpringCount    = springCount
            });
        }

        // ----------------------------------------------------------------------
        // Preset defaults
        // ----------------------------------------------------------------------

        static void ApplyPresetDefaults(string preset, ref float stiffness, ref float damping, ref float mass)
        {
            // Only override values that are still at their class defaults.
            const float defaultStiffness = 1000f;
            const float defaultDamping   = 5f;
            const float defaultMass      = 1f;

            switch (preset)
            {
                case "jelly":
                    if (Mathf.Approximately(stiffness, defaultStiffness)) stiffness = 500f;
                    if (Mathf.Approximately(damping,   defaultDamping))   damping   = 8f;
                    if (Mathf.Approximately(mass,      defaultMass))      mass      = 1.0f;
                    break;
                case "cloth":
                    if (Mathf.Approximately(stiffness, defaultStiffness)) stiffness = 800f;
                    if (Mathf.Approximately(damping,   defaultDamping))   damping   = 3f;
                    if (Mathf.Approximately(mass,      defaultMass))      mass      = 0.2f;
                    break;
                case "bounce":
                    if (Mathf.Approximately(stiffness, defaultStiffness)) stiffness = 2000f;
                    if (Mathf.Approximately(damping,   defaultDamping))   damping   = 2f;
                    if (Mathf.Approximately(mass,      defaultMass))      mass      = 0.5f;
                    break;
                case "hair":
                    if (Mathf.Approximately(stiffness, defaultStiffness)) stiffness = 400f;
                    if (Mathf.Approximately(damping,   defaultDamping))   damping   = 4f;
                    if (Mathf.Approximately(mass,      defaultMass))      mass      = 0.1f;
                    break;
            }
        }

        // ----------------------------------------------------------------------
        // Topology analysis (for reporting; runtime reconstructs at Start)
        // ----------------------------------------------------------------------

        static void ComputeTopologyCounts(string topology, Mesh mesh, out int particleCount, out int springCount)
        {
            particleCount = 0;
            springCount   = 0;

            if (topology == "surface" && mesh != null)
            {
                particleCount = mesh.vertexCount;
                springCount   = CountUniqueEdges(mesh);
            }
            else if (topology == "tetrahedral" && mesh != null)
            {
                particleCount = mesh.vertexCount;
                // Edges + approx face-diagonals (upper bound ~2x edges)
                int edges = CountUniqueEdges(mesh);
                springCount = edges + (mesh.triangles.Length / 3);
            }
            else if (topology == "lattice")
            {
                // Default synthesized lattice is 4x4x4
                int n = 4;
                particleCount = n * n * n;
                // 6-connected neighbours: count edges in 3D grid
                springCount = 3 * n * n * (n - 1);
            }
        }

        static int CountUniqueEdges(Mesh mesh)
        {
            var tris = mesh.triangles;
            var set = new HashSet<long>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                set.Add(EdgeKey(a, b));
                set.Add(EdgeKey(b, c));
                set.Add(EdgeKey(c, a));
            }
            return set.Count;
        }

        static long EdgeKey(int a, int b)
        {
            int lo = Math.Min(a, b);
            int hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }

        // ----------------------------------------------------------------------
        // Script template
        // ----------------------------------------------------------------------

        static string BuildScript(string className, string preset, float stiffness,
                                  float damping, float mass, float breakForce, string topology)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Generated by Mosaic Bridge - physics/spring-mass");
            sb.AppendLine("// Hooke's Law spring-mass solver. Customize as needed.");
            sb.AppendLine();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("[RequireComponent(typeof(MeshFilter))]");
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine($"    [Header(\"Spring-Mass ({preset})\")]");
            sb.AppendLine($"    public float springStiffness = {stiffness.ToString(System.Globalization.CultureInfo.InvariantCulture)}f;");
            sb.AppendLine($"    public float damping         = {damping.ToString(System.Globalization.CultureInfo.InvariantCulture)}f;");
            sb.AppendLine($"    public float mass            = {mass.ToString(System.Globalization.CultureInfo.InvariantCulture)}f;");
            sb.AppendLine($"    public float breakForce      = {breakForce.ToString(System.Globalization.CultureInfo.InvariantCulture)}f; // 0 = unbreakable");
            sb.AppendLine($"    public string topology       = \"{topology}\";");
            sb.AppendLine("    public Vector3 gravity        = new Vector3(0f, -9.81f, 0f);");
            sb.AppendLine("    public int latticeSize        = 4;     // used when topology == \"lattice\"");
            sb.AppendLine("    public float latticeSpacing   = 0.25f; // used when topology == \"lattice\"");
            sb.AppendLine();
            sb.AppendLine("    struct Spring { public int a; public int b; public float restLength; }");
            sb.AppendLine();
            sb.AppendLine("    Vector3[] _positions;");
            sb.AppendLine("    Vector3[] _velocities;");
            sb.AppendLine("    Vector3[] _forces;");
            sb.AppendLine("    List<Spring> _springs;");
            sb.AppendLine("    Mesh _mesh;");
            sb.AppendLine("    Vector3[] _baseVertices; // reference vertex positions for mesh deformation");
            sb.AppendLine();
            sb.AppendLine("    void Start()");
            sb.AppendLine("    {");
            sb.AppendLine("        var mf = GetComponent<MeshFilter>();");
            sb.AppendLine("        // Work on an instance mesh so we don't mutate shared assets.");
            sb.AppendLine("        _mesh = mf != null && mf.sharedMesh != null ? Instantiate(mf.sharedMesh) : null;");
            sb.AppendLine("        if (_mesh != null) mf.mesh = _mesh;");
            sb.AppendLine();
            sb.AppendLine("        if (topology == \"lattice\" || _mesh == null)");
            sb.AppendLine("            BuildLattice();");
            sb.AppendLine("        else if (topology == \"tetrahedral\")");
            sb.AppendLine("            BuildTetrahedral();");
            sb.AppendLine("        else");
            sb.AppendLine("            BuildSurface();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    void BuildSurface()");
            sb.AppendLine("    {");
            sb.AppendLine("        var verts = _mesh.vertices;");
            sb.AppendLine("        _baseVertices = (Vector3[])verts.Clone();");
            sb.AppendLine("        _positions  = (Vector3[])verts.Clone();");
            sb.AppendLine("        _velocities = new Vector3[verts.Length];");
            sb.AppendLine("        _forces     = new Vector3[verts.Length];");
            sb.AppendLine("        _springs    = new List<Spring>();");
            sb.AppendLine();
            sb.AppendLine("        var tris = _mesh.triangles;");
            sb.AppendLine("        var seen = new HashSet<long>();");
            sb.AppendLine("        for (int i = 0; i < tris.Length; i += 3)");
            sb.AppendLine("        {");
            sb.AppendLine("            AddSpringEdge(tris[i],     tris[i + 1], seen);");
            sb.AppendLine("            AddSpringEdge(tris[i + 1], tris[i + 2], seen);");
            sb.AppendLine("            AddSpringEdge(tris[i + 2], tris[i],     seen);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    void BuildTetrahedral()");
            sb.AppendLine("    {");
            sb.AppendLine("        BuildSurface();");
            sb.AppendLine("        // Add inner diagonals (a->c of every triangle) for volumetric stiffness.");
            sb.AppendLine("        var tris = _mesh.triangles;");
            sb.AppendLine("        var seen = new HashSet<long>();");
            sb.AppendLine("        foreach (var s in _springs) seen.Add(Key(s.a, s.b));");
            sb.AppendLine("        for (int i = 0; i < tris.Length; i += 3)");
            sb.AppendLine("            AddSpringEdge(tris[i], tris[i + 2], seen);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    void BuildLattice()");
            sb.AppendLine("    {");
            sb.AppendLine("        int n = Mathf.Max(2, latticeSize);");
            sb.AppendLine("        int count = n * n * n;");
            sb.AppendLine("        _positions  = new Vector3[count];");
            sb.AppendLine("        _velocities = new Vector3[count];");
            sb.AppendLine("        _forces     = new Vector3[count];");
            sb.AppendLine("        _baseVertices = new Vector3[count];");
            sb.AppendLine("        _springs    = new List<Spring>();");
            sb.AppendLine();
            sb.AppendLine("        int Idx(int x, int y, int z) => (z * n + y) * n + x;");
            sb.AppendLine();
            sb.AppendLine("        for (int z = 0; z < n; z++)");
            sb.AppendLine("        for (int y = 0; y < n; y++)");
            sb.AppendLine("        for (int x = 0; x < n; x++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var p = new Vector3(x, y, z) * latticeSpacing;");
            sb.AppendLine("            int i = Idx(x, y, z);");
            sb.AppendLine("            _positions[i]   = p;");
            sb.AppendLine("            _baseVertices[i] = p;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        var seen = new HashSet<long>();");
            sb.AppendLine("        for (int z = 0; z < n; z++)");
            sb.AppendLine("        for (int y = 0; y < n; y++)");
            sb.AppendLine("        for (int x = 0; x < n; x++)");
            sb.AppendLine("        {");
            sb.AppendLine("            int a = Idx(x, y, z);");
            sb.AppendLine("            if (x + 1 < n) AddSpringEdge(a, Idx(x + 1, y, z), seen);");
            sb.AppendLine("            if (y + 1 < n) AddSpringEdge(a, Idx(x, y + 1, z), seen);");
            sb.AppendLine("            if (z + 1 < n) AddSpringEdge(a, Idx(x, y, z + 1), seen);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    void AddSpringEdge(int a, int b, HashSet<long> seen)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (a == b) return;");
            sb.AppendLine("        long key = Key(a, b);");
            sb.AppendLine("        if (!seen.Add(key)) return;");
            sb.AppendLine("        _springs.Add(new Spring { a = a, b = b, restLength = Vector3.Distance(_positions[a], _positions[b]) });");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    static long Key(int a, int b)");
            sb.AppendLine("    {");
            sb.AppendLine("        int lo = Mathf.Min(a, b); int hi = Mathf.Max(a, b);");
            sb.AppendLine("        return ((long)lo << 32) | (uint)hi;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_positions == null || _springs == null) return;");
            sb.AppendLine();
            sb.AppendLine("        float dt = Time.deltaTime;");
            sb.AppendLine("        if (dt <= 0f) return;");
            sb.AppendLine("        float invMass = mass > 0f ? 1f / mass : 0f;");
            sb.AppendLine();
            sb.AppendLine("        // Reset forces");
            sb.AppendLine("        for (int i = 0; i < _forces.Length; i++) _forces[i] = Vector3.zero;");
            sb.AppendLine();
            sb.AppendLine("        // Spring forces (Hooke's Law) + damping");
            sb.AppendLine("        List<int> breaking = null;");
            sb.AppendLine("        for (int s = 0; s < _springs.Count; s++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var sp = _springs[s];");
            sb.AppendLine("            Vector3 delta = _positions[sp.b] - _positions[sp.a];");
            sb.AppendLine("            float dist = delta.magnitude;");
            sb.AppendLine("            if (dist < 1e-6f) continue;");
            sb.AppendLine("            Vector3 dir = delta / dist;");
            sb.AppendLine();
            sb.AppendLine("            float displacement = dist - sp.restLength;");
            sb.AppendLine("            Vector3 spring = -springStiffness * displacement * -dir; // pulls together when stretched");
            sb.AppendLine("            Vector3 relVel = _velocities[sp.a] - _velocities[sp.b];");
            sb.AppendLine("            Vector3 damp   = -damping * Vector3.Dot(relVel, dir) * dir;");
            sb.AppendLine("            Vector3 force  = spring + damp;");
            sb.AppendLine();
            sb.AppendLine("            _forces[sp.a] += force;");
            sb.AppendLine("            _forces[sp.b] -= force;");
            sb.AppendLine();
            sb.AppendLine("            if (breakForce > 0f && force.magnitude > breakForce)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (breaking == null) breaking = new List<int>();");
            sb.AppendLine("                breaking.Add(s);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        if (breaking != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int k = breaking.Count - 1; k >= 0; k--)");
            sb.AppendLine("                _springs.RemoveAt(breaking[k]);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Gravity + Euler integration");
            sb.AppendLine("        for (int i = 0; i < _positions.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            _forces[i] += gravity * mass;");
            sb.AppendLine("            _velocities[i] += _forces[i] * invMass * dt;");
            sb.AppendLine("            _positions[i]  += _velocities[i] * dt;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Deform mesh to match particles (surface/tetrahedral only)");
            sb.AppendLine("        if (_mesh != null && topology != \"lattice\")");
            sb.AppendLine("        {");
            sb.AppendLine("            _mesh.vertices = _positions;");
            sb.AppendLine("            _mesh.RecalculateNormals();");
            sb.AppendLine("            _mesh.RecalculateBounds();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ----------------------------------------------------------------------
        // Misc helpers
        // ----------------------------------------------------------------------

        internal static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_') sb.Append(ch);
            }
            var r = sb.ToString();
            if (r.Length == 0 || char.IsDigit(r[0])) r = "_" + r;
            return r;
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
    }
}
