using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public static class RenderSDFTextTool
    {
        [MosaicTool("render/sdf-text",
                    "Generates SDF-based text rendering setup with shader and renderer script for crisp text at any scale",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<RenderSDFTextResult> Execute(RenderSDFTextParams p)
        {
            var fontSize   = p.FontSize ?? 64;
            var spread     = p.Spread ?? 8;
            var characters = string.IsNullOrEmpty(p.Characters)
                ? " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
                : p.Characters;
            var outputDir  = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/Rendering/SDFText"
                : p.OutputDirectory;

            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<RenderSDFTextResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (fontSize <= 0)
                return ToolResult<RenderSDFTextResult>.Fail(
                    "FontSize must be greater than 0", ErrorCodes.OUT_OF_RANGE);

            var projectRoot   = Application.dataPath.Replace("/Assets", "");
            var shaderPath    = Path.Combine(outputDir, "SDFText.shader").Replace("\\", "/");
            var rendererPath  = Path.Combine(outputDir, "SDFTextRenderer.cs").Replace("\\", "/");

            // --- SDF Text Shader ---
            var shaderSrc = @"Shader ""Mosaic/SDFText""
{
    Properties
    {
        _MainTex (""SDF Atlas"", 2D) = ""white"" {}
        _Color (""Text Color"", Color) = (1, 1, 1, 1)
        _Smoothing (""Edge Smoothing"", Range(0, 0.5)) = 0.1
        _Threshold (""Edge Threshold"", Range(0, 1)) = 0.5
        [Toggle] _EnableOutline (""Enable Outline"", Float) = 0
        _OutlineColor (""Outline Color"", Color) = (0, 0, 0, 1)
        _OutlineWidth (""Outline Width"", Range(0, 0.5)) = 0.1
        [Toggle] _EnableShadow (""Enable Shadow"", Float) = 0
        _ShadowColor (""Shadow Color"", Color) = (0, 0, 0, 0.5)
        _ShadowOffset (""Shadow Offset"", Vector) = (0.01, -0.01, 0, 0)
        _ShadowSoftness (""Shadow Softness"", Range(0, 0.5)) = 0.05
    }
    SubShader
    {
        Tags { ""RenderType""=""Transparent"" ""Queue""=""Transparent"" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Smoothing;
            float _Threshold;
            float _EnableOutline;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _EnableShadow;
            fixed4 _ShadowColor;
            float4 _ShadowOffset;
            float _ShadowSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = tex2D(_MainTex, i.uv).a;
                fixed4 result = fixed4(0, 0, 0, 0);

                // Shadow
                if (_EnableShadow > 0.5)
                {
                    float shadowDist = tex2D(_MainTex, i.uv - _ShadowOffset.xy).a;
                    float shadowAlpha = smoothstep(_Threshold - _ShadowSoftness, _Threshold + _ShadowSoftness, shadowDist);
                    result = lerp(result, _ShadowColor, shadowAlpha * _ShadowColor.a);
                }

                // Outline
                if (_EnableOutline > 0.5)
                {
                    float outlineEdge = _Threshold - _OutlineWidth;
                    float outlineAlpha = smoothstep(outlineEdge - _Smoothing, outlineEdge + _Smoothing, dist);
                    result = lerp(result, _OutlineColor, outlineAlpha);
                }

                // Main text
                float textAlpha = smoothstep(_Threshold - _Smoothing, _Threshold + _Smoothing, dist);
                fixed4 textColor = _Color * i.color;
                result = lerp(result, textColor, textAlpha);

                return result;
            }
            ENDCG
        }
    }
}";

            // --- SDFTextRenderer.cs ---
            var rendererSrc = $@"using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SDF text renderer that lays out quads per character from an SDF atlas texture.
/// Attach to a GameObject; set the atlas texture and character map, then call SetText().
/// Note: SDF atlas generation requires external font processing. This renderer expects
/// a pre-built atlas with uniform grid layout (characters in ASCII order).
/// </summary>
public class SDFTextRenderer : MonoBehaviour
{{
    [Header(""Atlas Configuration"")]
    public Texture2D sdfAtlas;
    public int atlasColumns = 16;
    public int atlasRows = 6;
    public int fontSize = {fontSize};
    public int spread = {spread};
    public string characterSet = ""{EscapeForCSharp(characters)}"";

    [Header(""Text Settings"")]
    [TextArea(3, 10)]
    public string text = ""Hello World"";
    public float characterSpacing = 0.6f;
    public float lineSpacing = 1.2f;
    public Color textColor = Color.white;

    [Header(""References"")]
    public Material sdfMaterial;

    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    void Start()
    {{
        meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (sdfMaterial != null)
            meshRenderer.material = sdfMaterial;

        RebuildMesh();
    }}

    public void SetText(string newText)
    {{
        text = newText;
        RebuildMesh();
    }}

    void RebuildMesh()
    {{
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(characterSet)) return;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var triangles = new List<int>();

        float cursorX = 0;
        float cursorY = 0;
        float cellW = 1f / atlasColumns;
        float cellH = 1f / atlasRows;

        for (int i = 0; i < text.Length; i++)
        {{
            char c = text[i];
            if (c == '\n')
            {{
                cursorX = 0;
                cursorY -= lineSpacing;
                continue;
            }}

            int charIndex = characterSet.IndexOf(c);
            if (charIndex < 0) charIndex = 0;

            int col = charIndex % atlasColumns;
            int row = charIndex / atlasColumns;

            float uvX = col * cellW;
            float uvY = 1f - (row + 1) * cellH;

            int vi = vertices.Count;
            vertices.Add(new Vector3(cursorX, cursorY, 0));
            vertices.Add(new Vector3(cursorX + 1, cursorY, 0));
            vertices.Add(new Vector3(cursorX + 1, cursorY + 1, 0));
            vertices.Add(new Vector3(cursorX, cursorY + 1, 0));

            uvs.Add(new Vector2(uvX, uvY));
            uvs.Add(new Vector2(uvX + cellW, uvY));
            uvs.Add(new Vector2(uvX + cellW, uvY + cellH));
            uvs.Add(new Vector2(uvX, uvY + cellH));

            colors.Add(textColor);
            colors.Add(textColor);
            colors.Add(textColor);
            colors.Add(textColor);

            triangles.Add(vi);
            triangles.Add(vi + 2);
            triangles.Add(vi + 1);
            triangles.Add(vi);
            triangles.Add(vi + 3);
            triangles.Add(vi + 2);

            cursorX += characterSpacing;
        }}

        if (mesh == null) mesh = new Mesh();
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        if (meshFilter != null)
            meshFilter.mesh = mesh;
    }}

    void OnValidate()
    {{
        if (Application.isPlaying && meshFilter != null)
            RebuildMesh();
    }}
}}";

            WriteFile(projectRoot, shaderPath, shaderSrc);
            WriteFile(projectRoot, rendererPath, rendererSrc);

            return ToolResult<RenderSDFTextResult>.Ok(new RenderSDFTextResult
            {
                ShaderPath         = shaderPath,
                RendererScriptPath = rendererPath,
                FontSize           = fontSize
            });
        }

        static string EscapeForCSharp(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
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
