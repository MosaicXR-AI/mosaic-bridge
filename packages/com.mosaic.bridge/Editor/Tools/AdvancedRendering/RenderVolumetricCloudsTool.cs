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
    public static class RenderVolumetricCloudsTool
    {
        [MosaicTool("render/volumetric-clouds",
                    "Generates volumetric cloud rendering setup with compute noise, ray marching shader, and controller",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<RenderVolumetricCloudsResult> Execute(RenderVolumetricCloudsParams p)
        {
            var density       = p.CloudDensity ?? 0.5f;
            var windDir       = p.WindDirection ?? new[] { 1f, 0f, 0f };
            var windSpeed     = p.WindSpeed ?? 1f;
            var absorption    = p.LightAbsorption ?? 0.5f;
            var detailScale   = p.DetailNoiseScale ?? 3f;
            var shapeScale    = p.ShapeNoiseScale ?? 0.3f;
            var cloudMin      = p.CloudMinHeight ?? 1000f;
            var cloudMax      = p.CloudMaxHeight ?? 3000f;
            var raySteps      = p.RaySteps ?? 64;
            var lightSteps    = p.LightSteps ?? 8;
            var outputDir     = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/Rendering/VolumetricClouds"
                : p.OutputDirectory;

            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<RenderVolumetricCloudsResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (windDir.Length != 3)
                return ToolResult<RenderVolumetricCloudsResult>.Fail(
                    "WindDirection must have exactly 3 elements (x,y,z)", ErrorCodes.INVALID_PARAM);

            var projectRoot   = Application.dataPath.Replace("/Assets", "");
            var computePath   = Path.Combine(outputDir, "CloudNoise.compute").Replace("\\", "/");
            var shaderPath    = Path.Combine(outputDir, "Clouds.shader").Replace("\\", "/");
            var controllerPath = Path.Combine(outputDir, "CloudController.cs").Replace("\\", "/");

            string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

            // --- Cloud Noise Compute Shader ---
            var computeSrc = @"#pragma kernel GenerateNoise3D

RWTexture3D<float4> Result;
uint Resolution;
float Scale;

float hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise3D(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    return lerp(
        lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
             lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
        lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
             lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y),
        f.z);
}

float fbm(float3 p, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * noise3D(p * frequency);
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    return value;
}

[numthreads(8,8,8)]
void GenerateNoise3D(uint3 id : SV_DispatchThreadID)
{
    if (any(id >= Resolution)) return;
    float3 uvw = (float3)id / (float)Resolution;
    float shape = fbm(uvw * Scale, 4);
    float detail = fbm(uvw * Scale * 4.0, 6);
    float worley = 1.0 - noise3D(uvw * Scale * 2.0);
    Result[id] = float4(shape, detail, worley, 1.0);
}
";

            // --- Cloud Shader ---
            var shaderSrc = $@"Shader ""Mosaic/VolumetricClouds""
{{
    Properties
    {{
        _NoiseTex (""3D Noise"", 3D) = ""white"" {{}}
        _CloudDensity (""Cloud Density"", Float) = {F(density)}
        _LightAbsorption (""Light Absorption"", Float) = {F(absorption)}
        _CloudMinHeight (""Cloud Min Height"", Float) = {F(cloudMin)}
        _CloudMaxHeight (""Cloud Max Height"", Float) = {F(cloudMax)}
        _WindDir (""Wind Direction"", Vector) = ({F(windDir[0])}, {F(windDir[1])}, {F(windDir[2])}, 0)
        _WindSpeed (""Wind Speed"", Float) = {F(windSpeed)}
        _ShapeScale (""Shape Scale"", Float) = {F(shapeScale)}
        _DetailScale (""Detail Scale"", Float) = {F(detailScale)}
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Transparent"" ""Queue""=""Transparent"" }}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler3D _NoiseTex;
            float _CloudDensity;
            float _LightAbsorption;
            float _CloudMinHeight;
            float _CloudMaxHeight;
            float4 _WindDir;
            float _WindSpeed;
            float _ShapeScale;
            float _DetailScale;

            #define RAY_STEPS {raySteps}
            #define LIGHT_STEPS {lightSteps}
            #define PI 3.14159265359

            struct appdata {{ float4 vertex : POSITION; float2 uv : TEXCOORD0; }};
            struct v2f {{ float4 vertex : SV_POSITION; float3 worldPos : TEXCOORD0; float3 viewDir : TEXCOORD1; }};

            v2f vert(appdata v)
            {{
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                return o;
            }}

            float sampleDensity(float3 pos)
            {{
                float heightFraction = saturate((pos.y - _CloudMinHeight) / (_CloudMaxHeight - _CloudMinHeight));
                float heightGradient = heightFraction * (1.0 - heightFraction) * 4.0;

                float3 windOffset = _WindDir.xyz * _WindSpeed * _Time.y;
                float3 samplePos = (pos + windOffset) * _ShapeScale * 0.001;
                float4 noise = tex3Dlod(_NoiseTex, float4(samplePos, 0));

                float shape = noise.r;
                float detail = noise.g * _DetailScale;
                float d = saturate(shape - detail * 0.3) * heightGradient * _CloudDensity;
                return max(d, 0.0);
            }}

            // Beer-Lambert absorption
            float beerLambert(float density) {{ return exp(-density * _LightAbsorption); }}

            // Henyey-Greenstein phase function
            float hgPhase(float cosTheta, float g)
            {{
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
            }}

            float lightMarch(float3 pos)
            {{
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float stepSize = (_CloudMaxHeight - pos.y) / LIGHT_STEPS;
                float totalDensity = 0;
                for (int i = 0; i < LIGHT_STEPS; i++)
                {{
                    pos += lightDir * stepSize;
                    if (pos.y < _CloudMinHeight || pos.y > _CloudMaxHeight) break;
                    totalDensity += sampleDensity(pos) * stepSize;
                }}
                return beerLambert(totalDensity);
            }}

            fixed4 frag(v2f i) : SV_Target
            {{
                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(-i.viewDir);

                // Find intersection with cloud layer
                float tMin = (_CloudMinHeight - ro.y) / rd.y;
                float tMax = (_CloudMaxHeight - ro.y) / rd.y;
                if (tMin > tMax) {{ float tmp = tMin; tMin = tMax; tMax = tmp; }}
                tMin = max(tMin, 0);
                if (tMin >= tMax) return fixed4(0, 0, 0, 0);

                float stepSize = (tMax - tMin) / RAY_STEPS;
                float transmittance = 1.0;
                float3 lightEnergy = 0;
                float cosTheta = dot(rd, normalize(_WorldSpaceLightPos0.xyz));
                float phase = hgPhase(cosTheta, 0.3) + hgPhase(cosTheta, -0.3) * 0.5;

                for (int s = 0; s < RAY_STEPS; s++)
                {{
                    float3 pos = ro + rd * (tMin + (s + 0.5) * stepSize);
                    if (pos.y < _CloudMinHeight || pos.y > _CloudMaxHeight) continue;

                    float d = sampleDensity(pos) * stepSize;
                    if (d > 0.001)
                    {{
                        float lightTransmit = lightMarch(pos);
                        lightEnergy += d * transmittance * lightTransmit * phase;
                        transmittance *= beerLambert(d);
                        if (transmittance < 0.01) break;
                    }}
                }}

                float3 cloudColor = lightEnergy * float3(1, 1, 1) + float3(0.6, 0.7, 0.8) * (1.0 - transmittance) * 0.2;
                float alpha = 1.0 - transmittance;
                return fixed4(cloudColor, alpha);
            }}
            ENDCG
        }}
    }}
}}";

            // --- CloudController.cs ---
            var controllerSrc = $@"using UnityEngine;

