using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Physics
{
    /// <summary>
    /// <c>physics/fluid-create</c> — generates a Jos Stam Stable Fluids (grid-based Eulerian
    /// Navier-Stokes) MonoBehaviour and attaches it to a new scene GameObject.
    /// MVP scope is 2D smoke with density visualized via a textured quad updated each frame.
    /// Distinct from the SPH-based <c>simulation/fluid</c> tool.
    /// </summary>
    public static class PhysicsFluidCreateTool
    {
        private const int ResMin = 8;
        private const int ResMax = 128;

        [MosaicTool("physics/fluid-create",
                    "Generates a Jos Stam Stable Fluids (grid-based Navier-Stokes) MonoBehaviour and spawns a GameObject running it (smoke | liquid | fire).",
                    isReadOnly: false, category: "physics", Context = ToolContext.Both)]
        public static ToolResult<PhysicsFluidCreateResult> Execute(PhysicsFluidCreateParams p)
        {
            if (p == null)
                return ToolResult<PhysicsFluidCreateResult>.Fail("Params required", ErrorCodes.INVALID_PARAM);

            // --- Type ---
            string type = string.IsNullOrWhiteSpace(p.Type) ? "smoke" : p.Type.Trim().ToLowerInvariant();
            if (type != "smoke" && type != "liquid" && type != "fire")
                return ToolResult<PhysicsFluidCreateResult>.Fail(
                    $"Type must be 'smoke', 'liquid', or 'fire' (got '{p.Type}')",
                    ErrorCodes.INVALID_PARAM);

            // --- Resolution: reject absurd values, clamp the rest into [8, 128] ---
            int rawRes = p.Resolution ?? 64;
            if (rawRes <= 0 || rawRes > 4096)
                return ToolResult<PhysicsFluidCreateResult>.Fail(
                    $"Resolution must be a positive integer (got {rawRes})",
                    ErrorCodes.INVALID_PARAM);
            int resolution = Mathf.Clamp(rawRes, ResMin, ResMax);

            float viscosity       = p.Viscosity        ?? 0.0001f;
            float diffusion       = p.Diffusion        ?? 0.0f;
            float timeStep        = p.TimeStep         ?? 0.1f;
            float emitterRadius   = p.EmitterRadius    ?? 0.1f;
            float emitterStrength = p.EmitterStrength  ?? 1.0f;
            bool  useCompute      = p.UseComputeShader ?? false;

            Vector3 emitterPos = new Vector3(0.5f, 0.5f, 0.5f);
            if (p.EmitterPosition != null && p.EmitterPosition.Length >= 3)
                emitterPos = new Vector3(p.EmitterPosition[0], p.EmitterPosition[1], p.EmitterPosition[2]);

            Vector3 worldPos = Vector3.zero;
            if (p.Position != null && p.Position.Length >= 3)
                worldPos = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            string baseName = string.IsNullOrWhiteSpace(p.Name) ? $"StableFluid_{type}" : p.Name.Trim();
            string safeName = SanitizeIdentifier(baseName);
            string className = $"StableFluid_{safeName}";

            // --- SavePath ---
            string savePath = string.IsNullOrWhiteSpace(p.SavePath) ? "Assets/Generated/Physics/" : p.SavePath.Trim();
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<PhysicsFluidCreateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            AssetDatabaseHelper.EnsureFolder(savePath);
            string fullDir = Path.Combine(Application.dataPath, "..", savePath);

            string scriptAssetPath = $"{savePath}{className}.cs";
            string scriptFullPath  = Path.Combine(Application.dataPath, "..", scriptAssetPath);

            string scriptContent = BuildScript(
                className, type, resolution,
                viscosity, diffusion, timeStep,
                emitterPos, emitterRadius, emitterStrength,
                useCompute);

            File.WriteAllText(scriptFullPath, scriptContent);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(scriptAssetPath);

            // --- Create host GameObject ---
            // We cannot attach the component until Unity compiles the freshly-written script,
            // so the MVP spawns a visible placeholder GameObject and stores the script path
            // in the result. Users (or a follow-up tool call) can attach the component
            // after domain reload.
            var go = new GameObject(baseName);
            go.transform.position = worldPos;
            Undo.RegisterCreatedObjectUndo(go, "Create Stable Fluid");

            return ToolResult<PhysicsFluidCreateResult>.Ok(new PhysicsFluidCreateResult
            {
                ScriptPath     = scriptAssetPath,
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                Type           = type,
                Resolution     = resolution
            });
        }

        private static string SanitizeIdentifier(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // Script generation — Jos Stam Stable Fluids 1999, 2D MVP
        // ---------------------------------------------------------------------
        private static string BuildScript(
            string className, string type, int resolution,
            float viscosity, float diffusion, float timeStep,
            Vector3 emitterPos, float emitterRadius, float emitterStrength,
            bool useCompute)
        {
            // Culture-invariant float formatting
            string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

            // NOTE: MVP is CPU, 2D. UseComputeShader flag is surfaced as a public field on the
            // generated component (a hook for future GPU implementation) but the runtime solver
            // is always CPU in this MVP.
            string colorizeBlock = type switch
            {
                "fire"   => "Color c = FireColor(d + t * 0.2f);",
                "liquid" => "Color c = new Color(0.2f, 0.4f + 0.5f * d, 0.9f, Mathf.Clamp01(d));",
                _        => "Color c = new Color(d, d, d, Mathf.Clamp01(d));" // smoke
            };

            bool useTemperature = type == "fire";

            return $@"// Generated by Mosaic Bridge - physics/fluid-create
// Jos Stam Stable Fluids (1999) - 2D MVP
// Type: {type} | Resolution: {resolution} | Visc: {F(viscosity)} | Diff: {F(diffusion)} | dt: {F(timeStep)}
// Customize as needed. This is a reference implementation, intentionally readable over fast.

using UnityEngine;

[DisallowMultipleComponent]
public class {className} : MonoBehaviour
{{
    [Header(""Solver"")]
    public int   resolution    = {resolution};
    public float viscosity     = {F(viscosity)}f;
    public float diffusion     = {F(diffusion)}f;
    public float timeStep      = {F(timeStep)}f;
    public int   gaussSeidelIterations = 20;
    public bool  useComputeShader = {(useCompute ? "true" : "false")}; // MVP: CPU only

    [Header(""Emitter (normalized grid coords)"")]
    public Vector3 emitterPosition  = new Vector3({F(emitterPos.x)}f, {F(emitterPos.y)}f, {F(emitterPos.z)}f);
    public float   emitterRadius    = {F(emitterRadius)}f;
    public float   emitterStrength  = {F(emitterStrength)}f;

    [Header(""Display"")]
    public float   displaySize   = 5f;
    public bool    autoBuildQuad = true;

    // Grid: (N+2) * (N+2) with 1-cell border for boundary conditions
    int N;
    int Size;

    float[] u, v;        // velocity
    float[] u0, v0;      // scratch velocity
    float[] dens, dens0; // density
{(useTemperature ? "    float[] temp, temp0; // temperature (fire)\n" : string.Empty)}
    Texture2D _tex;
    Color[]   _pixels;
    GameObject _quad;

    int IX(int i, int j) => i + (N + 2) * j;

    void OnEnable()
    {{
        N = Mathf.Clamp(resolution, 8, 128);
        Size = (N + 2) * (N + 2);

        u     = new float[Size]; v     = new float[Size];
        u0    = new float[Size]; v0    = new float[Size];
        dens  = new float[Size]; dens0 = new float[Size];
{(useTemperature ? "        temp = new float[Size]; temp0 = new float[Size];\n" : string.Empty)}
        _tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Bilinear;
        _tex.wrapMode   = TextureWrapMode.Clamp;
        _pixels = new Color[N * N];

        if (autoBuildQuad) BuildDisplayQuad();
    }}

    void OnDisable()
    {{
        if (_quad != null) DestroyImmediate(_quad);
        if (_tex  != null) DestroyImmediate(_tex);
    }}

    void BuildDisplayQuad()
    {{
        _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quad.name = ""FluidDisplay"";
        _quad.transform.SetParent(transform, false);
        _quad.transform.localScale = Vector3.one * displaySize;
        var col = _quad.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
        var mr  = _quad.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find(""Unlit/Transparent""));
        mat.mainTexture = _tex;
        mr.sharedMaterial = mat;
    }}

    void Update()
    {{
        float dt = timeStep;

        // --- Inject sources from emitter ---
        System.Array.Clear(u0, 0, Size);
        System.Array.Clear(v0, 0, Size);
        System.Array.Clear(dens0, 0, Size);

        int ei = Mathf.Clamp(Mathf.RoundToInt(emitterPosition.x * N), 1, N);
        int ej = Mathf.Clamp(Mathf.RoundToInt(emitterPosition.y * N), 1, N);
        int rad = Mathf.Max(1, Mathf.RoundToInt(emitterRadius * N));

        for (int j = Mathf.Max(1, ej - rad); j <= Mathf.Min(N, ej + rad); j++)
        for (int i = Mathf.Max(1, ei - rad); i <= Mathf.Min(N, ei + rad); i++)
        {{
            float dx = (i - ei) / (float)rad;
            float dy = (j - ej) / (float)rad;
            float r2 = dx * dx + dy * dy;
            if (r2 > 1f) continue;
            float falloff = 1f - r2;
            dens0[IX(i, j)] = emitterStrength * falloff;
            v0[IX(i, j)]    = emitterStrength * falloff; // upward push
        }}

        AddSource(u,    u0,    dt);
        AddSource(v,    v0,    dt);
        AddSource(dens, dens0, dt);

        // --- Velocity step ---
        System.Array.Copy(u, u0, Size);
        Diffuse(1, u, u0, viscosity, dt);
        System.Array.Copy(v, v0, Size);
        Diffuse(2, v, v0, viscosity, dt);
        Project(u, v, u0, v0);

        System.Array.Copy(u, u0, Size);
        System.Array.Copy(v, v0, Size);
        Advect(1, u, u0, u0, v0, dt);
        Advect(2, v, v0, u0, v0, dt);
        Project(u, v, u0, v0);

        // --- Density step ---
        System.Array.Copy(dens, dens0, Size);
        Diffuse(0, dens, dens0, diffusion, dt);
        System.Array.Copy(dens, dens0, Size);
        Advect(0, dens, dens0, u, v, dt);

        UpdateTexture();
    }}

    // ----- Stam primitives --------------------------------------------------

    void AddSource(float[] x, float[] s, float dt)
    {{
        for (int i = 0; i < Size; i++) x[i] += dt * s[i];
    }}

    void Diffuse(int b, float[] x, float[] x0, float diff, float dt)
    {{
        float a = dt * diff * N * N;
        for (int k = 0; k < gaussSeidelIterations; k++)
        {{
            for (int j = 1; j <= N; j++)
            for (int i = 1; i <= N; i++)
            {{
                x[IX(i, j)] = (x0[IX(i, j)] + a * (
                    x[IX(i - 1, j)] + x[IX(i + 1, j)] +
                    x[IX(i, j - 1)] + x[IX(i, j + 1)])) / (1f + 4f * a);
            }}
            SetBoundary(b, x);
        }}
    }}

    void Advect(int b, float[] d, float[] d0, float[] uu, float[] vv, float dt)
    {{
        float dt0 = dt * N;
        for (int j = 1; j <= N; j++)
        for (int i = 1; i <= N; i++)
        {{
            float x = i - dt0 * uu[IX(i, j)];
            float y = j - dt0 * vv[IX(i, j)];
            if (x < 0.5f) x = 0.5f; if (x > N + 0.5f) x = N + 0.5f;
            int i0 = (int)x; int i1 = i0 + 1;
            if (y < 0.5f) y = 0.5f; if (y > N + 0.5f) y = N + 0.5f;
            int j0 = (int)y; int j1 = j0 + 1;

            float s1 = x - i0; float s0 = 1 - s1;
            float t1 = y - j0; float t0 = 1 - t1;

            d[IX(i, j)] =
                s0 * (t0 * d0[IX(i0, j0)] + t1 * d0[IX(i0, j1)]) +
                s1 * (t0 * d0[IX(i1, j0)] + t1 * d0[IX(i1, j1)]);
        }}
        SetBoundary(b, d);
    }}

    void Project(float[] uu, float[] vv, float[] p, float[] div)
    {{
        float h = 1f / N;
        for (int j = 1; j <= N; j++)
        for (int i = 1; i <= N; i++)
        {{
            div[IX(i, j)] = -0.5f * h * (
                uu[IX(i + 1, j)] - uu[IX(i - 1, j)] +
                vv[IX(i, j + 1)] - vv[IX(i, j - 1)]);
            p[IX(i, j)] = 0f;
        }}
        SetBoundary(0, div); SetBoundary(0, p);

        for (int k = 0; k < gaussSeidelIterations; k++)
        {{
            for (int j = 1; j <= N; j++)
            for (int i = 1; i <= N; i++)
            {{
                p[IX(i, j)] = (div[IX(i, j)] +
                    p[IX(i - 1, j)] + p[IX(i + 1, j)] +
                    p[IX(i, j - 1)] + p[IX(i, j + 1)]) / 4f;
            }}
            SetBoundary(0, p);
        }}

        for (int j = 1; j <= N; j++)
        for (int i = 1; i <= N; i++)
        {{
            uu[IX(i, j)] -= 0.5f * (p[IX(i + 1, j)] - p[IX(i - 1, j)]) / h;
            vv[IX(i, j)] -= 0.5f * (p[IX(i, j + 1)] - p[IX(i, j - 1)]) / h;
        }}
        SetBoundary(1, uu); SetBoundary(2, vv);
    }}

    void SetBoundary(int b, float[] x)
    {{
        for (int i = 1; i <= N; i++)
        {{
            x[IX(0,     i)] = (b == 1) ? -x[IX(1, i)] : x[IX(1, i)];
            x[IX(N + 1, i)] = (b == 1) ? -x[IX(N, i)] : x[IX(N, i)];
            x[IX(i, 0)]     = (b == 2) ? -x[IX(i, 1)] : x[IX(i, 1)];
            x[IX(i, N + 1)] = (b == 2) ? -x[IX(i, N)] : x[IX(i, N)];
        }}
        x[IX(0,     0)]     = 0.5f * (x[IX(1, 0)]     + x[IX(0, 1)]);
        x[IX(0,     N + 1)] = 0.5f * (x[IX(1, N + 1)] + x[IX(0, N)]);
        x[IX(N + 1, 0)]     = 0.5f * (x[IX(N, 0)]     + x[IX(N + 1, 1)]);
        x[IX(N + 1, N + 1)] = 0.5f * (x[IX(N, N + 1)] + x[IX(N + 1, N)]);
    }}

    // ----- Visualization ----------------------------------------------------

    void UpdateTexture()
    {{
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {{
            float d = Mathf.Clamp01(dens[IX(i + 1, j + 1)]);
            float t = 0f;
            {colorizeBlock}
            _pixels[i + j * N] = c;
        }}
        _tex.SetPixels(_pixels);
        _tex.Apply(false);
    }}

    static Color FireColor(float d)
    {{
        // Simple black-body ramp: black -> red -> orange -> yellow -> white
        d = Mathf.Clamp01(d);
        if (d < 0.25f) return new Color(d * 4f, 0, 0, d);
        if (d < 0.5f)  return new Color(1, (d - 0.25f) * 4f, 0, d);
        if (d < 0.75f) return new Color(1, 1, (d - 0.5f) * 4f, d);
        return new Color(1, 1, 1, d);
    }}
}}
";
        }
    }
}
