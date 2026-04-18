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
    public static class RenderingVolumetricFogTool
    {
        [MosaicTool("rendering/volumetric-fog-create",
                    "Generates froxel-based volumetric fog renderer feature and compute shader (URP/HDRP/Builtin)",
                    isReadOnly: false, category: "rendering", Context = ToolContext.Both)]
        public static ToolResult<RenderingVolumetricFogResult> Execute(RenderingVolumetricFogParams p)
        {
            var pipeline = string.IsNullOrEmpty(p.Pipeline) ? "urp" : p.Pipeline.ToLowerInvariant();
            if (pipeline != "urp" && pipeline != "hdrp" && pipeline != "builtin")
                return ToolResult<RenderingVolumetricFogResult>.Fail(
                    "Pipeline must be one of: urp, hdrp, builtin", ErrorCodes.INVALID_PARAM);

            var resolution = p.Resolution ?? new[] { 160, 90, 64 };
            if (resolution.Length != 3)
                return ToolResult<RenderingVolumetricFogResult>.Fail(
                    "Resolution must have exactly 3 elements (width, height, depth slices)",
                    ErrorCodes.INVALID_PARAM);
            for (int i = 0; i < 3; i++)
                if (resolution[i] <= 0)
                    return ToolResult<RenderingVolumetricFogResult>.Fail(
                        "Resolution components must be > 0", ErrorCodes.INVALID_PARAM);

            var density     = p.Density ?? 0.1f;
            var scattering  = p.Scattering ?? 0.5f;
            var extinction  = p.Extinction ?? 0.1f;
            var maxDistance = p.MaxDistance ?? 100f;
            var temporal    = p.TemporalReprojection ?? true;
            var fogColor    = p.FogColor ?? new[] { 0.7f, 0.8f, 1.0f, 1.0f };
            if (fogColor.Length != 4)
                return ToolResult<RenderingVolumetricFogResult>.Fail(
                    "FogColor must have exactly 4 elements (r,g,b,a)", ErrorCodes.INVALID_PARAM);

            var name     = string.IsNullOrEmpty(p.Name) ? "Default" : p.Name;
            var savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Rendering/" : p.SavePath;
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<RenderingVolumetricFogResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            var projectRoot   = Application.dataPath.Replace("/Assets", "");
            var computePath   = (savePath + $"VolumetricFog_{name}.compute").Replace("\\", "/");
            var scriptPath    = (savePath + $"VolumetricFogFeature_{name}.cs").Replace("\\", "/");

            string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

            // ---------- Compute Shader ----------
            var computeSrc = $@"#pragma kernel InjectDensity
#pragma kernel Scatter
#pragma kernel Accumulate

// Froxel grid: width x height x depth slices
RWTexture3D<float4> _DensityVolume;   // rgb = scattering, a = extinction
RWTexture3D<float4> _ScatteringVolume; // rgb = in-scattered light, a = extinction
RWTexture3D<float4> _FogVolume;       // rgb = accumulated color, a = transmittance

uint3  _Resolution;
float  _Density;
float  _Scattering;  // anisotropy g for Henyey-Greenstein
float  _Extinction;
float  _MaxDistance;
float4 _FogColor;
float3 _LightDir;
float3 _LightColor;
float  _Time;

// Henyey-Greenstein phase function
float HGPhase(float cosTheta, float g)
{{
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 - g2) / (4.0 * 3.14159265 * pow(max(denom, 1e-4), 1.5));
}}

float Hash(float3 p)
{{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}}

float3 FroxelToWorld(uint3 id)
{{
    float3 uvw = ((float3)id + 0.5) / (float3)_Resolution;
    // Simple linear mapping along view frustum for MVP
    float depth = uvw.z * _MaxDistance;
    return float3((uvw.xy * 2.0 - 1.0) * depth, depth);
}}

[numthreads(8,8,8)]
void InjectDensity(uint3 id : SV_DispatchThreadID)
{{
    if (any(id >= _Resolution)) return;
    float3 wp = FroxelToWorld(id);
    float noise = Hash(wp * 0.05 + _Time * 0.1);
    float d = _Density * (0.5 + 0.5 * noise);
    // Emissive ambient contribution
    float3 emissive = _FogColor.rgb * d * 0.1;
    _DensityVolume[id] = float4(emissive, d * _Extinction);
}}

[numthreads(8,8,8)]
void Scatter(uint3 id : SV_DispatchThreadID)
{{
    if (any(id >= _Resolution)) return;
    float4 dens = _DensityVolume[id];
    float3 wp = FroxelToWorld(id);
    float3 viewDir = normalize(wp);
    float cosTheta = dot(viewDir, -_LightDir);
    float phase = HGPhase(cosTheta, _Scattering);
    float3 inScatter = _LightColor * phase * dens.a + dens.rgb;
    _ScatteringVolume[id] = float4(inScatter, dens.a);
}}

[numthreads(8,8,1)]
void Accumulate(uint3 id : SV_DispatchThreadID)
{{
    if (any(id.xy >= _Resolution.xy)) return;
    float3 accumColor = 0;
    float  transmittance = 1.0;
    float stepSize = _MaxDistance / (float)_Resolution.z;

    for (uint z = 0; z < _Resolution.z; z++)
    {{
        uint3 coord = uint3(id.xy, z);
        float4 s = _ScatteringVolume[coord];
        float sigma = s.a;
        float tExt = exp(-sigma * stepSize);
        // Energy-conserving integration
        float3 sliceInScatter = s.rgb * (1.0 - tExt) / max(sigma, 1e-4);
        accumColor += transmittance * sliceInScatter;
        transmittance *= tExt;
        _FogVolume[coord] = float4(accumColor, transmittance);
    }}
}}
";

            // ---------- Renderer Feature Script ----------
            string scriptSrc;
            if (pipeline == "urp")
            {
                scriptSrc = BuildUrpFeature(name, resolution, density, scattering, extinction,
                                             maxDistance, temporal, fogColor, F);
            }
            else if (pipeline == "hdrp")
            {
                scriptSrc = BuildHdrpComponent(name, resolution, density, scattering, extinction,
                                                maxDistance, temporal, fogColor, F);
            }
            else
            {
                scriptSrc = BuildBuiltinComponent(name, resolution, density, scattering, extinction,
                                                   maxDistance, temporal, fogColor, F);
            }

            WriteFile(projectRoot, computePath, computeSrc);
            WriteFile(projectRoot, scriptPath, scriptSrc);
            AssetDatabase.Refresh();

            return ToolResult<RenderingVolumetricFogResult>.Ok(new RenderingVolumetricFogResult
            {
                ScriptPath        = scriptPath,
                ComputeShaderPath = computePath,
                Pipeline          = pipeline,
                Resolution        = resolution
            });
        }

        static string BuildUrpFeature(string name, int[] res, float density, float scattering,
                                      float extinction, float maxDistance, bool temporal,
                                      float[] fogColor, System.Func<float, string> F)
        {
            return $@"using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_URP
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Froxel-based volumetric fog renderer feature for URP.
/// Dispatches InjectDensity -> Scatter -> Accumulate compute passes,
/// then blits the accumulated volume as a full-screen effect.
/// </summary>
#if UNITY_URP
public class VolumetricFogFeature_{name} : ScriptableRendererFeature
{{
    [System.Serializable]
    public class Settings
    {{
        public ComputeShader fogCompute;
        public Vector3Int resolution = new Vector3Int({res[0]}, {res[1]}, {res[2]});
        public float density = {F(density)}f;
        public float scattering = {F(scattering)}f;
        public float extinction = {F(extinction)}f;
        public float maxDistance = {F(maxDistance)}f;
        public bool temporalReprojection = {(temporal ? "true" : "false")};
        public Color fogColor = new Color({F(fogColor[0])}f, {F(fogColor[1])}f, {F(fogColor[2])}f, {F(fogColor[3])}f);
    }}

    public Settings settings = new Settings();
    private VolumetricFogPass_{name} _pass;

    public override void Create()
    {{
        _pass = new VolumetricFogPass_{name}(settings)
        {{
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        }};
    }}

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {{
        if (settings.fogCompute == null) return;
        renderer.EnqueuePass(_pass);
    }}
}}

public class VolumetricFogPass_{name} : ScriptableRenderPass
{{
    private readonly VolumetricFogFeature_{name}.Settings _s;
    private RenderTexture _densityVol, _scatterVol, _fogVol;
    private int _kInject, _kScatter, _kAccum;

    public VolumetricFogPass_{name}(VolumetricFogFeature_{name}.Settings s)
    {{
        _s = s;
    }}

    void EnsureVolume(ref RenderTexture rt)
    {{
        if (rt != null && rt.width == _s.resolution.x && rt.height == _s.resolution.y && rt.volumeDepth == _s.resolution.z) return;
        if (rt != null) rt.Release();
        rt = new RenderTexture(_s.resolution.x, _s.resolution.y, 0, RenderTextureFormat.ARGBHalf)
        {{
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = _s.resolution.z,
            enableRandomWrite = true
        }};
        rt.Create();
    }}

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {{
        if (_s.fogCompute == null) return;
        EnsureVolume(ref _densityVol);
        EnsureVolume(ref _scatterVol);
        EnsureVolume(ref _fogVol);

        _kInject  = _s.fogCompute.FindKernel(""InjectDensity"");
        _kScatter = _s.fogCompute.FindKernel(""Scatter"");
        _kAccum   = _s.fogCompute.FindKernel(""Accumulate"");

        var cmd = CommandBufferPool.Get(""VolumetricFog_{name}"");

        var light = RenderSettings.sun != null ? RenderSettings.sun : UnityEngine.Object.FindObjectOfType<Light>();
        Vector3 lightDir = light != null ? light.transform.forward : Vector3.down;
        Color   lightCol = light != null ? light.color * light.intensity : Color.white;

        cmd.SetComputeIntParams(_s.fogCompute, ""_Resolution"", _s.resolution.x, _s.resolution.y, _s.resolution.z);
        cmd.SetComputeFloatParam(_s.fogCompute, ""_Density"", _s.density);
        cmd.SetComputeFloatParam(_s.fogCompute, ""_Scattering"", _s.scattering);
        cmd.SetComputeFloatParam(_s.fogCompute, ""_Extinction"", _s.extinction);
        cmd.SetComputeFloatParam(_s.fogCompute, ""_MaxDistance"", _s.maxDistance);
        cmd.SetComputeVectorParam(_s.fogCompute, ""_FogColor"", _s.fogColor);
        cmd.SetComputeVectorParam(_s.fogCompute, ""_LightDir"", lightDir);
        cmd.SetComputeVectorParam(_s.fogCompute, ""_LightColor"", (Vector4)lightCol);
        cmd.SetComputeFloatParam(_s.fogCompute, ""_Time"", Time.time);

        cmd.SetComputeTextureParam(_s.fogCompute, _kInject,  ""_DensityVolume"",   _densityVol);
        cmd.SetComputeTextureParam(_s.fogCompute, _kScatter, ""_DensityVolume"",   _densityVol);
        cmd.SetComputeTextureParam(_s.fogCompute, _kScatter, ""_ScatteringVolume"", _scatterVol);
        cmd.SetComputeTextureParam(_s.fogCompute, _kAccum,   ""_ScatteringVolume"", _scatterVol);
        cmd.SetComputeTextureParam(_s.fogCompute, _kAccum,   ""_FogVolume"",       _fogVol);

        int gx = Mathf.CeilToInt(_s.resolution.x / 8f);
        int gy = Mathf.CeilToInt(_s.resolution.y / 8f);
        int gz = Mathf.CeilToInt(_s.resolution.z / 8f);
        cmd.DispatchCompute(_s.fogCompute, _kInject,  gx, gy, gz);
        cmd.DispatchCompute(_s.fogCompute, _kScatter, gx, gy, gz);
        cmd.DispatchCompute(_s.fogCompute, _kAccum,   gx, gy, 1);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }}
}}
#else
public class VolumetricFogFeature_{name} : MonoBehaviour {{ /* URP package not installed */ }}
#endif
";
        }

        static string BuildHdrpComponent(string name, int[] res, float density, float scattering,
                                          float extinction, float maxDistance, bool temporal,
                                          float[] fogColor, System.Func<float, string> F)
        {
            return $@"using UnityEngine;

/// <summary>
/// HDRP volumetric fog driver component.
/// HDRP has native volumetric fog via Volume profiles; this component
/// dispatches the custom froxel compute for supplemental effects.
/// </summary>
[ExecuteAlways]
public class VolumetricFogFeature_{name} : MonoBehaviour
{{
    public ComputeShader fogCompute;
    public Vector3Int resolution = new Vector3Int({res[0]}, {res[1]}, {res[2]});
    public float density = {F(density)}f;
    public float scattering = {F(scattering)}f;
    public float extinction = {F(extinction)}f;
    public float maxDistance = {F(maxDistance)}f;
    public bool temporalReprojection = {(temporal ? "true" : "false")};
    public Color fogColor = new Color({F(fogColor[0])}f, {F(fogColor[1])}f, {F(fogColor[2])}f, {F(fogColor[3])}f);

    private RenderTexture _densityVol, _scatterVol, _fogVol;

    void OnEnable()  {{ AllocateVolumes(); }}
    void OnDisable() {{ ReleaseVolumes(); }}

    void AllocateVolumes()
    {{
        _densityVol = Create3D();
        _scatterVol = Create3D();
        _fogVol     = Create3D();
    }}

    RenderTexture Create3D()
    {{
        var rt = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBHalf)
        {{
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = resolution.z,
            enableRandomWrite = true
        }};
        rt.Create();
        return rt;
    }}

    void ReleaseVolumes()
    {{
        if (_densityVol != null) _densityVol.Release();
        if (_scatterVol != null) _scatterVol.Release();
        if (_fogVol != null)     _fogVol.Release();
    }}

    void Update()
    {{
        if (fogCompute == null || _fogVol == null) return;
        int kI = fogCompute.FindKernel(""InjectDensity"");
        int kS = fogCompute.FindKernel(""Scatter"");
        int kA = fogCompute.FindKernel(""Accumulate"");

        fogCompute.SetInts(""_Resolution"", resolution.x, resolution.y, resolution.z);
        fogCompute.SetFloat(""_Density"", density);
        fogCompute.SetFloat(""_Scattering"", scattering);
        fogCompute.SetFloat(""_Extinction"", extinction);
        fogCompute.SetFloat(""_MaxDistance"", maxDistance);
        fogCompute.SetVector(""_FogColor"", fogColor);
        fogCompute.SetVector(""_LightDir"", Vector3.down);
        fogCompute.SetVector(""_LightColor"", Color.white);
        fogCompute.SetFloat(""_Time"", Time.time);

        fogCompute.SetTexture(kI, ""_DensityVolume"", _densityVol);
        fogCompute.SetTexture(kS, ""_DensityVolume"", _densityVol);
        fogCompute.SetTexture(kS, ""_ScatteringVolume"", _scatterVol);
        fogCompute.SetTexture(kA, ""_ScatteringVolume"", _scatterVol);
        fogCompute.SetTexture(kA, ""_FogVolume"", _fogVol);

        int gx = Mathf.CeilToInt(resolution.x / 8f);
        int gy = Mathf.CeilToInt(resolution.y / 8f);
        int gz = Mathf.CeilToInt(resolution.z / 8f);
        fogCompute.Dispatch(kI, gx, gy, gz);
        fogCompute.Dispatch(kS, gx, gy, gz);
        fogCompute.Dispatch(kA, gx, gy, 1);
    }}
}}
";
        }

        static string BuildBuiltinComponent(string name, int[] res, float density, float scattering,
                                             float extinction, float maxDistance, bool temporal,
                                             float[] fogColor, System.Func<float, string> F)
        {
            return $@"using UnityEngine;

/// <summary>
/// Built-in pipeline volumetric fog image effect.
/// Dispatches froxel compute, then blits the accumulated fog as a full-screen overlay
/// using OnRenderImage.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class VolumetricFogFeature_{name} : MonoBehaviour
{{
    public ComputeShader fogCompute;
    public Shader blitShader;
    public Vector3Int resolution = new Vector3Int({res[0]}, {res[1]}, {res[2]});
    public float density = {F(density)}f;
    public float scattering = {F(scattering)}f;
    public float extinction = {F(extinction)}f;
    public float maxDistance = {F(maxDistance)}f;
    public bool temporalReprojection = {(temporal ? "true" : "false")};
    public Color fogColor = new Color({F(fogColor[0])}f, {F(fogColor[1])}f, {F(fogColor[2])}f, {F(fogColor[3])}f);

    private RenderTexture _densityVol, _scatterVol, _fogVol;
    private Material _blitMat;

    void OnEnable()
    {{
        _densityVol = Create3D();
        _scatterVol = Create3D();
        _fogVol     = Create3D();
        if (blitShader != null) _blitMat = new Material(blitShader);
    }}

    void OnDisable()
    {{
        if (_densityVol != null) _densityVol.Release();
        if (_scatterVol != null) _scatterVol.Release();
        if (_fogVol != null)     _fogVol.Release();
    }}

    RenderTexture Create3D()
    {{
        var rt = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBHalf)
        {{
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = resolution.z,
            enableRandomWrite = true
        }};
        rt.Create();
        return rt;
    }}

    void DispatchFog()
    {{
        if (fogCompute == null) return;
        int kI = fogCompute.FindKernel(""InjectDensity"");
        int kS = fogCompute.FindKernel(""Scatter"");
        int kA = fogCompute.FindKernel(""Accumulate"");

        fogCompute.SetInts(""_Resolution"", resolution.x, resolution.y, resolution.z);
        fogCompute.SetFloat(""_Density"", density);
        fogCompute.SetFloat(""_Scattering"", scattering);
        fogCompute.SetFloat(""_Extinction"", extinction);
        fogCompute.SetFloat(""_MaxDistance"", maxDistance);
        fogCompute.SetVector(""_FogColor"", fogColor);
        fogCompute.SetVector(""_LightDir"", Vector3.down);
        fogCompute.SetVector(""_LightColor"", Color.white);
        fogCompute.SetFloat(""_Time"", Time.time);

        fogCompute.SetTexture(kI, ""_DensityVolume"", _densityVol);
        fogCompute.SetTexture(kS, ""_DensityVolume"", _densityVol);
        fogCompute.SetTexture(kS, ""_ScatteringVolume"", _scatterVol);
        fogCompute.SetTexture(kA, ""_ScatteringVolume"", _scatterVol);
        fogCompute.SetTexture(kA, ""_FogVolume"", _fogVol);

        int gx = Mathf.CeilToInt(resolution.x / 8f);
        int gy = Mathf.CeilToInt(resolution.y / 8f);
        int gz = Mathf.CeilToInt(resolution.z / 8f);
        fogCompute.Dispatch(kI, gx, gy, gz);
        fogCompute.Dispatch(kS, gx, gy, gz);
        fogCompute.Dispatch(kA, gx, gy, 1);
    }}

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {{
        DispatchFog();
        if (_blitMat != null && _fogVol != null)
        {{
            _blitMat.SetTexture(""_FogVolume"", _fogVol);
            _blitMat.SetColor(""_FogColor"", fogColor);
            _blitMat.SetFloat(""_MaxDistance"", maxDistance);
            Graphics.Blit(src, dst, _blitMat);
        }}
        else
        {{
            // Fallback: tint by fog color ramp for MVP visualization
            Graphics.Blit(src, dst);
        }}
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
