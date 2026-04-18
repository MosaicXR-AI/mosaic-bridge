using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public static class RenderPortalTool
    {
        [MosaicTool("render/portal",
                    "Scaffolds a stencil-based portal rendering setup with shader, manager, and setup script",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<RenderPortalResult> Execute(RenderPortalParams p)
        {
            var width     = p.PortalWidth ?? 2f;
            var height    = p.PortalHeight ?? 3f;
            var rtRes     = p.RenderTextureResolution ?? 1024;
            var outputDir = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/Rendering/Portal"
                : p.OutputDirectory;

            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<RenderPortalResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var shaderPath  = Path.Combine(outputDir, "Portal.shader").Replace("\\", "/");
            var managerPath = Path.Combine(outputDir, "PortalManager.cs").Replace("\\", "/");
            var setupPath   = Path.Combine(outputDir, "PortalSetup.cs").Replace("\\", "/");

            // --- Portal Shader ---
            var shaderSrc = @"Shader ""Mosaic/Portal""
{
    Properties
    {
        _PortalTex (""Portal Texture"", 2D) = ""white"" {}
    }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""Queue""=""Geometry+1"" }

        // Stencil write pass
        Pass
        {
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }
            ColorMask 0
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return 0; }
            ENDCG
        }

        // Portal render pass
        Pass
        {
            Stencil
            {
                Ref 1
                Comp Equal
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _PortalTex;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float4 screenPos : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                return tex2D(_PortalTex, uv);
            }
            ENDCG
        }
    }
}";

            // --- PortalManager.cs ---
            var managerSrc = $@"using UnityEngine;

/// <summary>
/// Manages two portal GameObjects and their cameras.
/// Calculates view matrices so the player sees ""through"" each portal.
/// </summary>
public class PortalManager : MonoBehaviour
{{
    [Header(""Portal References"")]
    public Transform portalA;
    public Transform portalB;
    public Camera portalCameraA;
    public Camera portalCameraB;

    [Header(""Settings"")]
    public int renderTextureResolution = {rtRes};

    RenderTexture rtA;
    RenderTexture rtB;
    Material matA;
    Material matB;

    void Start()
    {{
        rtA = new RenderTexture(renderTextureResolution, renderTextureResolution, 24);
        rtB = new RenderTexture(renderTextureResolution, renderTextureResolution, 24);
        portalCameraA.targetTexture = rtA;
        portalCameraB.targetTexture = rtB;

        var renderer = portalA.GetComponent<Renderer>();
        if (renderer != null)
        {{
            matA = renderer.material;
            matA.SetTexture(""_PortalTex"", rtB);
        }}

        renderer = portalB.GetComponent<Renderer>();
        if (renderer != null)
        {{
            matB = renderer.material;
            matB.SetTexture(""_PortalTex"", rtA);
        }}
    }}

    void LateUpdate()
    {{
        UpdatePortalCamera(portalA, portalB, portalCameraA);
        UpdatePortalCamera(portalB, portalA, portalCameraB);
    }}

    void UpdatePortalCamera(Transform inPortal, Transform outPortal, Camera portalCam)
    {{
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // Calculate relative position of main camera to the in-portal
        Vector3 relativePos = inPortal.InverseTransformPoint(mainCam.transform.position);
        relativePos = new Vector3(-relativePos.x, relativePos.y, -relativePos.z);
        portalCam.transform.position = outPortal.TransformPoint(relativePos);

        // Calculate relative rotation
        Quaternion relativeRot = Quaternion.Inverse(inPortal.rotation) * mainCam.transform.rotation;
        relativeRot = Quaternion.Euler(0, 180, 0) * relativeRot;
        portalCam.transform.rotation = outPortal.rotation * relativeRot;
    }}

    void OnDestroy()
    {{
        if (rtA != null) rtA.Release();
        if (rtB != null) rtB.Release();
    }}
}}";

            // --- PortalSetup.cs ---
            var setupSrc = $@"using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to set up the portal scene with two portal quads facing each other.
/// </summary>
public static class PortalSetup
{{
    [MenuItem(""Mosaic/Setup Portals"")]
    public static void Setup()
    {{
        // Create parent
        var root = new GameObject(""PortalSystem"");
        var manager = root.AddComponent<PortalManager>();

        // Portal A
        var portalA = GameObject.CreatePrimitive(PrimitiveType.Quad);
        portalA.name = ""PortalA"";
        portalA.transform.SetParent(root.transform);
        portalA.transform.position = new Vector3(-3, {height / 2f:F1}f, 0);
        portalA.transform.localScale = new Vector3({width:F1}f, {height:F1}f, 1f);
        portalA.transform.rotation = Quaternion.Euler(0, 90, 0);

        // Portal B
        var portalB = GameObject.CreatePrimitive(PrimitiveType.Quad);
        portalB.name = ""PortalB"";
        portalB.transform.SetParent(root.transform);
        portalB.transform.position = new Vector3(3, {height / 2f:F1}f, 0);
        portalB.transform.localScale = new Vector3({width:F1}f, {height:F1}f, 1f);
        portalB.transform.rotation = Quaternion.Euler(0, -90, 0);

        // Portal Cameras
        var camObjA = new GameObject(""PortalCameraA"");
        camObjA.transform.SetParent(root.transform);
        var camA = camObjA.AddComponent<Camera>();
        camA.enabled = false;

        var camObjB = new GameObject(""PortalCameraB"");
        camObjB.transform.SetParent(root.transform);
        var camB = camObjB.AddComponent<Camera>();
        camB.enabled = false;

        // Apply portal shader material
        var shader = Shader.Find(""Mosaic/Portal"");
        if (shader != null)
        {{
            portalA.GetComponent<Renderer>().material = new Material(shader);
            portalB.GetComponent<Renderer>().material = new Material(shader);
        }}

        // Wire up manager
        manager.portalA = portalA.transform;
        manager.portalB = portalB.transform;
        manager.portalCameraA = camA;
        manager.portalCameraB = camB;

        Undo.RegisterCreatedObjectUndo(root, ""Create Portal System"");
        Selection.activeGameObject = root;
    }}
}}";

            // Write all files
            WriteFile(projectRoot, shaderPath, shaderSrc);
            WriteFile(projectRoot, managerPath, managerSrc);
            WriteFile(projectRoot, setupPath, setupSrc);

            return ToolResult<RenderPortalResult>.Ok(new RenderPortalResult
            {
                ShaderPath        = shaderPath,
                ManagerScriptPath = managerPath,
                SetupScriptPath   = setupPath
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
