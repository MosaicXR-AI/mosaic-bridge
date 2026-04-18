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
    /// Generates a screen-space Subsurface Scattering (SSS) shader and material.
    /// Based on Jimenez 2015 simplified SSS: wrap diffuse + pre-integrated tint +
    /// thickness-based transmission term.
    /// Supports URP and Built-in render pipelines.
    /// </summary>
    public static class ShaderSssCreateTool
    {
        static readonly string[] ValidPipelines = { "urp", "builtin" };
        static readonly string[] ValidProfiles  = { "skin", "foliage", "wax", "ice", "custom" };

        [MosaicTool("shader/sss-create",
                    "Generates a subsurface scattering shader + material (Jimenez 2015 simplified) with wrap-diffuse and thickness-based transmission",
                    isReadOnly: false, category: "shader", Context = ToolContext.Both)]
        public static ToolResult<ShaderSssCreateResult> Execute(ShaderSssCreateParams p)
        {
            p ??= new ShaderSssCreateParams();

            var profile    = string.IsNullOrEmpty(p.Profile)  ? "skin" : p.Profile.ToLowerInvariant();
            var pipeline   = string.IsNullOrEmpty(p.Pipeline) ? "urp"  : p.Pipeline.ToLowerInvariant();
            var savePath   = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Rendering/" : p.SavePath;
            var outputName = string.IsNullOrEmpty(p.OutputName) ? "SubsurfaceScattering" : p.OutputName;

            // Validate profile
            bool validProfile = false;
            foreach (var vp in ValidProfiles)
                if (vp == profile) { validProfile = true; break; }
            if (!validProfile)
                return ToolResult<ShaderSssCreateResult>.Fail(
                    $"Invalid profile '{p.Profile}'. Valid: skin, foliage, wax, ice, custom",
                    ErrorCodes.INVALID_PARAM);

            // Validate pipeline
            bool validPipeline = false;
            foreach (var vp in ValidPipelines)
                if (vp == pipeline) { validPipeline = true; break; }
            if (!validPipeline)
                return ToolResult<ShaderSssCreateResult>.Fail(
                    $"Invalid pipeline '{p.Pipeline}'. Valid: urp, builtin",
                    ErrorCodes.INVALID_PARAM);

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<ShaderSssCreateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            // Resolve preset defaults
            float scatterDistance;
            float wrap;
            float[] scatterColor;
            switch (profile)
            {
                case "skin":
                    scatterDistance = 0.01f;
                    wrap            = 0.5f;
                    scatterColor    = new[] { 0.9f, 0.5f, 0.3f };
                    break;
                case "foliage":
                    scatterDistance = 0.005f;
                    wrap            = 0.3f;
                    scatterColor    = new[] { 0.5f, 0.8f, 0.3f };
                    break;
                case "wax":
                    scatterDistance = 0.02f;
                    wrap            = 0.5f;
                    scatterColor    = new[] { 1.0f, 0.9f, 0.7f };
                    break;
                case "ice":
                    scatterDistance = 0.03f;
                    wrap            = 0.5f;
                    scatterColor    = new[] { 0.8f, 0.9f, 1.0f };
                    break;
                default: // custom
                    scatterDistance = 0.01f;
                    wrap            = 0.5f;
                    scatterColor    = new[] { 0.9f, 0.5f, 0.3f };
                    break;
            }

            // Apply user overrides on top of preset
            if (p.ScatterDistance.HasValue) scatterDistance = p.ScatterDistance.Value;
            if (p.ScatterColor != null && p.ScatterColor.Length >= 3)
                scatterColor = new[] { p.ScatterColor[0], p.ScatterColor[1], p.ScatterColor[2] };
            var thickness = p.Thickness ?? 0.5f;

            if (scatterDistance <= 0f)
                return ToolResult<ShaderSssCreateResult>.Fail(
                    "ScatterDistance must be > 0", ErrorCodes.INVALID_PARAM);
            if (thickness < 0f)
                return ToolResult<ShaderSssCreateResult>.Fail(
                    "Thickness must be >= 0", ErrorCodes.INVALID_PARAM);

            var shaderName = $"Mosaic/SubsurfaceScattering_{outputName}";
            var shaderPath = (savePath + outputName + ".shader").Replace("\\", "/");
            var matPath    = (savePath + outputName + "Mat.mat").Replace("\\", "/");

            string source = pipeline == "urp"
                ? BuildUrpShader(shaderName, scatterColor, scatterDistance, thickness, wrap)
                : BuildBuiltinShader(shaderName, scatterColor, scatterDistance, thickness, wrap);

            var projectRoot    = Application.dataPath.Replace("/Assets", "");
            var fullShaderPath = Path.Combine(projectRoot, shaderPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullShaderPath));
            File.WriteAllText(fullShaderPath, source, Encoding.UTF8);
            AssetDatabase.ImportAsset(shaderPath);

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
            if (mat.HasProperty("_ScatterDistance")) mat.SetFloat("_ScatterDistance", scatterDistance);
            if (mat.HasProperty("_Thickness"))       mat.SetFloat("_Thickness", thickness);
            if (mat.HasProperty("_Wrap"))            mat.SetFloat("_Wrap", wrap);
            if (mat.HasProperty("_ScatterColor"))
                mat.SetColor("_ScatterColor", new Color(scatterColor[0], scatterColor[1], scatterColor[2], 1f));

            AssignTexture(mat, "_MainTex",      p.AlbedoPath);
            AssignTexture(mat, "_BumpMap",      p.NormalPath);
            AssignTexture(mat, "_ThicknessMap", p.ThicknessMapPath);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            return ToolResult<ShaderSssCreateResult>.Ok(new ShaderSssCreateResult
            {
                ShaderPath   = shaderPath,
                MaterialPath = matPath,
                Profile      = profile
            });
        }

        static void AssignTexture(Material mat, string prop, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null && mat.HasProperty(prop))
                mat.SetTexture(prop, tex);
        }

        static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------
        // URP shader
        // ---------------------------------------------------------------
        static string BuildUrpShader(string name, float[] sc, float dist, float thick, float wrap)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Shader \"{name}\"");
            sb.AppendLine("{");
            sb.AppendLine("    Properties");
            sb.AppendLine("    {");
            sb.AppendLine("        _MainTex (\"Albedo\", 2D) = \"white\" {}");
            sb.AppendLine("        _BumpMap (\"Normal Map\", 2D) = \"bump\" {}");
            sb.AppendLine("        _ThicknessMap (\"Thickness Map\", 2D) = \"white\" {}");
            sb.AppendLine($"        _ScatterColor (\"Scatter Color\", Color) = ({F(sc[0])}, {F(sc[1])}, {F(sc[2])}, 1.0)");
            sb.AppendLine($"        _ScatterDistance (\"Scatter Distance\", Range(0.0, 0.1)) = {F(dist)}");
            sb.AppendLine($"        _Thickness (\"Thickness\", Range(0.0, 2.0)) = {F(thick)}");
            sb.AppendLine($"        _Wrap (\"Wrap Diffuse\", Range(0.0, 1.0)) = {F(wrap)}");
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
            sb.AppendLine("            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);");
            sb.AppendLine("            TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);");
            sb.AppendLine("            TEXTURE2D(_ThicknessMap); SAMPLER(sampler_ThicknessMap);");
            sb.AppendLine("            CBUFFER_START(UnityPerMaterial)");
            sb.AppendLine("                float4 _MainTex_ST;");
            sb.AppendLine("                float4 _ScatterColor;");
            sb.AppendLine("                float  _ScatterDistance;");
            sb.AppendLine("                float  _Thickness;");
            sb.AppendLine("                float  _Wrap;");
            sb.AppendLine("            CBUFFER_END");
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
            sb.AppendLine();
            sb.AppendLine("            // Jimenez 2015 simplified SSS components");
            sb.AppendLine("            float WrappedNdotL(float3 n, float3 l, float w)");
            sb.AppendLine("            {");
            sb.AppendLine("                float ndl = dot(n, l);");
            sb.AppendLine("                return saturate((ndl + w) / (1.0 + w));");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            float3 SubsurfaceTransmission(float thickness, float3 lightDir, float3 viewDir, float3 scatterColor, float scatterDist)");
            sb.AppendLine("            {");
            sb.AppendLine("                // Back-lit transmission: light wraps around thin surfaces");
            sb.AppendLine("                float vdotL = pow(saturate(dot(-lightDir, viewDir)), 12.0);");
            sb.AppendLine("                float transmission = vdotL * exp(-thickness * 5.0 / max(scatterDist, 1e-4) * 0.05);");
            sb.AppendLine("                return scatterColor * transmission;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            Varyings vert(Attributes IN)");
            sb.AppendLine("            {");
            sb.AppendLine("                Varyings OUT;");
            sb.AppendLine("                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);");
            sb.AppendLine("                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);");
            sb.AppendLine("                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);");
            sb.AppendLine("                float3 tangentWS   = TransformObjectToWorldDir(IN.tangentOS.xyz);");
            sb.AppendLine("                float3 bitangentWS = cross(OUT.normalWS, tangentWS) * IN.tangentOS.w;");
            sb.AppendLine("                OUT.tangentWS   = tangentWS;");
            sb.AppendLine("                OUT.bitangentWS = bitangentWS;");
            sb.AppendLine("                OUT.uv          = IN.uv;");
            sb.AppendLine("                return OUT;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            half4 frag(Varyings IN) : SV_Target");
            sb.AppendLine("            {");
            sb.AppendLine("                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));");
            sb.AppendLine("                float3x3 TBN = float3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), normalize(IN.normalWS));");
            sb.AppendLine("                half3 nrmTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv));");
            sb.AppendLine("                float3 nrmWS = normalize(mul(nrmTS, TBN));");
            sb.AppendLine("                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;");
            sb.AppendLine("                float thicknessSample = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, IN.uv).r * _Thickness;");
            sb.AppendLine();
            sb.AppendLine("                Light mainLight = GetMainLight();");
            sb.AppendLine("                float3 lightDirWS = normalize(mainLight.direction);");
            sb.AppendLine();
            sb.AppendLine("                // Pre-integrated wrapped diffuse tinted by scatter color (surface bleed)");
            sb.AppendLine("                float wrapped = WrappedNdotL(nrmWS, lightDirWS, _Wrap);");
            sb.AppendLine("                float3 sssDiffuse = _ScatterColor.rgb * wrapped;");
            sb.AppendLine();
            sb.AppendLine("                // Thickness-based transmission");
            sb.AppendLine("                float3 transmission = SubsurfaceTransmission(thicknessSample, lightDirWS, viewDirWS, _ScatterColor.rgb, _ScatterDistance);");
            sb.AppendLine();
            sb.AppendLine("                half3 color = albedo * (sssDiffuse + transmission) * mainLight.color + albedo * SampleSH(nrmWS);");
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
        // Built-in shader
        // ---------------------------------------------------------------
        static string BuildBuiltinShader(string name, float[] sc, float dist, float thick, float wrap)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Shader \"{name}\"");
            sb.AppendLine("{");
            sb.AppendLine("    Properties");
            sb.AppendLine("    {");
            sb.AppendLine("        _MainTex (\"Albedo\", 2D) = \"white\" {}");
            sb.AppendLine("        _BumpMap (\"Normal Map\", 2D) = \"bump\" {}");
            sb.AppendLine("        _ThicknessMap (\"Thickness Map\", 2D) = \"white\" {}");
            sb.AppendLine($"        _ScatterColor (\"Scatter Color\", Color) = ({F(sc[0])}, {F(sc[1])}, {F(sc[2])}, 1.0)");
            sb.AppendLine($"        _ScatterDistance (\"Scatter Distance\", Range(0.0, 0.1)) = {F(dist)}");
            sb.AppendLine($"        _Thickness (\"Thickness\", Range(0.0, 2.0)) = {F(thick)}");
            sb.AppendLine($"        _Wrap (\"Wrap Diffuse\", Range(0.0, 1.0)) = {F(wrap)}");
            sb.AppendLine("    }");
            sb.AppendLine("    SubShader");
            sb.AppendLine("    {");
            sb.AppendLine("        Tags { \"RenderType\"=\"Opaque\" \"Queue\"=\"Geometry\" }");
            sb.AppendLine("        LOD 300");
            sb.AppendLine("        CGPROGRAM");
            sb.AppendLine("        #pragma surface surf SubsurfaceScatter fullforwardshadows");
            sb.AppendLine("        #pragma target 3.5");
            sb.AppendLine();
            sb.AppendLine("        sampler2D _MainTex;");
            sb.AppendLine("        sampler2D _BumpMap;");
            sb.AppendLine("        sampler2D _ThicknessMap;");
            sb.AppendLine("        float4    _ScatterColor;");
            sb.AppendLine("        float     _ScatterDistance;");
            sb.AppendLine("        float     _Thickness;");
            sb.AppendLine("        float     _Wrap;");
            sb.AppendLine();
            sb.AppendLine("        struct Input");
            sb.AppendLine("        {");
            sb.AppendLine("            float2 uv_MainTex;");
            sb.AppendLine("            float2 uv_BumpMap;");
            sb.AppendLine("            float2 uv_ThicknessMap;");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        struct SurfaceOutputSSS");
            sb.AppendLine("        {");
            sb.AppendLine("            fixed3 Albedo;");
            sb.AppendLine("            fixed3 Normal;");
            sb.AppendLine("            fixed3 Emission;");
            sb.AppendLine("            fixed  Alpha;");
            sb.AppendLine("            float  Thickness;");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        inline fixed4 LightingSubsurfaceScatter(SurfaceOutputSSS s, fixed3 lightDir, fixed3 viewDir, fixed atten)");
            sb.AppendLine("        {");
            sb.AppendLine("            float ndl = dot(s.Normal, lightDir);");
            sb.AppendLine("            float wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));");
            sb.AppendLine("            float3 sssDiffuse = _ScatterColor.rgb * wrapped;");
            sb.AppendLine("            float vdotL = pow(saturate(dot(-lightDir, viewDir)), 12.0);");
            sb.AppendLine("            float transmission = vdotL * exp(-s.Thickness * 5.0 / max(_ScatterDistance, 1e-4) * 0.05);");
            sb.AppendLine("            float3 trans = _ScatterColor.rgb * transmission;");
            sb.AppendLine("            fixed4 c;");
            sb.AppendLine("            c.rgb = s.Albedo * (sssDiffuse + trans) * _LightColor0.rgb * atten;");
            sb.AppendLine("            c.a = s.Alpha;");
            sb.AppendLine("            return c;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        void surf(Input IN, inout SurfaceOutputSSS o)");
            sb.AppendLine("        {");
            sb.AppendLine("            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);");
            sb.AppendLine("            o.Albedo = c.rgb;");
            sb.AppendLine("            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));");
            sb.AppendLine("            o.Thickness = tex2D(_ThicknessMap, IN.uv_ThicknessMap).r * _Thickness;");
            sb.AppendLine("            o.Alpha = c.a;");
            sb.AppendLine("        }");
            sb.AppendLine("        ENDCG");
            sb.AppendLine("    }");
            sb.AppendLine("    FallBack \"Diffuse\"");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
