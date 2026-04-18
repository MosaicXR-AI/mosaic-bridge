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
    public static class RenderAtmosphereTool
    {
        [MosaicTool("render/atmosphere",
                    "Generates an atmospheric scattering shader with Rayleigh and Mie scattering plus a controller script",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<RenderAtmosphereResult> Execute(RenderAtmosphereParams p)
        {
            var planetRadius     = p.PlanetRadius ?? 6371000f;
            var atmosphereHeight = p.AtmosphereHeight ?? 100000f;
            var rayleighScale    = p.RayleighScaleHeight ?? 8500f;
            var mieScale         = p.MieScaleHeight ?? 1200f;
            var sunIntensity     = p.SunIntensity ?? 20f;
            var rayleighCoeff    = p.RayleighCoefficients ?? new[] { 5.8e-6f, 13.5e-6f, 33.1e-6f };
            var mieCoeff         = p.MieCoefficient ?? 21e-6f;
            var outputDir        = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/Rendering/Atmosphere"
                : p.OutputDirectory;

            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<RenderAtmosphereResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (rayleighCoeff.Length != 3)
                return ToolResult<RenderAtmosphereResult>.Fail(
                    "RayleighCoefficients must have exactly 3 elements (RGB)", ErrorCodes.INVALID_PARAM);

            var projectRoot    = Application.dataPath.Replace("/Assets", "");
            var shaderPath     = Path.Combine(outputDir, "Atmosphere.shader").Replace("\\", "/");
            var controllerPath = Path.Combine(outputDir, "AtmosphereController.cs").Replace("\\", "/");

            string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

            // --- Atmosphere Shader ---
            var shaderSrc = $@"Shader ""Mosaic/Atmosphere""
{{
    Properties
    {{
        _PlanetRadius (""Planet Radius"", Float) = {F(planetRadius)}
        _AtmosphereRadius (""Atmosphere Radius"", Float) = {F(planetRadius + atmosphereHeight)}
        _RayleighScaleHeight (""Rayleigh Scale Height"", Float) = {F(rayleighScale)}
        _MieScaleHeight (""Mie Scale Height"", Float) = {F(mieScale)}
        _SunIntensity (""Sun Intensity"", Float) = {F(sunIntensity)}
        _RayleighCoeff (""Rayleigh Coefficients"", Vector) = ({F(rayleighCoeff[0])}, {F(rayleighCoeff[1])}, {F(rayleighCoeff[2])}, 0)
        _MieCoeff (""Mie Coefficient"", Float) = {F(mieCoeff)}
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Background"" ""Queue""=""Background"" }}
        Pass
        {{
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            float _PlanetRadius;
            float _AtmosphereRadius;
            float _RayleighScaleHeight;
            float _MieScaleHeight;
            float _SunIntensity;
            float4 _RayleighCoeff;
            float _MieCoeff;

            #define PI 3.14159265359
            #define NUM_SCATTER_STEPS 16
            #define NUM_OPTICAL_STEPS 8

            struct appdata {{ float4 vertex : POSITION; }};
            struct v2f {{ float4 vertex : SV_POSITION; float3 viewDir : TEXCOORD0; }};

            v2f vert(appdata v)
            {{
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.viewDir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }}

            float2 raySphereIntersect(float3 ro, float3 rd, float radius)
            {{
                float b = dot(ro, rd);
                float c = dot(ro, ro) - radius * radius;
                float disc = b * b - c;
                if (disc < 0.0) return float2(1e5, -1e5);
                float sq = sqrt(disc);
                return float2(-b - sq, -b + sq);
            }}

            float rayleighPhase(float cosTheta)
            {{
                return 3.0 / (16.0 * PI) * (1.0 + cosTheta * cosTheta);
            }}

            float miePhase(float cosTheta, float g)
            {{
                float g2 = g * g;
                return 3.0 / (8.0 * PI) * ((1.0 - g2) * (1.0 + cosTheta * cosTheta))
                    / (pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5) * (2.0 + g2));
            }}

            fixed4 frag(v2f i) : SV_Target
            {{
                float3 rd = normalize(i.viewDir);
                float3 ro = float3(0, _PlanetRadius + 1.0, 0);
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);

                float2 atmoHit = raySphereIntersect(ro, rd, _AtmosphereRadius);
                if (atmoHit.x > atmoHit.y) return fixed4(0, 0, 0, 1);

                float tStart = max(atmoHit.x, 0.0);
                float tEnd = atmoHit.y;
                float stepSize = (tEnd - tStart) / NUM_SCATTER_STEPS;

                float3 rayleighScatter = 0;
                float3 mieScatter = 0;
                float rayleighOptical = 0;
                float mieOptical = 0;

                for (int s = 0; s < NUM_SCATTER_STEPS; s++)
                {{
                    float t = tStart + (s + 0.5) * stepSize;
                    float3 pos = ro + rd * t;
                    float height = length(pos) - _PlanetRadius;

                    float rayleighDensity = exp(-height / _RayleighScaleHeight) * stepSize;
                    float mieDensity = exp(-height / _MieScaleHeight) * stepSize;

                    rayleighOptical += rayleighDensity;
                    mieOptical += mieDensity;

                    // Light optical depth
                    float2 sunHit = raySphereIntersect(pos, sunDir, _AtmosphereRadius);
                    float sunStepSize = sunHit.y / NUM_OPTICAL_STEPS;
                    float rayleighOpticalLight = 0;
                    float mieOpticalLight = 0;

                    for (int j = 0; j < NUM_OPTICAL_STEPS; j++)
                    {{
                        float3 lightPos = pos + sunDir * (j + 0.5) * sunStepSize;
                        float lightHeight = length(lightPos) - _PlanetRadius;
                        rayleighOpticalLight += exp(-lightHeight / _RayleighScaleHeight) * sunStepSize;
                        mieOpticalLight += exp(-lightHeight / _MieScaleHeight) * sunStepSize;
                    }}

                    float3 tau = _RayleighCoeff.rgb * (rayleighOptical + rayleighOpticalLight)
                               + _MieCoeff * 1.1 * (mieOptical + mieOpticalLight);
                    float3 attenuation = exp(-tau);

                    rayleighScatter += rayleighDensity * attenuation;
                    mieScatter += mieDensity * attenuation;
                }}

                float cosTheta = dot(rd, sunDir);
                float3 color = (rayleighScatter * _RayleighCoeff.rgb * rayleighPhase(cosTheta)
                             + mieScatter * _MieCoeff * miePhase(cosTheta, 0.758))
                             * _SunIntensity;

                color = 1.0 - exp(-color);
                return fixed4(color, 1);
            }}
            ENDCG
        }}
    }}
}}";

            // --- AtmosphereController.cs ---
            var controllerSrc = $@"using UnityEngine;

