using System;
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
    public static class ShaderCreateRaymarcherTool
    {
        static readonly string[] ValidPrimitives  = { "sphere", "box", "torus", "cylinder", "plane" };
        static readonly string[] ValidOperations  = { "union", "intersection", "subtraction", "smooth-union" };

        [MosaicTool("shader/create-raymarcher",
                    "Generates a ray marching shader with SDF primitives and configurable combine operations",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ShaderCreateRaymarcherResult> Execute(ShaderCreateRaymarcherParams p)
        {
            // Defaults
            var primitives     = p.Primitives ?? new[] { "sphere" };
            var operation      = string.IsNullOrEmpty(p.Operation) ? "union" : p.Operation;
            var smoothFactor   = p.SmoothFactor ?? 0.5f;
            var maxSteps       = p.MaxSteps ?? 100;
            var maxDist        = p.MaxDistance ?? 100f;
            var surfaceDist    = p.SurfaceDistance ?? 0.001f;
            var outputDir      = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/Rendering/Raymarcher"
                : p.OutputDirectory;

            // Validate
            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<ShaderCreateRaymarcherResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            foreach (var prim in primitives)
            {
                if (!ValidPrimitives.Contains(prim.ToLowerInvariant()))
                    return ToolResult<ShaderCreateRaymarcherResult>.Fail(
                        $"Invalid primitive '{prim}'. Valid: {string.Join(", ", ValidPrimitives)}",
                        ErrorCodes.INVALID_PARAM);
            }

            if (!ValidOperations.Contains(operation.ToLowerInvariant()))
                return ToolResult<ShaderCreateRaymarcherResult>.Fail(
                    $"Invalid operation '{operation}'. Valid: {string.Join(", ", ValidOperations)}",
                    ErrorCodes.INVALID_PARAM);

            var shaderName = "Mosaic/Raymarcher";
            var shaderPath = Path.Combine(outputDir, "Raymarcher.shader").Replace("\\", "/");
            var matPath    = Path.Combine(outputDir, "RaymarcherMat.mat").Replace("\\", "/");

            // Build shader source
            var sb = new StringBuilder();
            sb.AppendLine($"Shader \"{shaderName}\"");
            sb.AppendLine("{");
            sb.AppendLine("    Properties");
            sb.AppendLine("    {");
            sb.AppendLine("        _MainTex (\"Texture\", 2D) = \"white\" {}");
            sb.AppendLine("    }");
            sb.AppendLine("    SubShader");
            sb.AppendLine("    {");
            sb.AppendLine("        Tags { \"RenderType\"=\"Opaque\" \"Queue\"=\"Geometry\" }");
            sb.AppendLine("        Pass");
            sb.AppendLine("        {");
            sb.AppendLine("            CGPROGRAM");
            sb.AppendLine("            #pragma vertex vert");
            sb.AppendLine("            #pragma fragment frag");
            sb.AppendLine("            #include \"UnityCG.cginc\"");
            sb.AppendLine();
            sb.AppendLine("            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };");
            sb.AppendLine("            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; float3 ro : TEXCOORD1; float3 hitPos : TEXCOORD2; };");
            sb.AppendLine();
            sb.AppendLine("            v2f vert(appdata v)");
            sb.AppendLine("            {");
            sb.AppendLine("                v2f o;");
            sb.AppendLine("                o.vertex = UnityObjectToClipPos(v.vertex);");
            sb.AppendLine("                o.uv = v.uv;");
            sb.AppendLine("                o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;");
            sb.AppendLine("                o.hitPos = v.vertex.xyz;");
            sb.AppendLine("                return o;");
            sb.AppendLine("            }");
            sb.AppendLine();

            // SDF functions
            sb.AppendLine("            // SDF Primitives");
            sb.AppendLine("            float sdSphere(float3 p, float r) { return length(p) - r; }");
            sb.AppendLine("            float sdBox(float3 p, float3 b) { float3 q = abs(p) - b; return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0); }");
            sb.AppendLine("            float sdTorus(float3 p, float2 t) { float2 q = float2(length(p.xz) - t.x, p.y); return length(q) - t.y; }");
            sb.AppendLine("            float sdCylinder(float3 p, float r, float h) { float2 d = abs(float2(length(p.xz), p.y)) - float2(r, h); return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)); }");
            sb.AppendLine("            float sdPlane(float3 p) { return p.y; }");
            sb.AppendLine();

            // Combine operations
            sb.AppendLine("            // Combine operations");
            sb.AppendLine("            float opUnion(float d1, float d2) { return min(d1, d2); }");
            sb.AppendLine("            float opIntersection(float d1, float d2) { return max(d1, d2); }");
            sb.AppendLine("            float opSubtraction(float d1, float d2) { return max(-d1, d2); }");
            sb.AppendLine($"            float opSmoothUnion(float d1, float d2, float k) {{ float h = clamp(0.5 + 0.5 * (d2 - d1) / k, 0.0, 1.0); return lerp(d2, d1, h) - k * h * (1.0 - h); }}");
            sb.AppendLine();

            // Scene SDF
            sb.AppendLine("            float GetDist(float3 p)");
            sb.AppendLine("            {");
            var lowerPrims = primitives.Select(x => x.ToLowerInvariant()).ToArray();
            int idx = 0;
            foreach (var prim in lowerPrims)
            {
                string sdf = prim switch
                {
                    "sphere"   => $"sdSphere(p - float3({idx * 1.5f}, 0, 0), 0.5)",
                    "box"      => $"sdBox(p - float3({idx * 1.5f}, 0, 0), float3(0.4, 0.4, 0.4))",
                    "torus"    => $"sdTorus(p - float3({idx * 1.5f}, 0, 0), float2(0.4, 0.15))",
                    "cylinder" => $"sdCylinder(p - float3({idx * 1.5f}, 0, 0), 0.3, 0.5)",
                    "plane"    => "sdPlane(p + float3(0, 0.5, 0))",
                    _          => "sdSphere(p, 0.5)"
                };

                if (idx == 0)
                    sb.AppendLine($"                float d = {sdf};");
                else
                {
                    string combineOp = operation.ToLowerInvariant() switch
                    {
                        "union"        => $"opUnion(d, {sdf})",
                        "intersection" => $"opIntersection(d, {sdf})",
                        "subtraction"  => $"opSubtraction(d, {sdf})",
                        "smooth-union" => $"opSmoothUnion(d, {sdf}, {smoothFactor:F3})",
                        _              => $"opUnion(d, {sdf})"
                    };
                    sb.AppendLine($"                d = {combineOp};");
                }
                idx++;
            }
            if (idx == 0) sb.AppendLine("                float d = sdSphere(p, 0.5);");
            sb.AppendLine("                return d;");
            sb.AppendLine("            }");
            sb.AppendLine();

            // Normal estimation
            sb.AppendLine("            float3 GetNormal(float3 p)");
            sb.AppendLine("            {");
            sb.AppendLine("                float e = 0.001;");
            sb.AppendLine("                float d = GetDist(p);");
            sb.AppendLine("                float3 n = d - float3(GetDist(p - float3(e, 0, 0)), GetDist(p - float3(0, e, 0)), GetDist(p - float3(0, 0, e)));");
            sb.AppendLine("                return normalize(n);");
            sb.AppendLine("            }");
            sb.AppendLine();

            // Ray march
            sb.AppendLine($"            #define MAX_STEPS {maxSteps}");
            sb.AppendLine($"            #define MAX_DIST {maxDist:F1}");
            sb.AppendLine($"            #define SURF_DIST {surfaceDist}");
            sb.AppendLine();
            sb.AppendLine("            float Raymarch(float3 ro, float3 rd)");
            sb.AppendLine("            {");
            sb.AppendLine("                float dO = 0;");
            sb.AppendLine("                for (int i = 0; i < MAX_STEPS; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    float3 p = ro + rd * dO;");
            sb.AppendLine("                    float dS = GetDist(p);");
            sb.AppendLine("                    dO += dS;");
            sb.AppendLine("                    if (dO > MAX_DIST || abs(dS) < SURF_DIST) break;");
            sb.AppendLine("                }");
            sb.AppendLine("                return dO;");
            sb.AppendLine("            }");
            sb.AppendLine();

            // Fragment shader with lighting
            sb.AppendLine("            fixed4 frag(v2f i) : SV_Target");
            sb.AppendLine("            {");
            sb.AppendLine("                float3 ro = i.ro;");
            sb.AppendLine("                float3 rd = normalize(i.hitPos - ro);");
            sb.AppendLine("                float d = Raymarch(ro, rd);");
            sb.AppendLine("                if (d >= MAX_DIST) discard;");
            sb.AppendLine("                float3 p = ro + rd * d;");
            sb.AppendLine("                float3 n = GetNormal(p);");
            sb.AppendLine("                float3 lightDir = normalize(float3(1, 1, -1));");
            sb.AppendLine("                float diff = saturate(dot(n, lightDir));");
            sb.AppendLine("                float3 viewDir = normalize(ro - p);");
            sb.AppendLine("                float3 halfDir = normalize(lightDir + viewDir);");
            sb.AppendLine("                float spec = pow(saturate(dot(n, halfDir)), 64.0);");
            sb.AppendLine("                float3 col = float3(0.8, 0.8, 0.9) * (diff * 0.8 + 0.2) + spec * 0.5;");
            sb.AppendLine("                return fixed4(col, 1);");
            sb.AppendLine("            }");
            sb.AppendLine("            ENDCG");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // Write files
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullShaderPath = Path.Combine(projectRoot, shaderPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullShaderPath));
            File.WriteAllText(fullShaderPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(shaderPath);

            // Create material
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                AssetDatabase.Refresh();
                shader = Shader.Find(shaderName);
            }
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            return ToolResult<ShaderCreateRaymarcherResult>.Ok(new ShaderCreateRaymarcherResult
            {
                ShaderPath   = shaderPath,
                MaterialPath = matPath,
                Primitives   = lowerPrims,
                Operation    = operation
            });
        }
    }
}
