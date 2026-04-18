using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    /// <summary>
    /// Generates Radiance Cascades global-illumination scaffolding
    /// (Alexander Sannikov 2023) as a ScriptableRendererFeature + compute
    /// shader. MVP: valid compile-clean stub with cascade generation,
    /// merge, and final irradiance apply kernels.
    /// </summary>
    public static class RenderingRadianceCascadesTool
    {
        [MosaicTool("rendering/radiance-cascades",
                    "Generates Radiance Cascades GI renderer feature and compute shader (URP/HDRP)",
                    isReadOnly: false, category: "rendering", Context = ToolContext.Both)]
        public static ToolResult<RenderingRadianceCascadesResult> Execute(RenderingRadianceCascadesParams p)
        {
            var pipeline = string.IsNullOrEmpty(p.Pipeline) ? "urp" : p.Pipeline.ToLowerInvariant();
            if (pipeline != "urp" && pipeline != "hdrp")
                return ToolResult<RenderingRadianceCascadesResult>.Fail(
                    "Pipeline must be one of: urp, hdrp", ErrorCodes.INVALID_PARAM);

            var cascadeCount = p.CascadeCount ?? 6;
            if (cascadeCount <= 0 || cascadeCount > 16)
                return ToolResult<RenderingRadianceCascadesResult>.Fail(
                    "CascadeCount must be in range [1, 16]", ErrorCodes.INVALID_PARAM);

            var probeSpacing = p.ProbeSpacing ?? 1.0f;
            if (probeSpacing <= 0f)
                return ToolResult<RenderingRadianceCascadesResult>.Fail(
                    "ProbeSpacing must be > 0", ErrorCodes.INVALID_PARAM);

            var bounceCount = p.BounceCount ?? 2;
            if (bounceCount < 0 || bounceCount > 8)
                return ToolResult<RenderingRadianceCascadesResult>.Fail(
                    "BounceCount must be in range [0, 8]", ErrorCodes.INVALID_PARAM);

            var intensity = p.Intensity ?? 1.0f;

            var resolution = p.Resolution ?? new[] { 1920, 1080 };
            if (resolution.Length != 2)
                return ToolResult<RenderingRadianceCascadesResult>.Fail(
                    "Resolution must have exactly 2 elements (width, height)",
                    ErrorCodes.INVALID_PARAM);
            for (int i = 0; i < 2; i++)
                if (resolution[i] <= 0)
                    return ToolResult<RenderingRadianceCascadesResult>.Fail(
                        "Resolution components must be > 0", ErrorCodes.INVALID_PARAM);

            var name     = string.IsNullOrEmpty(p.OutputName) ? "Default" : p.OutputName;
            var savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Rendering/" : p.SavePath;
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<RenderingRadianceCascadesResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var computePath = (savePath + $"RadianceCascades_{name}.compute").Replace("\\", "/");
            var scriptPath  = (savePath + $"RadianceCascadesFeature_{name}.cs").Replace("\\", "/");

            string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

            var computeSrc = BuildComputeShader(name, cascadeCount, bounceCount, F(probeSpacing), F(intensity));
            string scriptSrc = pipeline == "urp"
                ? BuildUrpFeature(name, cascadeCount, bounceCount, probeSpacing, intensity, resolution, F)
                : BuildHdrpComponent(name, cascadeCount, bounceCount, probeSpacing, intensity, resolution, F);

            WriteFile(projectRoot, computePath, computeSrc);
            WriteFile(projectRoot, scriptPath,  scriptSrc);
            AssetDatabase.Refresh();

            return ToolResult<RenderingRadianceCascadesResult>.Ok(new RenderingRadianceCascadesResult
            {
                ScriptPath        = scriptPath,
                ComputeShaderPath = computePath,
                CascadeCount      = cascadeCount,
                Pipeline          = pipeline
            });
        }

        static string BuildComputeShader(string name, int cascades, int bounces,
                                         string probeSpacing, string intensity)
        {
            return $@"// Radiance Cascades GI compute shader (Alexander Sannikov 2023, MVP scaffold)
// Cascade N: probe count halves, ray count quadruples, ray length quadruples.
// Final irradiance is obtained by bilinearly merging cascade i+1 into cascade i
// and sampling cascade 0 at the shade point.

#pragma kernel GenerateCascade
#pragma kernel MergeCascades
#pragma kernel ApplyIrradiance

#define CASCADE_COUNT {cascades}
#define BOUNCE_COUNT  {bounces}

// Per-cascade radiance buffers. Flat layout: [probeY * probesX + probeX] * rayCount + rayIndex.
RWStructuredBuffer<float4> _Cascade0;
RWStructuredBuffer<float4> _Cascade1;
RWStructuredBuffer<float4> _CascadeOut;   // target for merge
StructuredBuffer<float4>   _CascadeLower; // source (higher cascade index = sparser/longer)
StructuredBuffer<float4>   _CascadeUpper;

RWTexture2D<float4> _IrradianceTarget;
Texture2D<float>    _CameraDepthTexture;
Texture2D<float4>   _CameraColorTexture;
SamplerState sampler_CameraDepthTexture;
SamplerState sampler_CameraColorTexture;

uint2  _ScreenSize;
uint   _CascadeIndex;      // 0 = densest
uint2  _ProbeCounts;       // probes per cascade level
uint   _RaysPerProbe;
float  _ProbeSpacing;      // base spacing at cascade 0
float  _RayLength0;        // base ray length at cascade 0
float  _Intensity;
float4x4 _InvViewProj;

static const float PI = 3.14159265359;

// Reconstruct world position from depth (screen-space ray march fallback)
float3 ReconstructWorld(float2 uv, float depth)
{{
    float4 clip = float4(uv * 2.0 - 1.0, depth, 1.0);
    float4 wp = mul(_InvViewProj, clip);
    return wp.xyz / max(wp.w, 1e-6);
}}

// Simple 2-sphere equal-area ray direction from ray index
float3 RayDirection(uint rayIdx, uint rayCount)
{{
    float phi   = 2.0 * PI * (float)rayIdx / max(rayCount, 1u);
    float theta = acos(1.0 - 2.0 * frac((float)rayIdx * 0.61803398875));
    float s = sin(theta);
    return float3(s * cos(phi), cos(theta), s * sin(phi));
}}

// Screen-space ray march against depth buffer for MVP occlusion.
// Returns accumulated radiance along the ray (placeholder: sample scene color at hit).
float4 MarchRay(float3 origin, float3 dir, float length)
{{
    const int STEPS = 16;
    float stepLen = length / STEPS;
    float3 p = origin;
    float4 accum = 0;
    float transmittance = 1.0;

    [loop]
    for (int i = 0; i < STEPS; i++)
    {{
        p += dir * stepLen;
        // Placeholder: sample scene color as emissive along the ray.
        // Production would project p to clip space, sample depth, and shade hits.
        float2 uv = saturate(p.xy * 0.5 + 0.5);
        float4 sceneCol = _CameraColorTexture.SampleLevel(sampler_CameraColorTexture, uv, 0);
        accum.rgb += transmittance * sceneCol.rgb * 0.05;
        transmittance *= 0.95;
    }}
    accum.a = 1.0 - transmittance;
    return accum;
}}

// Cascade N: ray count = rays0 * 4^N, ray length = l0 * 4^N, probes = probes0 / 2^N.
[numthreads(8, 8, 1)]
void GenerateCascade(uint3 id : SV_DispatchThreadID)
{{
    if (any(id.xy >= _ProbeCounts)) return;

    uint probeIdx = id.y * _ProbeCounts.x + id.x;
    float scale   = pow(4.0, (float)_CascadeIndex);
    float rayLen  = _RayLength0 * scale;

    // Probe position in world space (uniform grid on screen plane for MVP)
    float2 probeUV = ((float2)id.xy + 0.5) / (float2)_ProbeCounts;
    float depth = _CameraDepthTexture.SampleLevel(sampler_CameraDepthTexture, probeUV, 0);
    float3 probeWP = ReconstructWorld(probeUV, depth);

    // Integrate radiance over the rays belonging to this probe.
    for (uint r = 0; r < _RaysPerProbe; r++)
    {{
        float3 dir = RayDirection(r, _RaysPerProbe);
        float4 rad = MarchRay(probeWP, dir, rayLen);
        uint slot  = probeIdx * _RaysPerProbe + r;
        _CascadeOut[slot] = rad;
    }}
}}

// Bilinearly interpolate cascade N+1 (sparser, longer rays) into cascade N.
// Preserves energy by averaging 4 neighbours in the coarser cascade.
[numthreads(8, 8, 1)]
void MergeCascades(uint3 id : SV_DispatchThreadID)
{{
    if (any(id.xy >= _ProbeCounts)) return;

    uint lowerIdx = id.y * _ProbeCounts.x + id.x;
    uint upperX   = id.x >> 1;
    uint upperY   = id.y >> 1;
    uint upperStride = max(_ProbeCounts.x >> 1, 1u);
    uint upperIdx = upperY * upperStride + upperX;

    for (uint r = 0; r < _RaysPerProbe; r++)
    {{
        uint slot   = lowerIdx * _RaysPerProbe + r;
        // Upper cascade stores 4x the rays; sample corresponding angular bucket.
        uint upperR = r * 4;
        float4 upper =
            _CascadeUpper[upperIdx * (_RaysPerProbe * 4) + upperR + 0] +
            _CascadeUpper[upperIdx * (_RaysPerProbe * 4) + upperR + 1] +
            _CascadeUpper[upperIdx * (_RaysPerProbe * 4) + upperR + 2] +
            _CascadeUpper[upperIdx * (_RaysPerProbe * 4) + upperR + 3];
        upper *= 0.25;

        float4 lower = _CascadeLower[slot];
        // Continuity merge: lower ray sees its own near-field plus far-field from upper.
        _CascadeOut[slot] = lower + lower.a * upper;
    }}
}}

// Final compose: sample cascade 0 and hemispherical-average rays to produce irradiance.
[numthreads(8, 8, 1)]
void ApplyIrradiance(uint3 id : SV_DispatchThreadID)
{{
    if (any(id.xy >= _ScreenSize)) return;

    float2 uv = ((float2)id.xy + 0.5) / (float2)_ScreenSize;
    float2 probeF = uv * (float2)_ProbeCounts - 0.5;
    uint2  probeI = (uint2)clamp(probeF, 0, (float2)_ProbeCounts - 1);
    uint probeIdx = probeI.y * _ProbeCounts.x + probeI.x;

    float4 irradiance = 0;
    for (uint r = 0; r < _RaysPerProbe; r++)
    {{
        irradiance += _Cascade0[probeIdx * _RaysPerProbe + r];
    }}
    irradiance /= max((float)_RaysPerProbe, 1.0);

    float4 sceneCol = _CameraColorTexture.SampleLevel(sampler_CameraColorTexture, uv, 0);
    float3 gi = irradiance.rgb * _Intensity;
    _IrradianceTarget[id.xy] = float4(sceneCol.rgb + gi, 1.0);
}}
";
        }

        static string BuildUrpFeature(string name, int cascadeCount, int bounceCount,
                                      float probeSpacing, float intensity, int[] resolution,
                                      System.Func<float, string> F)
        {
            return $@"using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_URP
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Radiance Cascades GI renderer feature for URP.
/// Dispatches GenerateCascade for each cascade level (dense->sparse),
/// then MergeCascades from sparse->dense, then ApplyIrradiance to
/// shade the main color buffer.
/// </summary>
#if UNITY_URP
public class RadianceCascadesFeature_{name} : ScriptableRendererFeature
{{
    [System.Serializable]
    public class Settings
    {{
        public ComputeShader cascadeCompute;
        public int   cascadeCount = {cascadeCount};
        public int   bounceCount  = {bounceCount};
        public float probeSpacing = {F(probeSpacing)}f;
        public float intensity    = {F(intensity)}f;
        public Vector2Int resolution = new Vector2Int({resolution[0]}, {resolution[1]});
    }}

    public Settings settings = new Settings();
    private RadianceCascadesPass_{name} _pass;

    public override void Create()
    {{
        _pass = new RadianceCascadesPass_{name}(settings)
        {{
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        }};
    }}

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {{
        if (settings.cascadeCompute == null) return;
        renderer.EnqueuePass(_pass);
    }}
}}

public class RadianceCascadesPass_{name} : ScriptableRenderPass
{{
    private readonly RadianceCascadesFeature_{name}.Settings _s;
    private ComputeBuffer[] _cascades;
    private RenderTexture   _irradiance;
    private int _kGen, _kMerge, _kApply;
    private const int RAYS0 = 4; // base rays per probe at cascade 0

    public RadianceCascadesPass_{name}(RadianceCascadesFeature_{name}.Settings s)
    {{
        _s = s;
    }}

    void EnsureResources()
    {{
        if (_cascades == null || _cascades.Length != _s.cascadeCount)
        {{
            ReleaseBuffers();
            _cascades = new ComputeBuffer[_s.cascadeCount];
            for (int i = 0; i < _s.cascadeCount; i++)
            {{
                int probesX = Mathf.Max(_s.resolution.x >> i, 1);
                int probesY = Mathf.Max(_s.resolution.y >> i, 1);
                int rays    = RAYS0 * (int)Mathf.Pow(4f, i);
                int count   = probesX * probesY * rays;
                _cascades[i] = new ComputeBuffer(count, sizeof(float) * 4);
            }}
        }}

        if (_irradiance == null || _irradiance.width != _s.resolution.x || _irradiance.height != _s.resolution.y)
        {{
            if (_irradiance != null) _irradiance.Release();
            _irradiance = new RenderTexture(_s.resolution.x, _s.resolution.y, 0, RenderTextureFormat.ARGBHalf)
            {{
                enableRandomWrite = true
            }};
            _irradiance.Create();
        }}
    }}

    void ReleaseBuffers()
    {{
        if (_cascades == null) return;
        for (int i = 0; i < _cascades.Length; i++)
            if (_cascades[i] != null) _cascades[i].Release();
        _cascades = null;
    }}

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {{
        if (_s.cascadeCompute == null) return;
        EnsureResources();

        _kGen   = _s.cascadeCompute.FindKernel(""GenerateCascade"");
        _kMerge = _s.cascadeCompute.FindKernel(""MergeCascades"");
        _kApply = _s.cascadeCompute.FindKernel(""ApplyIrradiance"");

        var cam = renderingData.cameraData.camera;
        var invVP = (cam.projectionMatrix * cam.worldToCameraMatrix).inverse;

        var cmd = CommandBufferPool.Get(""RadianceCascades_{name}"");
        cmd.SetComputeFloatParam(_s.cascadeCompute, ""_ProbeSpacing"", _s.probeSpacing);
        cmd.SetComputeFloatParam(_s.cascadeCompute, ""_RayLength0"",   _s.probeSpacing * 2f);
        cmd.SetComputeFloatParam(_s.cascadeCompute, ""_Intensity"",    _s.intensity);
        cmd.SetComputeMatrixParam(_s.cascadeCompute, ""_InvViewProj"", invVP);

        // --- Generate all cascades (dense -> sparse) ---
        for (int i = 0; i < _s.cascadeCount; i++)
        {{
            int probesX = Mathf.Max(_s.resolution.x >> i, 1);
            int probesY = Mathf.Max(_s.resolution.y >> i, 1);
            int rays    = RAYS0 * (int)Mathf.Pow(4f, i);

            cmd.SetComputeIntParam(_s.cascadeCompute,  ""_CascadeIndex"", i);
            cmd.SetComputeIntParams(_s.cascadeCompute, ""_ProbeCounts"",  probesX, probesY);
            cmd.SetComputeIntParam(_s.cascadeCompute,  ""_RaysPerProbe"", rays);
            cmd.SetComputeBufferParam(_s.cascadeCompute, _kGen, ""_CascadeOut"", _cascades[i]);
            cmd.DispatchCompute(_s.cascadeCompute, _kGen,
                Mathf.CeilToInt(probesX / 8f), Mathf.CeilToInt(probesY / 8f), 1);
        }}

        // --- Merge sparse -> dense ---
        for (int i = _s.cascadeCount - 2; i >= 0; i--)
        {{
            int probesX = Mathf.Max(_s.resolution.x >> i, 1);
            int probesY = Mathf.Max(_s.resolution.y >> i, 1);
            int rays    = RAYS0 * (int)Mathf.Pow(4f, i);

            cmd.SetComputeIntParams(_s.cascadeCompute, ""_ProbeCounts"",  probesX, probesY);
            cmd.SetComputeIntParam(_s.cascadeCompute,  ""_RaysPerProbe"", rays);
            cmd.SetComputeBufferParam(_s.cascadeCompute, _kMerge, ""_CascadeLower"", _cascades[i]);
            cmd.SetComputeBufferParam(_s.cascadeCompute, _kMerge, ""_CascadeUpper"", _cascades[i + 1]);
            cmd.SetComputeBufferParam(_s.cascadeCompute, _kMerge, ""_CascadeOut"",   _cascades[i]);
            cmd.DispatchCompute(_s.cascadeCompute, _kMerge,
                Mathf.CeilToInt(probesX / 8f), Mathf.CeilToInt(probesY / 8f), 1);
        }}

        // --- Apply irradiance to final image ---
        cmd.SetComputeIntParams(_s.cascadeCompute, ""_ScreenSize"",   _s.resolution.x, _s.resolution.y);
        cmd.SetComputeIntParams(_s.cascadeCompute, ""_ProbeCounts"",  _s.resolution.x, _s.resolution.y);
        cmd.SetComputeIntParam(_s.cascadeCompute,  ""_RaysPerProbe"", RAYS0);
        cmd.SetComputeBufferParam(_s.cascadeCompute, _kApply, ""_Cascade0"",         _cascades[0]);
        cmd.SetComputeTextureParam(_s.cascadeCompute, _kApply, ""_IrradianceTarget"", _irradiance);
        cmd.DispatchCompute(_s.cascadeCompute, _kApply,
            Mathf.CeilToInt(_s.resolution.x / 8f), Mathf.CeilToInt(_s.resolution.y / 8f), 1);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }}
}}
#else
public class RadianceCascadesFeature_{name} : MonoBehaviour {{ /* URP package not installed */ }}
#endif
";
        }

        static string BuildHdrpComponent(string name, int cascadeCount, int bounceCount,
                                         float probeSpacing, float intensity, int[] resolution,
                                         System.Func<float, string> F)
        {
            return $@"using UnityEngine;

/// <summary>
/// Radiance Cascades GI driver component for HDRP.
/// HDRP integration typically uses a CustomPass; this MVP dispatches the
/// compute directly from a camera hook so the scaffolding compiles without
/// requiring the HDRP package.
/// </summary>
[ExecuteAlways]
public class RadianceCascadesFeature_{name} : MonoBehaviour
{{
    public ComputeShader cascadeCompute;
    public int   cascadeCount = {cascadeCount};
    public int   bounceCount  = {bounceCount};
    public float probeSpacing = {F(probeSpacing)}f;
    public float intensity    = {F(intensity)}f;
    public Vector2Int resolution = new Vector2Int({resolution[0]}, {resolution[1]});

    private ComputeBuffer[] _cascades;
    private RenderTexture   _irradiance;
    private const int RAYS0 = 4;

    void OnEnable()  {{ Allocate(); }}
    void OnDisable() {{ Release(); }}

    void Allocate()
    {{
        _cascades = new ComputeBuffer[cascadeCount];
        for (int i = 0; i < cascadeCount; i++)
        {{
            int px = Mathf.Max(resolution.x >> i, 1);
            int py = Mathf.Max(resolution.y >> i, 1);
            int rays = RAYS0 * (int)Mathf.Pow(4f, i);
            _cascades[i] = new ComputeBuffer(px * py * rays, sizeof(float) * 4);
        }}
        _irradiance = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBHalf)
        {{
            enableRandomWrite = true
        }};
        _irradiance.Create();
    }}

    void Release()
    {{
        if (_cascades != null)
            for (int i = 0; i < _cascades.Length; i++)
                if (_cascades[i] != null) _cascades[i].Release();
        _cascades = null;
        if (_irradiance != null) _irradiance.Release();
    }}

    void Update()
    {{
        if (cascadeCompute == null || _cascades == null) return;
        int kG = cascadeCompute.FindKernel(""GenerateCascade"");
        int kM = cascadeCompute.FindKernel(""MergeCascades"");
        int kA = cascadeCompute.FindKernel(""ApplyIrradiance"");

        cascadeCompute.SetFloat(""_ProbeSpacing"", probeSpacing);
        cascadeCompute.SetFloat(""_RayLength0"",   probeSpacing * 2f);
        cascadeCompute.SetFloat(""_Intensity"",    intensity);

        for (int i = 0; i < cascadeCount; i++)
        {{
            int px = Mathf.Max(resolution.x >> i, 1);
            int py = Mathf.Max(resolution.y >> i, 1);
            int rays = RAYS0 * (int)Mathf.Pow(4f, i);
            cascadeCompute.SetInt(""_CascadeIndex"", i);
            cascadeCompute.SetInts(""_ProbeCounts"", px, py);
            cascadeCompute.SetInt(""_RaysPerProbe"", rays);
            cascadeCompute.SetBuffer(kG, ""_CascadeOut"", _cascades[i]);
            cascadeCompute.Dispatch(kG, Mathf.CeilToInt(px / 8f), Mathf.CeilToInt(py / 8f), 1);
        }}

        for (int i = cascadeCount - 2; i >= 0; i--)
        {{
            int px = Mathf.Max(resolution.x >> i, 1);
            int py = Mathf.Max(resolution.y >> i, 1);
            int rays = RAYS0 * (int)Mathf.Pow(4f, i);
            cascadeCompute.SetInts(""_ProbeCounts"", px, py);
            cascadeCompute.SetInt(""_RaysPerProbe"", rays);
            cascadeCompute.SetBuffer(kM, ""_CascadeLower"", _cascades[i]);
            cascadeCompute.SetBuffer(kM, ""_CascadeUpper"", _cascades[i + 1]);
            cascadeCompute.SetBuffer(kM, ""_CascadeOut"",   _cascades[i]);
            cascadeCompute.Dispatch(kM, Mathf.CeilToInt(px / 8f), Mathf.CeilToInt(py / 8f), 1);
        }}

        cascadeCompute.SetInts(""_ScreenSize"",   resolution.x, resolution.y);
        cascadeCompute.SetInts(""_ProbeCounts"",  resolution.x, resolution.y);
        cascadeCompute.SetInt(""_RaysPerProbe"",  RAYS0);
        cascadeCompute.SetBuffer(kA, ""_Cascade0"", _cascades[0]);
        cascadeCompute.SetTexture(kA, ""_IrradianceTarget"", _irradiance);
        cascadeCompute.Dispatch(kA,
            Mathf.CeilToInt(resolution.x / 8f),
            Mathf.CeilToInt(resolution.y / 8f), 1);
    }}
}}
";
        }

        static void WriteFile(string projectRoot, string assetPath, string content)
        {
            var fullPath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content, Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath);
        }
    }
}