/// <summary>
/// Controller for atmospheric scattering parameters. Attach to a skybox or camera.
/// Syncs public fields to the Atmosphere shader material at runtime.
/// </summary>
public class AtmosphereController : MonoBehaviour
{{
    [Header(""Planet"")]
    public float planetRadius = {F(planetRadius)}f;
    public float atmosphereHeight = {F(atmosphereHeight)}f;

    [Header(""Scattering"")]
    public float rayleighScaleHeight = {F(rayleighScale)}f;
    public float mieScaleHeight = {F(mieScale)}f;
    public float sunIntensity = {F(sunIntensity)}f;
    public Vector3 rayleighCoefficients = new Vector3({F(rayleighCoeff[0])}f, {F(rayleighCoeff[1])}f, {F(rayleighCoeff[2])}f);
    public float mieCoefficient = {F(mieCoeff)}f;

    [Header(""References"")]
    public Material atmosphereMaterial;

    void Update()
    {{
        if (atmosphereMaterial == null) return;

        atmosphereMaterial.SetFloat(""_PlanetRadius"", planetRadius);
        atmosphereMaterial.SetFloat(""_AtmosphereRadius"", planetRadius + atmosphereHeight);
        atmosphereMaterial.SetFloat(""_RayleighScaleHeight"", rayleighScaleHeight);
        atmosphereMaterial.SetFloat(""_MieScaleHeight"", mieScaleHeight);
        atmosphereMaterial.SetFloat(""_SunIntensity"", sunIntensity);
        atmosphereMaterial.SetVector(""_RayleighCoeff"", new Vector4(rayleighCoefficients.x, rayleighCoefficients.y, rayleighCoefficients.z, 0));
        atmosphereMaterial.SetFloat(""_MieCoeff"", mieCoefficient);
    }}
}}";

            WriteFile(projectRoot, shaderPath, shaderSrc);
            WriteFile(projectRoot, controllerPath, controllerSrc);

            return ToolResult<RenderAtmosphereResult>.Ok(new RenderAtmosphereResult
            {
                ShaderPath           = shaderPath,
                ControllerScriptPath = controllerPath,
                PlanetRadius         = planetRadius,
                AtmosphereHeight     = atmosphereHeight
            });
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