/// <summary>
/// Controls volumetric cloud parameters at runtime.
/// Syncs public fields to the cloud shader material.
/// </summary>
public class CloudController : MonoBehaviour
{{
    [Header(""Cloud Shape"")]
    public float cloudDensity = {F(density)}f;
    public float shapeNoiseScale = {F(shapeScale)}f;
    public float detailNoiseScale = {F(detailScale)}f;
    public float cloudMinHeight = {F(cloudMin)}f;
    public float cloudMaxHeight = {F(cloudMax)}f;

    [Header(""Wind"")]
    public Vector3 windDirection = new Vector3({F(windDir[0])}f, {F(windDir[1])}f, {F(windDir[2])}f);
    public float windSpeed = {F(windSpeed)}f;

    [Header(""Lighting"")]
    public float lightAbsorption = {F(absorption)}f;

    [Header(""References"")]
    public Material cloudMaterial;

    void Update()
    {{
        if (cloudMaterial == null) return;

        cloudMaterial.SetFloat(""_CloudDensity"", cloudDensity);
        cloudMaterial.SetFloat(""_LightAbsorption"", lightAbsorption);
        cloudMaterial.SetFloat(""_CloudMinHeight"", cloudMinHeight);
        cloudMaterial.SetFloat(""_CloudMaxHeight"", cloudMaxHeight);
        cloudMaterial.SetVector(""_WindDir"", new Vector4(windDirection.x, windDirection.y, windDirection.z, 0));
        cloudMaterial.SetFloat(""_WindSpeed"", windSpeed);
        cloudMaterial.SetFloat(""_ShapeScale"", shapeNoiseScale);
        cloudMaterial.SetFloat(""_DetailScale"", detailNoiseScale);
    }}
}}";

            WriteFile(projectRoot, computePath, computeSrc);
            WriteFile(projectRoot, shaderPath, shaderSrc);
            WriteFile(projectRoot, controllerPath, controllerSrc);

            return ToolResult<RenderVolumetricCloudsResult>.Ok(new RenderVolumetricCloudsResult
            {
                ComputeShaderPath    = computePath,
                ShaderPath           = shaderPath,
                ControllerScriptPath = controllerPath
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
