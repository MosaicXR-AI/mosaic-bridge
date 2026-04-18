using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    /// <summary>
    /// Generates a URP Screen-Space Reflections (SSR) shader + ScriptableRendererFeature pair.
    /// Implements the McGuire &amp; Mara 2014 screen-space ray-march with binary-search refinement.
    /// </summary>
    public static class ShaderSsrConfigureTool
    {
        static readonly string[] ValidQualities = { "low", "medium", "high", "ultra" };

        [MosaicTool("shader/ssr-configure",
                    "Generates URP SSR shader + ScriptableRendererFeature with quality presets",
                    isReadOnly: false, category: "shader", Context = ToolContext.Both)]
        public static ToolResult<ShaderSsrConfigureResult> Execute(ShaderSsrConfigureParams p)
        {
            p = p ?? new ShaderSsrConfigureParams();

            var quality = string.IsNullOrEmpty(p.Quality) ? "medium" : p.Quality.ToLowerInvariant();
            if (!ValidQualities.Contains(quality))
                return ToolResult<ShaderSsrConfigureResult>.Fail(
                    $"Invalid quality '{p.Quality}'. Valid: {string.Join(", ", ValidQualities)}",
                    ErrorCodes.INVALID_PARAM);

            // Quality preset defaults for MaxSteps / StepSize.
            int   presetSteps;
            float presetStep;
            switch (quality)
            {
                case "low":    presetSteps = 32;  presetStep = 0.2f;   break;
                case "high":   presetSteps = 128; presetStep = 0.05f;  break;
                case "ultra":  presetSteps = 256; presetStep = 0.025f; break;
                default:       presetSteps = 64;  presetStep = 0.1f;   break; // medium
            }

            var maxSteps    = p.MaxSteps     ?? presetSteps;
            var stepSize    = p.StepSize     ?? presetStep;
            var thickness   = p.Thickness    ?? 0.5f;
            var fresnelFade = p.FresnelFade  ?? 1.0f;
            var maxDistance = p.MaxDistance  ?? 50f;

            if (maxSteps <= 0)
                return ToolResult<ShaderSsrConfigureResult>.Fail(
                    "MaxSteps must be > 0", ErrorCodes.INVALID_PARAM);
            if (stepSize <= 0f)
                return ToolResult<ShaderSsrConfigureResult>.Fail(
                    "StepSize must be > 0", ErrorCodes.INVALID_PARAM);

            var targetCameraName = p.TargetCamera;
            if (!string.IsNullOrEmpty(targetCameraName))
            {
                var go = GameObject.Find(targetCameraName);
                if (go == null || go.GetComponent<Camera>() == null)
                    return ToolResult<ShaderSsrConfigureResult>.Fail(
                        $"TargetCamera '{targetCameraName}' not found or has no Camera component",
                        ErrorCodes.NOT_FOUND);
            }

            var savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Rendering/" : p.SavePath;
            savePath = savePath.Replace("\\", "/");
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<ShaderSsrConfigureResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            var name = string.IsNullOrEmpty(p.OutputName) ? "Default" : SanitizeName(p.OutputName);

            var shaderFileName  = $"SSR_{name}.shader";
            var featureFileName = $"SSRFeature_{name}.cs";
            var shaderAssetPath = savePath + shaderFileName;
            var scriptAssetPath = savePath + featureFileName;

            string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

            // ── HLSL SSR shader ────────────────────────────────────────────
            var shaderSb = new StringBuilder();
            shaderSb.AppendLine($"Shader \"Mosaic/SSR_{name}\"");
            shaderSb.AppendLine("{");
            shaderSb.AppendLine("    Properties");
            shaderSb.AppendLine("    {");
            shaderSb.AppendLine("        _MainTex (\"Scene Color\", 2D) = \"white\" {}");
            shaderSb.AppendLine($"        _MaxSteps (\"Max Steps\", Int) = {maxSteps}");
            shaderSb.AppendLine($"        _StepSize (\"Step Size\", Float) = {F(stepSize)}");
            shaderSb.AppendLine($"        _Thickness (\"Hit Thickness\", Float) = {F(thickness)}");
            shaderSb.AppendLine($"        _FresnelFade (\"Fresnel Fade\", Float) = {F(fresnelFade)}");
            shaderSb.AppendLine($"        _MaxDistance (\"Max Ray Distance\", Float) = {F(maxDistance)}");
            shaderSb.AppendLine("    }");
            shaderSb.AppendLine("    SubShader");
            shaderSb.AppendLine("    {");
            shaderSb.AppendLine("        Tags { \"RenderType\"=\"Opaque\" \"RenderPipeline\"=\"UniversalPipeline\" }");
            shaderSb.AppendLine("        Cull Off ZWrite Off ZTest Always");
            shaderSb.AppendLine();
            shaderSb.AppendLine("        Pass");
            shaderSb.AppendLine("        {");
            shaderSb.AppendLine("            Name \"SSR\"");
            shaderSb.AppendLine("            HLSLPROGRAM");
            shaderSb.AppendLine("            #pragma vertex Vert");
            shaderSb.AppendLine("            #pragma fragment Frag");
            shaderSb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"");
            shaderSb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl\"");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);");
            shaderSb.AppendLine("            TEXTURE2D(_CameraNormalsTexture); SAMPLER(sampler_CameraNormalsTexture);");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            int   _MaxSteps;");
            shaderSb.AppendLine("            float _StepSize;");
            shaderSb.AppendLine("            float _Thickness;");
            shaderSb.AppendLine("            float _FresnelFade;");
            shaderSb.AppendLine("            float _MaxDistance;");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };");
            shaderSb.AppendLine("            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            Varyings Vert(Attributes IN)");
            shaderSb.AppendLine("            {");
            shaderSb.AppendLine("                Varyings OUT;");
            shaderSb.AppendLine("                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);");
            shaderSb.AppendLine("                OUT.uv = IN.uv;");
            shaderSb.AppendLine("                return OUT;");
            shaderSb.AppendLine("            }");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            float3 ReconstructWorldPos(float2 uv, float rawDepth)");
            shaderSb.AppendLine("            {");
            shaderSb.AppendLine("                float4 ndc = float4(uv * 2.0 - 1.0, rawDepth, 1.0);");
            shaderSb.AppendLine("                #if UNITY_UV_STARTS_AT_TOP");
            shaderSb.AppendLine("                ndc.y = -ndc.y;");
            shaderSb.AppendLine("                #endif");
            shaderSb.AppendLine("                float4 wpos = mul(UNITY_MATRIX_I_VP, ndc);");
            shaderSb.AppendLine("                return wpos.xyz / wpos.w;");
            shaderSb.AppendLine("            }");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            float SchlickFresnel(float cosTheta)");
            shaderSb.AppendLine("            {");
            shaderSb.AppendLine("                float f = 1.0 - saturate(cosTheta);");
            shaderSb.AppendLine("                return pow(f, 5.0);");
            shaderSb.AppendLine("            }");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            // Project a world-space point to screen UV + linear eye depth.");
            shaderSb.AppendLine("            bool ProjectToScreen(float3 worldPos, out float2 outUV, out float outEyeDepth)");
            shaderSb.AppendLine("            {");
            shaderSb.AppendLine("                float4 clip = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));");
            shaderSb.AppendLine("                if (clip.w <= 0.0) { outUV = 0; outEyeDepth = 0; return false; }");
            shaderSb.AppendLine("                float3 ndc = clip.xyz / clip.w;");
            shaderSb.AppendLine("                outUV = ndc.xy * 0.5 + 0.5;");
            shaderSb.AppendLine("                #if UNITY_UV_STARTS_AT_TOP");
            shaderSb.AppendLine("                outUV.y = 1.0 - outUV.y;");
            shaderSb.AppendLine("                #endif");
            shaderSb.AppendLine("                outEyeDepth = clip.w;");
            shaderSb.AppendLine("                return true;");
            shaderSb.AppendLine("            }");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            // McGuire & Mara 2014-style screen-space ray march with binary refinement.");
            shaderSb.AppendLine("            bool TraceSSR(float3 rayOriginWS, float3 rayDirWS, out float2 hitUV)");
            shaderSb.AppendLine("            {");
            shaderSb.AppendLine("                hitUV = 0;");
            shaderSb.AppendLine("                float3 p = rayOriginWS;");
            shaderSb.AppendLine("                float  step = _StepSize;");
            shaderSb.AppendLine("                float  travelled = 0.0;");
            shaderSb.AppendLine("                float3 lastP = p;");
            shaderSb.AppendLine("                [loop]");
            shaderSb.AppendLine("                for (int i = 0; i < _MaxSteps; ++i)");
            shaderSb.AppendLine("                {");
            shaderSb.AppendLine("                    lastP = p;");
            shaderSb.AppendLine("                    p += rayDirWS * step;");
            shaderSb.AppendLine("                    travelled += step;");
            shaderSb.AppendLine("                    if (travelled > _MaxDistance) return false;");
            shaderSb.AppendLine();
            shaderSb.AppendLine("                    float2 uv; float rayEye;");
            shaderSb.AppendLine("                    if (!ProjectToScreen(p, uv, rayEye)) return false;");
            shaderSb.AppendLine("                    if (any(uv < 0.0) || any(uv > 1.0)) return false;");
            shaderSb.AppendLine();
            shaderSb.AppendLine("                    float sceneRaw = SampleSceneDepth(uv);");
            shaderSb.AppendLine("                    float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);");
            shaderSb.AppendLine("                    float delta = rayEye - sceneEye;");
            shaderSb.AppendLine();
            shaderSb.AppendLine("                    if (delta > 0.0 && delta < _Thickness)");
            shaderSb.AppendLine("                    {");
            shaderSb.AppendLine("                        // Binary-search refinement between lastP and p.");
            shaderSb.AppendLine("                        float3 a = lastP; float3 b = p;");
            shaderSb.AppendLine("                        [unroll]");
            shaderSb.AppendLine("                        for (int j = 0; j < 6; ++j)");
            shaderSb.AppendLine("                        {");
            shaderSb.AppendLine("                            float3 mid = (a + b) * 0.5;");
            shaderSb.AppendLine("                            float2 mUV; float mEye;");
            shaderSb.AppendLine("                            if (!ProjectToScreen(mid, mUV, mEye)) break;");
            shaderSb.AppendLine("                            float mRaw = SampleSceneDepth(mUV);");
            shaderSb.AppendLine("                            float mSceneEye = LinearEyeDepth(mRaw, _ZBufferParams);");
            shaderSb.AppendLine("                            if (mEye - mSceneEye > 0.0) b = mid; else a = mid;");
            shaderSb.AppendLine("                            uv = mUV;");
            shaderSb.AppendLine("                        }");
            shaderSb.AppendLine("                        hitUV = uv;");
            shaderSb.AppendLine("                        return true;");
            shaderSb.AppendLine("                    }");
            shaderSb.AppendLine("                }");
            shaderSb.AppendLine("                return false;");
            shaderSb.AppendLine("            }");
            shaderSb.AppendLine();
            shaderSb.AppendLine("            half4 Frag(Varyings IN) : SV_Target");
            shaderSb.AppendLine("            {");
            shaderSb.AppendLine("                float2 uv = IN.uv;");
            shaderSb.AppendLine("                half4  sceneCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);");
            shaderSb.AppendLine("                float  rawDepth = SampleSceneDepth(uv);");
            shaderSb.AppendLine("                if (rawDepth <= 0.0) return sceneCol;");
            shaderSb.AppendLine();
            shaderSb.AppendLine("                float3 worldPos = ReconstructWorldPos(uv, rawDepth);");
            shaderSb.AppendLine("                float3 normalWS = normalize(SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv).xyz * 2.0 - 1.0);");
            shaderSb.AppendLine("                float3 viewDir  = normalize(worldPos - _WorldSpaceCameraPos);");
            shaderSb.AppendLine("                float3 reflDir  = reflect(viewDir, normalWS);");
            shaderSb.AppendLine();
            shaderSb.AppendLine("                float2 hitUV;");
            shaderSb.AppendLine("                if (!TraceSSR(worldPos + normalWS * 0.01, reflDir, hitUV))");
            shaderSb.AppendLine("                    return sceneCol;");
            shaderSb.AppendLine();
            shaderSb.AppendLine("                half4 reflCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, hitUV);");
            shaderSb.AppendLine("                float fres = SchlickFresnel(dot(-viewDir, normalWS)) * _FresnelFade;");
            shaderSb.AppendLine("                return lerp(sceneCol, reflCol, saturate(fres));");
            shaderSb.AppendLine("            }");
            shaderSb.AppendLine("            ENDHLSL");
            shaderSb.AppendLine("        }");
            shaderSb.AppendLine("    }");
            shaderSb.AppendLine("    Fallback Off");
            shaderSb.AppendLine("}");

            // ── ScriptableRendererFeature ──────────────────────────────────
            var featureSb = new StringBuilder();
            featureSb.AppendLine("using UnityEngine;");
            featureSb.AppendLine("using UnityEngine.Rendering;");
            featureSb.AppendLine("using UnityEngine.Rendering.Universal;");
            featureSb.AppendLine();
            featureSb.AppendLine("/// <summary>");
            featureSb.AppendLine($"/// URP Screen-Space Reflections render feature ({name}).");
            featureSb.AppendLine("/// Injected after opaque rendering; reads _CameraColorTexture + _CameraDepthTexture + _CameraNormalsTexture,");
            featureSb.AppendLine("/// writes to an intermediate RT, then composes back onto the camera color target.");
            featureSb.AppendLine("/// </summary>");
            featureSb.AppendLine($"public class SSRFeature_{name} : ScriptableRendererFeature");
            featureSb.AppendLine("{");
            featureSb.AppendLine("    [System.Serializable]");
            featureSb.AppendLine("    public class Settings");
            featureSb.AppendLine("    {");
            featureSb.AppendLine("        public Shader ssrShader;");
            featureSb.AppendLine($"        public int   maxSteps    = {maxSteps};");
            featureSb.AppendLine($"        public float stepSize    = {F(stepSize)}f;");
            featureSb.AppendLine($"        public float thickness   = {F(thickness)}f;");
            featureSb.AppendLine($"        public float fresnelFade = {F(fresnelFade)}f;");
            featureSb.AppendLine($"        public float maxDistance = {F(maxDistance)}f;");
            featureSb.AppendLine("        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;");
            featureSb.AppendLine("    }");
            featureSb.AppendLine();
            featureSb.AppendLine("    public Settings settings = new Settings();");
            featureSb.AppendLine("    private SSRPass _pass;");
            featureSb.AppendLine("    private Material _material;");
            featureSb.AppendLine();
            featureSb.AppendLine("    public override void Create()");
            featureSb.AppendLine("    {");
            featureSb.AppendLine($"        if (settings.ssrShader == null) settings.ssrShader = Shader.Find(\"Mosaic/SSR_{name}\");");
            featureSb.AppendLine("        if (settings.ssrShader == null) return;");
            featureSb.AppendLine("        _material = CoreUtils.CreateEngineMaterial(settings.ssrShader);");
            featureSb.AppendLine("        _pass = new SSRPass(_material, settings) { renderPassEvent = settings.renderPassEvent };");
            featureSb.AppendLine("    }");
            featureSb.AppendLine();
            featureSb.AppendLine("    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            featureSb.AppendLine("    {");
            featureSb.AppendLine("        if (_pass == null || _material == null) return;");
            featureSb.AppendLine("        _pass.Setup(renderer.cameraColorTargetHandle);");
            featureSb.AppendLine("        renderer.EnqueuePass(_pass);");
            featureSb.AppendLine("    }");
            featureSb.AppendLine();
            featureSb.AppendLine("    protected override void Dispose(bool disposing)");
            featureSb.AppendLine("    {");
            featureSb.AppendLine("        CoreUtils.Destroy(_material);");
            featureSb.AppendLine("    }");
            featureSb.AppendLine();
            featureSb.AppendLine("    // ── SSR pass ─────────────────────────────────────────────────");
            featureSb.AppendLine("    private class SSRPass : ScriptableRenderPass");
            featureSb.AppendLine("    {");
            featureSb.AppendLine("        private readonly Material _mat;");
            featureSb.AppendLine("        private readonly Settings _settings;");
            featureSb.AppendLine("        private RTHandle _source;");
            featureSb.AppendLine("        private RTHandle _tmp;");
            featureSb.AppendLine("        private static readonly int IdMaxSteps    = Shader.PropertyToID(\"_MaxSteps\");");
            featureSb.AppendLine("        private static readonly int IdStepSize    = Shader.PropertyToID(\"_StepSize\");");
            featureSb.AppendLine("        private static readonly int IdThickness   = Shader.PropertyToID(\"_Thickness\");");
            featureSb.AppendLine("        private static readonly int IdFresnelFade = Shader.PropertyToID(\"_FresnelFade\");");
            featureSb.AppendLine("        private static readonly int IdMaxDistance = Shader.PropertyToID(\"_MaxDistance\");");
            featureSb.AppendLine();
            featureSb.AppendLine("        public SSRPass(Material mat, Settings s)");
            featureSb.AppendLine("        {");
            featureSb.AppendLine("            _mat = mat; _settings = s;");
            featureSb.AppendLine("            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color);");
            featureSb.AppendLine("        }");
            featureSb.AppendLine();
            featureSb.AppendLine("        public void Setup(RTHandle source) { _source = source; }");
            featureSb.AppendLine();
            featureSb.AppendLine("        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)");
            featureSb.AppendLine("        {");
            featureSb.AppendLine("            var desc = renderingData.cameraData.cameraTargetDescriptor;");
            featureSb.AppendLine("            desc.depthBufferBits = 0;");
            featureSb.AppendLine("            RenderingUtils.ReAllocateIfNeeded(ref _tmp, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: \"_SSRIntermediate\");");
            featureSb.AppendLine("        }");
            featureSb.AppendLine();
            featureSb.AppendLine("        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)");
            featureSb.AppendLine("        {");
            featureSb.AppendLine("            if (_mat == null || _source == null || _tmp == null) return;");
            featureSb.AppendLine($"            var cmd = CommandBufferPool.Get(\"SSR_{name}\");");
            featureSb.AppendLine("            _mat.SetInt(IdMaxSteps, _settings.maxSteps);");
            featureSb.AppendLine("            _mat.SetFloat(IdStepSize, _settings.stepSize);");
            featureSb.AppendLine("            _mat.SetFloat(IdThickness, _settings.thickness);");
            featureSb.AppendLine("            _mat.SetFloat(IdFresnelFade, _settings.fresnelFade);");
            featureSb.AppendLine("            _mat.SetFloat(IdMaxDistance, _settings.maxDistance);");
            featureSb.AppendLine("            // Source -> intermediate (SSR composed), then intermediate -> source.");
            featureSb.AppendLine("            Blitter.BlitCameraTexture(cmd, _source, _tmp, _mat, 0);");
            featureSb.AppendLine("            Blitter.BlitCameraTexture(cmd, _tmp, _source);");
            featureSb.AppendLine("            context.ExecuteCommandBuffer(cmd);");
            featureSb.AppendLine("            CommandBufferPool.Release(cmd);");
            featureSb.AppendLine("        }");
            featureSb.AppendLine();
            featureSb.AppendLine("        public void Dispose() { _tmp?.Release(); }");
            featureSb.AppendLine("    }");
            featureSb.AppendLine("}");

            // ── Write to disk ──────────────────────────────────────────────
            try
            {
                var projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
                WriteFile(projectRoot, shaderAssetPath, shaderSb.ToString());
                WriteFile(projectRoot, scriptAssetPath, featureSb.ToString());
            }
            catch (Exception e)
            {
                return ToolResult<ShaderSsrConfigureResult>.Fail(
                    $"Failed to write SSR assets: {e.Message}", ErrorCodes.INTERNAL_ERROR);
            }

            return ToolResult<ShaderSsrConfigureResult>.Ok(new ShaderSsrConfigureResult
            {
                ScriptPath = scriptAssetPath,
                ShaderPath = shaderAssetPath,
                Quality    = quality,
                MaxSteps   = maxSteps
            });
        }

        static void WriteFile(string projectRoot, string assetPath, string content)
        {
            var fullPath = Path.Combine(projectRoot, assetPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, content, Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath);
        }

        static string SanitizeName(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw)
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            var s = sb.ToString();
            if (s.Length == 0) return "Default";
            if (char.IsDigit(s[0])) s = "_" + s;
            return s;
        }
    }
}
