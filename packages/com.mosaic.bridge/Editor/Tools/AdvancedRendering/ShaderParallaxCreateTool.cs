using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    /// <summary>
    /// Generates a Parallax Occlusion Mapping (POM) shader and material.
    /// Based on Tatarchuk 2006 "Practical Parallax Occlusion Mapping".
    /// Supports URP and Built-in render pipelines.
    /// </summary>
    public static class ShaderParallaxCreateTool
    {
        static readonly string[] ValidPipelines = { "urp", "builtin" };

        [MosaicTool("shader/parallax-create",
                    "Generates a parallax occlusion mapping (POM) shader + material (Tatarchuk 2006) with optional self-shadowing",
                    isReadOnly: false, category: "shader", Context = ToolContext.Both)]
        public static ToolResult<ShaderParallaxCreateResult> Execute(ShaderParallaxCreateParams p)
        {
            p ??= new ShaderParallaxCreateParams();

            var pipeline     = string.IsNullOrEmpty(p.Pipeline) ? "urp" : p.Pipeline.ToLowerInvariant();
            var heightScale  = p.HeightScale ?? 0.05f;
            var minSteps     = p.MinSteps ?? 8;
            var maxSteps     = p.MaxSteps ?? 32;
            var selfShadow   = p.SelfShadow ?? true;
            var savePath     = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Rendering/" : p.SavePath;
            var outputName   = string.IsNullOrEmpty(p.OutputName) ? "ParallaxOcclusion" : p.OutputName;

            // Validate pipeline
            bool validPipeline = false;
            foreach (var vp in ValidPipelines)
                if (vp == pipeline) { validPipeline = true; break; }
            if (!validPipeline)
                return ToolResult<ShaderParallaxCreateResult>.Fail(
                    $"Invalid pipeline '{p.Pipeline}'. Valid: urp, builtin",
                    ErrorCodes.INVALID_PARAM);

            // Validate SavePath
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<ShaderParallaxCreateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            // Validate steps
            if (minSteps < 1 || maxSteps < 1)
                return ToolResult<ShaderParallaxCreateResult>.Fail(
                    "MinSteps and MaxSteps must be >= 1", ErrorCodes.INVALID_PARAM);
            if (minSteps >= maxSteps)
                return ToolResult<ShaderParallaxCreateResult>.Fail(
                    $"MinSteps ({minSteps}) must be less than MaxSteps ({maxSteps})",
                    ErrorCodes.INVALID_PARAM);

            if (heightScale <= 0f)
                return ToolResult<ShaderParallaxCreateResult>.Fail(
                    "HeightScale must be > 0", ErrorCodes.INVALID_PARAM);

            // Normalize savePath
            if (!savePath.EndsWith("/")) savePath += "/";

            var shaderName = $"Mosaic/ParallaxOcclusion_{outputName}";
            var shaderPath = (savePath + outputName + ".shader").Replace("\\", "/");
            var matPath    = (savePath + outputName + "Mat.mat").Replace("\\", "/");

            // Generate shader source
            string source = pipeline == "urp"
                ? BuildUrpShader(shaderName, minSteps, maxSteps, heightScale, selfShadow)
                : BuildBuiltinShader(shaderName, minSteps, maxSteps, heightScale, selfShadow);

            // Write shader
            var projectRoot   = Application.dataPath.Replace("/Assets", "");
            var fullShaderPath = Path.Combine(projectRoot, shaderPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullShaderPath));
            File.WriteAllText(fullShaderPath, source, Encoding.UTF8);
            AssetDatabase.ImportAsset(shaderPath);

            // Create material
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                AssetDatabase.Refresh();
                shader = Shader.Find(shaderName);
            }
            if (shader == null)
                shader = Shader.Find(pipeline == "urp" ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");

            var mat = new Material(shader);
            mat.SetFloat("_HeightScale", heightScale);
            mat.SetFloat("_MinSteps", minSteps);
            mat.SetFloat("_MaxSteps", maxSteps);
            mat.SetFloat("_SelfShadow", selfShadow ? 1f : 0f);

            // Assign textures if provided and existing
            AssignTexture(mat, "_Heightmap",   p.HeightmapPath);
            AssignTexture(mat, "_MainTex",     p.AlbedoPath);
            AssignTexture(mat, "_BumpMap",     p.NormalPath);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            return ToolResult<ShaderParallaxCreateResult>.Ok(new ShaderParallaxCreateResult
            {
                ShaderPath   = shaderPath,
                MaterialPath = matPath,
                HeightScale  = heightScale
            });
        }

        static void AssignTexture(Material mat, string prop, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null && mat.HasProperty(prop))
                mat.SetTexture(prop, tex);
        }

        // ---------------------------------------------------------------
        // Shader generation (URP)
        // ---------------------------------------------------------------
        static string BuildUrpShader(string name, int minSteps, int maxSteps, float heightScale, bool selfShadow)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Shader \"{name}\"");
            sb.AppendLine("{");
            sb.AppendLine("    Properties");
            sb.AppendLine("    {");
            sb.AppendLine("        _MainTex (\"Albedo\", 2D) = \"white\" {}");
            sb.AppendLine("        _BumpMap (\"Normal Map\", 2D) = \"bump\" {}");
            sb.AppendLine("        _Heightmap (\"Heightmap\", 2D) = \"black\" {}");
            sb.AppendLine($"        _HeightScale (\"Height Scale\", Range(0.0, 0.5)) = {heightScale:F4}");
            sb.AppendLine($"        _MinSteps (\"Min Steps\", Range(1, 64)) = {minSteps}");
            sb.AppendLine($"        _MaxSteps (\"Max Steps\", Range(1, 128)) = {maxSteps}");
            sb.AppendLine($"        [Toggle] _SelfShadow (\"Self Shadow\", Float) = {(selfShadow ? 1 : 0)}");
            sb.AppendLine("    }");
            sb.AppendLine("    SubShader");
            sb.AppendLine("    {");
            sb.AppendLine("        Tags { \"RenderType\"=\"Opaque\" \"RenderPipeline\"=\"UniversalPipeline\" \"Queue\"=\"Geometry\" }");
            sb.AppendLine("        LOD 300");
            sb.AppendLine("        Pass");
            sb.AppendLine("        {");
            sb.AppendLine("            Name \"ForwardLit\"");
            sb.AppendLine("            Tags { \"LightMode\"=\"UniversalForward\" }");
            sb.AppendLine("            HLSLPROGRAM");
            sb.AppendLine("            #pragma vertex vert");
            sb.AppendLine("            #pragma fragment frag");
            sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"");
            sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl\"");
            sb.AppendLine();
            AppendProps(sb);
            AppendStructsUrp(sb);
            sb.AppendLine();
            AppendPomFunctions(sb, selfShadow);
            sb.AppendLine();
            sb.AppendLine("            Varyings vert(Attributes IN)");
            sb.AppendLine("            {");
            sb.AppendLine("                Varyings OUT;");
            sb.AppendLine("                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);");
            sb.AppendLine("                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);");
            sb.AppendLine("                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);");
            sb.AppendLine("                float3 tangentWS   = TransformObjectToWorldDir(IN.tangentOS.xyz);");
            sb.AppendLine("                float3 bitangentWS = cross(OUT.normalWS, tangentWS) * IN.tangentOS.w;");
            sb.AppendLine("                OUT.tangentWS    = tangentWS;");
            sb.AppendLine("                OUT.bitangentWS  = bitangentWS;");
            sb.AppendLine("                OUT.uv           = IN.uv;");
            sb.AppendLine("                return OUT;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            half4 frag(Varyings IN) : SV_Target");
            sb.AppendLine("            {");
            sb.AppendLine("                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));");
            sb.AppendLine("                float3x3 TBN = float3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), normalize(IN.normalWS));");
            sb.AppendLine("                float3 viewDirTS = mul(TBN, viewDirWS);");
            sb.AppendLine("                float2 uv = ParallaxOcclusion(IN.uv, viewDirTS);");
            sb.AppendLine("                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;");
            sb.AppendLine("                half3 nrmTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv));");
            sb.AppendLine("                float3 nrmWS = normalize(mul(nrmTS, TBN));");
            sb.AppendLine("                Light mainLight = GetMainLight();");
            sb.AppendLine("                float3 lightDirWS = normalize(mainLight.direction);");
            sb.AppendLine("                float3 lightDirTS = mul(TBN, lightDirWS);");
            sb.AppendLine("                float NdotL = saturate(dot(nrmWS, lightDirWS));");
            sb.AppendLine("                float shadow = 1.0;");
            if (selfShadow)
            {
                sb.AppendLine("                if (_SelfShadow > 0.5) shadow = ParallaxShadow(uv, lightDirTS);");
            }
            sb.AppendLine("                half3 color = albedo * (NdotL * shadow * mainLight.color + SampleSH(nrmWS));");
            sb.AppendLine("                return half4(color, 1.0);");
            sb.AppendLine("            }");
            sb.AppendLine("            ENDHLSL");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    FallBack \"Universal Render Pipeline/Lit\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // Shader generation (Built-in)
        // ---------------------------------------------------------------
        static string BuildBuiltinShader(string name, int minSteps, int maxSteps, float heightScale, bool selfShadow)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Shader \"{name}\"");
            sb.AppendLine("{");
            sb.AppendLine("    Properties");
            sb.AppendLine("    {");
            sb.AppendLine("        _MainTex (\"Albedo\", 2D) = \"white\" {}");
            sb.AppendLine("        _BumpMap (\"Normal Map\", 2D) = \"bump\" {}");
            sb.AppendLine("        _Heightmap (\"Heightmap\", 2D) = \"black\" {}");
            sb.AppendLine($"        _HeightScale (\"Height Scale\", Range(0.0, 0.5)) = {heightScale:F4}");
            sb.AppendLine($"        _MinSteps (\"Min Steps\", Range(1, 64)) = {minSteps}");
            sb.AppendLine($"        _MaxSteps (\"Max Steps\", Range(1, 128)) = {maxSteps}");
            sb.AppendLine($"        [Toggle] _SelfShadow (\"Self Shadow\", Float) = {(selfShadow ? 1 : 0)}");
            sb.AppendLine("    }");
            sb.AppendLine("    SubShader");
            sb.AppendLine("    {");
            sb.AppendLine("        Tags { \"RenderType\"=\"Opaque\" \"Queue\"=\"Geometry\" }");
            sb.AppendLine("        LOD 300");
            sb.AppendLine("        CGPROGRAM");
            sb.AppendLine("        #pragma surface surf Standard fullforwardshadows vertex:vert");
            sb.AppendLine("        #pragma target 3.5");
            sb.AppendLine();
            sb.AppendLine("        sampler2D _MainTex;");
            sb.AppendLine("        sampler2D _BumpMap;");
            sb.AppendLine("        sampler2D _Heightmap;");
            sb.AppendLine("        float _HeightScale;");
            sb.AppendLine("        float _MinSteps;");
            sb.AppendLine("        float _MaxSteps;");
            sb.AppendLine("        float _SelfShadow;");
            sb.AppendLine();
            sb.AppendLine("        struct Input");
            sb.AppendLine("        {");
            sb.AppendLine("            float2 uv_MainTex;");
            sb.AppendLine("            float3 viewDirTS;");
            sb.AppendLine("            float3 lightDirTS;");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        void vert(inout appdata_full v, out Input o)");
            sb.AppendLine("        {");
            sb.AppendLine("            UNITY_INITIALIZE_OUTPUT(Input, o);");
            sb.AppendLine("            float3 bitangent = cross(v.normal, v.tangent.xyz) * v.tangent.w;");
            sb.AppendLine("            float3x3 rotation = float3x3(v.tangent.xyz, bitangent, v.normal);");
            sb.AppendLine("            float3 viewDirOS  = ObjSpaceViewDir(v.vertex);");
            sb.AppendLine("            float3 lightDirOS = ObjSpaceLightDir(v.vertex);");
            sb.AppendLine("            o.viewDirTS  = mul(rotation, viewDirOS);");
            sb.AppendLine("            o.lightDirTS = mul(rotation, lightDirOS);");
            sb.AppendLine("        }");
            sb.AppendLine();
            AppendPomFunctionsBuiltin(sb, selfShadow);
            sb.AppendLine();
            sb.AppendLine("        void surf(Input IN, inout SurfaceOutputStandard o)");
            sb.AppendLine("        {");
            sb.AppendLine("            float3 viewDirTS = normalize(IN.viewDirTS);");
            sb.AppendLine("            float2 uv = ParallaxOcclusion(IN.uv_MainTex, viewDirTS);");
            sb.AppendLine("            fixed4 c = tex2D(_MainTex, uv);");
            sb.AppendLine("            o.Albedo = c.rgb;");
            sb.AppendLine("            o.Normal = UnpackNormal(tex2D(_BumpMap, uv));");
            sb.AppendLine("            float shadow = 1.0;");
            if (selfShadow)
            {
                sb.AppendLine("            if (_SelfShadow > 0.5) shadow = ParallaxShadow(uv, normalize(IN.lightDirTS));");
            }
            sb.AppendLine("            o.Albedo *= shadow;");
            sb.AppendLine("            o.Alpha = c.a;");
            sb.AppendLine("        }");
            sb.AppendLine("        ENDCG");
            sb.AppendLine("    }");
            sb.AppendLine("    FallBack \"Diffuse\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // URP property block + structs
        static void AppendProps(StringBuilder sb)
        {
            sb.AppendLine("            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);");
            sb.AppendLine("            TEXTURE2D(_BumpMap);   SAMPLER(sampler_BumpMap);");
            sb.AppendLine("            TEXTURE2D(_Heightmap); SAMPLER(sampler_Heightmap);");
            sb.AppendLine("            CBUFFER_START(UnityPerMaterial)");
            sb.AppendLine("                float4 _MainTex_ST;");
            sb.AppendLine("                float  _HeightScale;");
            sb.AppendLine("                float  _MinSteps;");
            sb.AppendLine("                float  _MaxSteps;");
            sb.AppendLine("                float  _SelfShadow;");
            sb.AppendLine("            CBUFFER_END");
        }

        static void AppendStructsUrp(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("            struct Attributes");
            sb.AppendLine("            {");
            sb.AppendLine("                float4 positionOS : POSITION;");
            sb.AppendLine("                float3 normalOS   : NORMAL;");
            sb.AppendLine("                float4 tangentOS  : TANGENT;");
            sb.AppendLine("                float2 uv         : TEXCOORD0;");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            struct Varyings");
            sb.AppendLine("            {");
            sb.AppendLine("                float4 positionHCS : SV_POSITION;");
            sb.AppendLine("                float3 positionWS  : TEXCOORD0;");
            sb.AppendLine("                float3 normalWS    : TEXCOORD1;");
            sb.AppendLine("                float3 tangentWS   : TEXCOORD2;");
            sb.AppendLine("                float3 bitangentWS : TEXCOORD3;");
            sb.AppendLine("                float2 uv          : TEXCOORD4;");
            sb.AppendLine("            };");
        }

        // POM + shadow (URP/HLSL)
        static void AppendPomFunctions(StringBuilder sb, bool selfShadow)
        {
            sb.AppendLine("            // Steep parallax + occlusion refinement (Tatarchuk 2006)");
            sb.AppendLine("            float2 ParallaxOcclusion(float2 uv, float3 viewDirTS)");
            sb.AppendLine("            {");
            sb.AppendLine("                float nSteps = lerp(_MaxSteps, _MinSteps, abs(viewDirTS.z));");
            sb.AppendLine("                float stepDepth = 1.0 / nSteps;");
            sb.AppendLine("                float2 uvStep = viewDirTS.xy * _HeightScale / (nSteps * max(abs(viewDirTS.z), 1e-4));");
            sb.AppendLine("                float currentDepth = 0.0;");
            sb.AppendLine("                float2 currentUV = uv;");
            sb.AppendLine("                float currentHeight = SAMPLE_TEXTURE2D(_Heightmap, sampler_Heightmap, currentUV).r;");
            sb.AppendLine("                [loop] for (int i = 0; i < 128; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (currentDepth >= currentHeight) break;");
            sb.AppendLine("                    currentUV -= uvStep;");
            sb.AppendLine("                    currentDepth += stepDepth;");
            sb.AppendLine("                    currentHeight = SAMPLE_TEXTURE2D(_Heightmap, sampler_Heightmap, currentUV).r;");
            sb.AppendLine("                }");
            sb.AppendLine("                float2 prevUV = currentUV + uvStep;");
            sb.AppendLine("                float afterDepth  = currentHeight - currentDepth;");
            sb.AppendLine("                float beforeDepth = SAMPLE_TEXTURE2D(_Heightmap, sampler_Heightmap, prevUV).r - (currentDepth - stepDepth);");
            sb.AppendLine("                float t = afterDepth / (afterDepth - beforeDepth + 1e-5);");
            sb.AppendLine("                return lerp(currentUV, prevUV, t);");
            sb.AppendLine("            }");
            if (selfShadow)
            {
                sb.AppendLine();
                sb.AppendLine("            // Soft self-shadow by marching toward the light");
                sb.AppendLine("            float ParallaxShadow(float2 uv, float3 lightDirTS)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (lightDirTS.z <= 0.0) return 0.0;");
                sb.AppendLine("                float nSteps = _MinSteps;");
                sb.AppendLine("                float stepDepth = 1.0 / nSteps;");
                sb.AppendLine("                float2 uvStep = lightDirTS.xy * _HeightScale / (nSteps * max(abs(lightDirTS.z), 1e-4));");
                sb.AppendLine("                float currentDepth = SAMPLE_TEXTURE2D(_Heightmap, sampler_Heightmap, uv).r;");
                sb.AppendLine("                float2 currentUV = uv;");
                sb.AppendLine("                float shadow = 1.0;");
                sb.AppendLine("                [loop] for (int i = 0; i < 64; i++)");
                sb.AppendLine("                {");
                sb.AppendLine("                    currentUV += uvStep;");
                sb.AppendLine("                    currentDepth -= stepDepth;");
                sb.AppendLine("                    if (currentDepth <= 0.0) break;");
                sb.AppendLine("                    float h = SAMPLE_TEXTURE2D(_Heightmap, sampler_Heightmap, currentUV).r;");
                sb.AppendLine("                    if (h > currentDepth) shadow = min(shadow, (currentDepth - h) * 8.0 + 1.0);");
                sb.AppendLine("                }");
                sb.AppendLine("                return saturate(shadow);");
                sb.AppendLine("            }");
            }
        }

        // POM + shadow (Built-in surface shader / CG)
        static void AppendPomFunctionsBuiltin(StringBuilder sb, bool selfShadow)
        {
            sb.AppendLine("        float2 ParallaxOcclusion(float2 uv, float3 viewDirTS)");
            sb.AppendLine("        {");
            sb.AppendLine("            float nSteps = lerp(_MaxSteps, _MinSteps, abs(viewDirTS.z));");
            sb.AppendLine("            float stepDepth = 1.0 / nSteps;");
            sb.AppendLine("            float2 uvStep = viewDirTS.xy * _HeightScale / (nSteps * max(abs(viewDirTS.z), 1e-4));");
            sb.AppendLine("            float currentDepth = 0.0;");
            sb.AppendLine("            float2 currentUV = uv;");
            sb.AppendLine("            float currentHeight = tex2D(_Heightmap, currentUV).r;");
            sb.AppendLine("            [loop] for (int i = 0; i < 128; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (currentDepth >= currentHeight) break;");
            sb.AppendLine("                currentUV -= uvStep;");
            sb.AppendLine("                currentDepth += stepDepth;");
            sb.AppendLine("                currentHeight = tex2D(_Heightmap, currentUV).r;");
            sb.AppendLine("            }");
            sb.AppendLine("            float2 prevUV = currentUV + uvStep;");
            sb.AppendLine("            float afterDepth  = currentHeight - currentDepth;");
            sb.AppendLine("            float beforeDepth = tex2D(_Heightmap, prevUV).r - (currentDepth - stepDepth);");
            sb.AppendLine("            float t = afterDepth / (afterDepth - beforeDepth + 1e-5);");
            sb.AppendLine("            return lerp(currentUV, prevUV, t);");
            sb.AppendLine("        }");
            if (selfShadow)
            {
                sb.AppendLine();
                sb.AppendLine("        float ParallaxShadow(float2 uv, float3 lightDirTS)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (lightDirTS.z <= 0.0) return 0.0;");
                sb.AppendLine("            float nSteps = _MinSteps;");
                sb.AppendLine("            float stepDepth = 1.0 / nSteps;");
                sb.AppendLine("            float2 uvStep = lightDirTS.xy * _HeightScale / (nSteps * max(abs(lightDirTS.z), 1e-4));");
                sb.AppendLine("            float currentDepth = tex2D(_Heightmap, uv).r;");
                sb.AppendLine("            float2 currentUV = uv;");
                sb.AppendLine("            float shadow = 1.0;");
                sb.AppendLine("            [loop] for (int i = 0; i < 64; i++)");
                sb.AppendLine("            {");
                sb.AppendLine("                currentUV += uvStep;");
                sb.AppendLine("                currentDepth -= stepDepth;");
                sb.AppendLine("                if (currentDepth <= 0.0) break;");
                sb.AppendLine("                float h = tex2D(_Heightmap, currentUV).r;");
                sb.AppendLine("                if (h > currentDepth) shadow = min(shadow, (currentDepth - h) * 8.0 + 1.0);");
                sb.AppendLine("            }");
                sb.AppendLine("            return saturate(shadow);");
                sb.AppendLine("        }");
            }
        }
    }
}
