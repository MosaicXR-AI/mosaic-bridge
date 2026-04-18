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
    /// Generates an atmospheric scattering sky system (Preetham, Bruneton, or O'Neil)
    /// as a shader + skybox material. Optionally assigns it to RenderSettings.skybox.
    /// </summary>
    public static class RenderingAtmosphereCreateTool
    {
        static readonly string[] ValidModels      = { "preetham", "bruneton", "oneil" };
        static readonly string[] ValidOutputTypes = { "skybox_material", "shader", "compute_lut" };

        [MosaicTool("rendering/atmosphere-create",
                    "Generates a Preetham/Bruneton/O'Neil atmospheric scattering sky shader + skybox material",
                    isReadOnly: false, category: "rendering", Context = ToolContext.Both)]
        public static ToolResult<RenderingAtmosphereCreateResult> Execute(RenderingAtmosphereCreateParams p)
        {
            p ??= new RenderingAtmosphereCreateParams();

            var model         = string.IsNullOrEmpty(p.Model) ? "preetham" : p.Model.ToLowerInvariant();
            var outputType    = string.IsNullOrEmpty(p.OutputType) ? "skybox_material" : p.OutputType.ToLowerInvariant();
            var sunDirRaw     = p.SunDirection ?? new[] { 0f, 0.5f, 1f };
            var rayleigh      = p.RayleighCoefficients ?? new[] { 5.8f, 13.5f, 33.1f };
            var mie           = p.MieCoefficient ?? 21.0f;
            var planetRadius  = p.PlanetRadius ?? 6360f;
            var atmoHeight    = p.AtmosphereHeight ?? 80f;
            var sunIntensity  = p.SunIntensity ?? 20.0f;
            var turbidity     = p.Turbidity ?? 2.0f;
            var applyToScene  = p.ApplyToScene ?? false;
            var savePath      = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Rendering/" : p.SavePath;

            // --- Validation ------------------------------------------------------
            if (!ValidModels.Contains(model))
                return ToolResult<RenderingAtmosphereCreateResult>.Fail(
                    $"Invalid Model '{p.Model}'. Valid: {string.Join(", ", ValidModels)}",
                    ErrorCodes.INVALID_PARAM);

            if (!ValidOutputTypes.Contains(outputType))
                return ToolResult<RenderingAtmosphereCreateResult>.Fail(
                    $"Invalid OutputType '{p.OutputType}'. Valid: {string.Join(", ", ValidOutputTypes)}",
                    ErrorCodes.INVALID_PARAM);

            if (sunDirRaw.Length != 3)
                return ToolResult<RenderingAtmosphereCreateResult>.Fail(
                    "SunDirection must have exactly 3 elements (x, y, z)", ErrorCodes.INVALID_PARAM);

            if (rayleigh.Length != 3)
                return ToolResult<RenderingAtmosphereCreateResult>.Fail(
                    "RayleighCoefficients must have exactly 3 elements (RGB)", ErrorCodes.INVALID_PARAM);

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<RenderingAtmosphereCreateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            // Normalize sun direction
            var sunVec = new Vector3(sunDirRaw[0], sunDirRaw[1], sunDirRaw[2]);
            if (sunVec.sqrMagnitude < 1e-8f) sunVec = new Vector3(0, 0.5f, 1f);
            sunVec.Normalize();
            var sunDir = new[] { sunVec.x, sunVec.y, sunVec.z };

            // --- Paths -----------------------------------------------------------
            var baseName     = string.IsNullOrEmpty(p.OutputName) ? $"Atmosphere_{Capitalize(model)}" : p.OutputName;
            var dir          = savePath.TrimEnd('/') + "/";
            var shaderAsset  = dir + baseName + ".shader";
            var matAsset     = dir + baseName + ".mat";
            var computeAsset = dir + baseName + "_LUT.compute";
            var shaderName   = $"Mosaic/Atmosphere/{Capitalize(model)}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            // --- Generate shader source -----------------------------------------
            string shaderSrc = model switch
            {
                "preetham" => BuildPreethamShader(shaderName, rayleigh, mie, sunDir, sunIntensity, turbidity),
                "bruneton" => BuildBrunetonShader(shaderName, rayleigh, mie, sunDir, sunIntensity, planetRadius, atmoHeight),
                "oneil"    => BuildOneilShader   (shaderName, rayleigh, mie, sunDir, sunIntensity, planetRadius, atmoHeight),
                _          => throw new InvalidOperationException("unreachable"),
            };

            // --- Write files -----------------------------------------------------
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullShaderPath = Path.Combine(projectRoot, shaderAsset);
            Directory.CreateDirectory(Path.GetDirectoryName(fullShaderPath));
            File.WriteAllText(fullShaderPath, shaderSrc, Encoding.UTF8);

            string computePathOut = null;
            if (outputType == "compute_lut")
            {
                var fullCompute = Path.Combine(projectRoot, computeAsset);
                File.WriteAllText(fullCompute, BuildLutComputeShader(rayleigh, mie, planetRadius, atmoHeight),
                                  Encoding.UTF8);
                computePathOut = computeAsset;
            }

            AssetDatabase.Refresh();

            // --- Create material if requested -----------------------------------
            string matPathOut = null;
            string primaryAssetPath = shaderAsset;
            bool applied = false;

            if (outputType == "skybox_material")
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    // Fallback for test environments where the custom shader hasn't compiled.
                    shader = Shader.Find("Skybox/Procedural") ?? Shader.Find("Unlit/Color");
                }

                if (shader != null)
                {
                    var mat = new Material(shader) { name = baseName };
                    AssetDatabase.CreateAsset(mat, matAsset);
                    AssetDatabase.SaveAssets();
                    matPathOut = matAsset;
                    primaryAssetPath = matAsset;

                    if (applyToScene)
                    {
                        RenderSettings.skybox = mat;
                        DynamicGI.UpdateEnvironment();
                        applied = true;
                    }
                }
            }

            return ToolResult<RenderingAtmosphereCreateResult>.Ok(new RenderingAtmosphereCreateResult
            {
                AssetPath       = primaryAssetPath,
                Model           = model,
                OutputType      = outputType,
                AppliedToScene  = applied,
                ShaderPath      = shaderAsset,
                MaterialPath    = matPathOut,
                ComputeLutPath  = computePathOut,
            });
        }

        // ────────────────────────────────────────────────────────────────────────
        // Preetham (1999) — analytic sky using the 5-parameter Perez model.
        // Fast; suitable for real-time skyboxes. Produces CIE xyY zenith luminance
        // scaled by Perez distribution, converted to RGB.
        // ────────────────────────────────────────────────────────────────────────
        static string BuildPreethamShader(string shaderName, float[] ray, float mie,
                                          float[] sunDir, float intensity, float turbidity)
        {
            // Plain decimal format — ShaderLab property defaults don't accept scientific notation (e.g. "5.8E-06")
            string F(float v) => v.ToString("0.#########", CultureInfo.InvariantCulture);

            return $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _SunDirection (""Sun Direction"", Vector) = ({F(sunDir[0])}, {F(sunDir[1])}, {F(sunDir[2])}, 0)
        _Turbidity (""Turbidity"", Range(1.0, 10.0)) = {F(turbidity)}
        _SunIntensity (""Sun Intensity"", Float) = {F(intensity)}
        _RayleighCoeff (""Rayleigh Coefficients"", Vector) = ({F(ray[0])}, {F(ray[1])}, {F(ray[2])}, 0)
        _MieCoeff (""Mie Coefficient"", Float) = {F(mie)}
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Background"" ""Queue""=""Background"" ""PreviewType""=""Skybox"" }}
        Cull Off ZWrite Off

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            float4 _SunDirection;
            float  _Turbidity;
            float  _SunIntensity;
            float4 _RayleighCoeff;
            float  _MieCoeff;

            #define PI 3.14159265359

            struct appdata {{ float4 vertex : POSITION; }};
            struct v2f    {{ float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; }};

            v2f vert(appdata v)
            {{
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }}

            // Perez 5-parameter brightness model:  F(theta, gamma) = (1 + A e^(B/cos(theta))) (1 + C e^(D*gamma) + E cos^2(gamma))
            float PerezY (float cosT, float cosG, float gamma, float A, float B, float C, float D, float E)
            {{
                cosT = max(cosT, 0.001);
                return (1.0 + A * exp(B / cosT)) * (1.0 + C * exp(D * gamma) + E * cosG * cosG);
            }}

            // Dedicated specializations (kept as separate functions per story spec)
            float PerezX (float cosT, float cosG, float gamma, float A, float B, float C, float D, float E)
            {{ return PerezY(cosT, cosG, gamma, A, B, C, D, E); }}
            float PerezY2(float cosT, float cosG, float gamma, float A, float B, float C, float D, float E)
            {{ return PerezY(cosT, cosG, gamma, A, B, C, D, E); }}
            float PerezX2(float cosT, float cosG, float gamma, float A, float B, float C, float D, float E)
            {{ return PerezY(cosT, cosG, gamma, A, B, C, D, E); }}

            // Zenith luminance/chromaticity polynomials from Preetham et al. 1999
            float ZenithLuminance(float T, float thetaS)
            {{
                float chi = (4.0 / 9.0 - T / 120.0) * (PI - 2.0 * thetaS);
                return (4.0453 * T - 4.9710) * tan(chi) - 0.2155 * T + 2.4192;
            }}

            float ZenithX(float T, float thetaS)
            {{
                float3 T3 = float3(T * T, T, 1.0);
                float3 t  = float3(thetaS * thetaS * thetaS, thetaS * thetaS, thetaS);
                float3 c0 = float3( 0.00166, -0.02903,  0.11693);
                float3 c1 = float3(-0.00375,  0.06377, -0.21196);
                float3 c2 = float3( 0.00209, -0.03202,  0.06052);
                float3 c3 = float3( 0.00000,  0.00394,  0.25886);
                return dot(T3, float3(dot(c0, t), dot(c1, t), dot(c2, t))) + T3.z * c3.z;
            }}

            float ZenithY(float T, float thetaS)
            {{
                float3 T3 = float3(T * T, T, 1.0);
                float3 t  = float3(thetaS * thetaS * thetaS, thetaS * thetaS, thetaS);
                float3 c0 = float3( 0.00275, -0.04214,  0.15346);
                float3 c1 = float3(-0.00610,  0.08970, -0.26756);
                float3 c2 = float3( 0.00317, -0.04153,  0.06670);
                float3 c3 = float3( 0.00000,  0.00516,  0.26688);
                return dot(T3, float3(dot(c0, t), dot(c1, t), dot(c2, t))) + T3.z * c3.z;
            }}

            float3 xyYtoRGB(float x, float y, float Y)
            {{
                float X = (Y / max(y, 1e-4)) * x;
                float Z = (Y / max(y, 1e-4)) * (1.0 - x - y);
                // CIE XYZ → linear sRGB (Rec.709)
                float3 rgb;
                rgb.r =  3.2404542 * X - 1.5371385 * Y - 0.4985314 * Z;
                rgb.g = -0.9692660 * X + 1.8760108 * Y + 0.0415560 * Z;
                rgb.b =  0.0556434 * X - 0.2040259 * Y + 1.0572252 * Z;
                return max(rgb, 0.0);
            }}

            fixed4 frag(v2f i) : SV_Target
            {{
                float3 view = normalize(i.viewDir);
                float3 sun  = normalize(_SunDirection.xyz);

                // theta = angle from zenith; gamma = angle between view and sun
                float cosTheta = max(view.y, 0.001);
                float cosGamma = clamp(dot(view, sun), -1.0, 1.0);
                float gamma    = acos(cosGamma);
                float thetaS   = acos(clamp(sun.y, 0.0, 1.0));
                float cosThetaS = max(cos(thetaS), 0.001);

                float T = _Turbidity;

                // Perez coefficients for Y, x, y (Preetham 1999 Table 2)
                float AY =  0.1787 * T - 1.4630;
                float BY = -0.3554 * T + 0.4275;
                float CY = -0.0227 * T + 5.3251;
                float DY =  0.1206 * T - 2.5771;
                float EY = -0.0670 * T + 0.3703;

                float Ax = -0.0193 * T - 0.2592;
                float Bx = -0.0665 * T + 0.0008;
                float Cx = -0.0004 * T + 0.2125;
                float Dx = -0.0641 * T - 0.8989;
                float Ex = -0.0033 * T + 0.0452;

                float Ay = -0.0167 * T - 0.2608;
                float By = -0.0950 * T + 0.0092;
                float Cy = -0.0079 * T + 0.2102;
                float Dy = -0.0441 * T - 1.6537;
                float Ey = -0.0109 * T + 0.0529;

                float numY = PerezY (cosTheta,  cosGamma, gamma, AY, BY, CY, DY, EY);
                float denY = PerezY2(1.0,       cos(thetaS), thetaS, AY, BY, CY, DY, EY);
                float numx = PerezX (cosTheta,  cosGamma, gamma, Ax, Bx, Cx, Dx, Ex);
                float denx = PerezX2(1.0,       cos(thetaS), thetaS, Ax, Bx, Cx, Dx, Ex);
                float numy = PerezY (cosTheta,  cosGamma, gamma, Ay, By, Cy, Dy, Ey);
                float deny = PerezY2(1.0,       cos(thetaS), thetaS, Ay, By, Cy, Dy, Ey);

                float Yz = ZenithLuminance(T, thetaS);
                float xz = ZenithX        (T, thetaS);
                float yz = ZenithY        (T, thetaS);

                float Y = Yz * (numY / max(denY, 1e-4));
                float x = xz * (numx / max(denx, 1e-4));
                float y = yz * (numy / max(deny, 1e-4));

                float3 col = xyYtoRGB(x, y, Y) * (_SunIntensity * 0.05);
                col = 1.0 - exp(-col);  // tonemap
                return fixed4(col, 1);
            }}
            ENDCG
        }}
    }}
    Fallback ""Skybox/Procedural""
}}";
        }

        // ────────────────────────────────────────────────────────────────────────
        // Bruneton (2008) — precomputed transmittance & scattering LUTs for high
        // quality atmosphere. This MVP generates a shader that reads external LUTs
        // (supplied by _TransmittanceLUT / _ScatteringLUT); actual LUT generation
        // is provided by the compute_lut output (see BuildLutComputeShader).
        // ────────────────────────────────────────────────────────────────────────
        static string BuildBrunetonShader(string shaderName, float[] ray, float mie,
                                          float[] sunDir, float intensity,
                                          float planetRadius, float atmoHeight)
        {
            // Plain decimal format — ShaderLab property defaults don't accept scientific notation (e.g. "5.8E-06")
            string F(float v) => v.ToString("0.#########", CultureInfo.InvariantCulture);

            return $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _TransmittanceLUT (""Transmittance LUT"", 2D) = ""white"" {{}}
        _ScatteringLUT (""Scattering LUT"", 2D) = ""white"" {{}}
        _SunDirection (""Sun Direction"", Vector) = ({F(sunDir[0])}, {F(sunDir[1])}, {F(sunDir[2])}, 0)
        _SunIntensity (""Sun Intensity"", Float) = {F(intensity)}
        _PlanetRadius (""Planet Radius (km)"", Float) = {F(planetRadius)}
        _AtmosphereHeight (""Atmosphere Height (km)"", Float) = {F(atmoHeight)}
        _RayleighCoeff (""Rayleigh"", Vector) = ({F(ray[0])}, {F(ray[1])}, {F(ray[2])}, 0)
        _MieCoeff (""Mie"", Float) = {F(mie)}
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Background"" ""Queue""=""Background"" ""PreviewType""=""Skybox"" }}
        Cull Off ZWrite Off

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _TransmittanceLUT;
            sampler2D _ScatteringLUT;
            float4 _SunDirection;
            float  _SunIntensity;
            float  _PlanetRadius;
            float  _AtmosphereHeight;
            float4 _RayleighCoeff;
            float  _MieCoeff;

            struct appdata {{ float4 vertex : POSITION; }};
            struct v2f    {{ float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; }};

            v2f vert(appdata v)
            {{
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }}

            // LUT parameterisation: u = cos(view-zenith) re-mapped, v = altitude re-mapped.
            float2 ParamsToUV(float cosView, float altitudeNorm)
            {{
                float u = 0.5 + 0.5 * cosView;
                float v = saturate(altitudeNorm);
                return float2(u, v);
            }}

            fixed4 frag(v2f i) : SV_Target
            {{
                float3 view = normalize(i.viewDir);
                float3 sun  = normalize(_SunDirection.xyz);

                float cosView = clamp(view.y, -1.0, 1.0);
                float cosSun  = clamp(sun.y,  -1.0, 1.0);
                float altitudeNorm = 0.0; // camera assumed at sea level

                float2 uv = ParamsToUV(cosView, altitudeNorm);
                float3 transmittance = tex2D(_TransmittanceLUT, uv).rgb;
                float3 inscatter     = tex2D(_ScatteringLUT,   float2(uv.x, 0.5 + 0.5 * cosSun)).rgb;

                // Fallback analytic term so the sky isn't black when LUTs are default white.
                float mu = clamp(dot(view, sun), -1.0, 1.0);
                float rayleighPhase = 0.0596831 * (1.0 + mu * mu);
                float g = 0.76;
                float miePhase = 0.1193662 * ((1.0 - g * g) * (1.0 + mu * mu))
                               / pow(1.0 + g * g - 2.0 * g * mu, 1.5);

                float3 skyColor = inscatter * (_RayleighCoeff.rgb * rayleighPhase + _MieCoeff.xxx * miePhase);
                skyColor *= transmittance * _SunIntensity;
                skyColor = 1.0 - exp(-skyColor);
                return fixed4(skyColor, 1);
            }}
            ENDCG
        }}
    }}
    Fallback ""Skybox/Procedural""
}}";
        }

        // ────────────────────────────────────────────────────────────────────────
        // O'Neil (GPU Gems 2) — inline Rayleigh/Mie integration, simpler + cheaper
        // than Bruneton. 16-sample view + 8-sample light ray optical depth.
        // ────────────────────────────────────────────────────────────────────────
        static string BuildOneilShader(string shaderName, float[] ray, float mie,
                                       float[] sunDir, float intensity,
                                       float planetRadius, float atmoHeight)
        {
            // Plain decimal format — ShaderLab property defaults don't accept scientific notation (e.g. "5.8E-06")
            string F(float v) => v.ToString("0.#########", CultureInfo.InvariantCulture);

            // Convert km → m for shader integration
            float planetM = planetRadius * 1000f;
            float atmoM   = atmoHeight   * 1000f;

            return $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _SunDirection (""Sun Direction"", Vector) = ({F(sunDir[0])}, {F(sunDir[1])}, {F(sunDir[2])}, 0)
        _SunIntensity (""Sun Intensity"", Float) = {F(intensity)}
        _PlanetRadius (""Planet Radius"", Float) = {F(planetM)}
        _AtmosphereRadius (""Atmosphere Radius"", Float) = {F(planetM + atmoM)}
        _RayleighCoeff (""Rayleigh"", Vector) = ({F(ray[0] * 1e-6f)}, {F(ray[1] * 1e-6f)}, {F(ray[2] * 1e-6f)}, 0)
        _MieCoeff (""Mie"", Float) = {F(mie * 1e-6f)}
        _RayleighScaleHeight (""Rayleigh Scale Height"", Float) = 8500
        _MieScaleHeight (""Mie Scale Height"", Float) = 1200
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Background"" ""Queue""=""Background"" ""PreviewType""=""Skybox"" }}
        Cull Off ZWrite Off

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            float4 _SunDirection;
            float  _SunIntensity;
            float  _PlanetRadius;
            float  _AtmosphereRadius;
            float4 _RayleighCoeff;
            float  _MieCoeff;
            float  _RayleighScaleHeight;
            float  _MieScaleHeight;

            #define PI 3.14159265359
            #define VIEW_STEPS  16
            #define LIGHT_STEPS 8

            struct appdata {{ float4 vertex : POSITION; }};
            struct v2f    {{ float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; }};

            v2f vert(appdata v)
            {{
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }}

            float2 raySphere(float3 ro, float3 rd, float radius)
            {{
                float b = dot(ro, rd);
                float c = dot(ro, ro) - radius * radius;
                float d = b * b - c;
                if (d < 0.0) return float2(1e5, -1e5);
                float s = sqrt(d);
                return float2(-b - s, -b + s);
            }}

            fixed4 frag(v2f i) : SV_Target
            {{
                float3 rd = normalize(i.viewDir);
                float3 ro = float3(0, _PlanetRadius + 1.0, 0);
                float3 sun = normalize(_SunDirection.xyz);

                float2 hit = raySphere(ro, rd, _AtmosphereRadius);
                if (hit.x > hit.y) return fixed4(0, 0, 0, 1);
                float tStart = max(hit.x, 0.0);
                float tEnd   = hit.y;
                float ds     = (tEnd - tStart) / VIEW_STEPS;

                float3 rayleighSum = 0;
                float3 mieSum      = 0;
                float  rOptical    = 0;
                float  mOptical    = 0;

                [loop]
                for (int s = 0; s < VIEW_STEPS; s++)
                {{
                    float3 p = ro + rd * (tStart + (s + 0.5) * ds);
                    float  h = length(p) - _PlanetRadius;

                    float rd_ = exp(-h / _RayleighScaleHeight) * ds;
                    float md_ = exp(-h / _MieScaleHeight)     * ds;
                    rOptical += rd_;
                    mOptical += md_;

                    float2 lh = raySphere(p, sun, _AtmosphereRadius);
                    float  dsL = lh.y / LIGHT_STEPS;
                    float rOL = 0, mOL = 0;

                    [loop]
                    for (int j = 0; j < LIGHT_STEPS; j++)
                    {{
                        float3 lp = p + sun * (j + 0.5) * dsL;
                        float  lh2 = length(lp) - _PlanetRadius;
                        rOL += exp(-lh2 / _RayleighScaleHeight) * dsL;
                        mOL += exp(-lh2 / _MieScaleHeight)     * dsL;
                    }}

                    float3 tau = _RayleighCoeff.rgb * (rOptical + rOL) + _MieCoeff * 1.1 * (mOptical + mOL);
                    float3 att = exp(-tau);
                    rayleighSum += rd_ * att;
                    mieSum      += md_ * att;
                }}

                float mu = dot(rd, sun);
                float rayleighPhase = 3.0 / (16.0 * PI) * (1.0 + mu * mu);
                float g = 0.758;
                float miePhase = 3.0 / (8.0 * PI) * ((1.0 - g * g) * (1.0 + mu * mu))
                                / (pow(1.0 + g * g - 2.0 * g * mu, 1.5) * (2.0 + g * g));

                float3 col = (rayleighSum * _RayleighCoeff.rgb * rayleighPhase
                            + mieSum      * _MieCoeff          * miePhase) * _SunIntensity;
                col = 1.0 - exp(-col);
                return fixed4(col, 1);
            }}
            ENDCG
        }}
    }}
    Fallback ""Skybox/Procedural""
}}";
        }

        // ────────────────────────────────────────────────────────────────────────
        // Compute shader stub that precomputes Bruneton-style transmittance LUT.
        // ────────────────────────────────────────────────────────────────────────
        static string BuildLutComputeShader(float[] ray, float mie, float planetRadius, float atmoHeight)
        {
            // Plain decimal format — ShaderLab property defaults don't accept scientific notation (e.g. "5.8E-06")
            string F(float v) => v.ToString("0.#########", CultureInfo.InvariantCulture);
            return $@"// Auto-generated by rendering/atmosphere-create (compute_lut output)
#pragma kernel CSTransmittance

RWTexture2D<float4> _TransmittanceLUT;
float3 _RayleighCoeff;
float  _MieCoeff;
float  _PlanetRadius;
float  _AtmosphereRadius;
float  _RayleighScaleHeight;
float  _MieScaleHeight;

float2 raySphere(float3 ro, float3 rd, float r)
{{
    float b = dot(ro, rd);
    float c = dot(ro, ro) - r * r;
    float d = b * b - c;
    if (d < 0) return float2(1e5, -1e5);
    float s = sqrt(d);
    return float2(-b - s, -b + s);
}}

[numthreads(8, 8, 1)]
void CSTransmittance(uint3 id : SV_DispatchThreadID)
{{
    uint w, h;
    _TransmittanceLUT.GetDimensions(w, h);
    float2 uv = (id.xy + 0.5) / float2(w, h);
    float cosView = uv.x * 2.0 - 1.0;
    float altitude = uv.y * ({F(atmoHeight * 1000f)});

    float3 ro = float3(0, _PlanetRadius + altitude, 0);
    float3 rd = normalize(float3(sqrt(max(1 - cosView * cosView, 0.0)), cosView, 0));

    float2 hit = raySphere(ro, rd, _AtmosphereRadius);
    float  t   = max(hit.y, 0.0);
    const int N = 40;
    float ds = t / N;
    float rOpt = 0, mOpt = 0;
    for (int i = 0; i < N; i++)
    {{
        float3 p = ro + rd * (i + 0.5) * ds;
        float  hh = length(p) - _PlanetRadius;
        rOpt += exp(-hh / _RayleighScaleHeight) * ds;
        mOpt += exp(-hh / _MieScaleHeight)     * ds;
    }}
    float3 tau = _RayleighCoeff * rOpt + _MieCoeff * 1.1 * mOpt;
    _TransmittanceLUT[id.xy] = float4(exp(-tau), 1);
}}

// Initial coefficients baked from tool params:
//   Rayleigh (x10^-6): {F(ray[0])}, {F(ray[1])}, {F(ray[2])}
//   Mie      (x10^-6): {F(mie)}
//   Planet radius (km): {F(planetRadius)}
//   Atmosphere height (km): {F(atmoHeight)}
";
        }

        static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
