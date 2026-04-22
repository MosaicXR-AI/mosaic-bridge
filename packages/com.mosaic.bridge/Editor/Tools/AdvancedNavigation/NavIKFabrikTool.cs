using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public static class NavIKFabrikTool
    {
        [MosaicTool("nav/ik-fabrik",
                    "Sets up FABRIK inverse kinematics solver on a bone chain with a generated MonoBehaviour script",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavIKFabrikResult> Execute(NavIKFabrikParams p)
        {
            if (p.TargetPosition == null || p.TargetPosition.Length != 3)
                return ToolResult<NavIKFabrikResult>.Fail(
                    "TargetPosition must be a float array of [x, y, z]", ErrorCodes.INVALID_PARAM);

            // Find root bone
            GameObject root = null;
            if (p.RootInstanceId.HasValue && p.RootInstanceId.Value != 0)
                root = UnityEngine.Resources.EntityIdToObject(p.RootInstanceId.Value) as GameObject;
            if (root == null && !string.IsNullOrEmpty(p.RootName))
                root = GameObject.Find(p.RootName);
            if (root == null)
                return ToolResult<NavIKFabrikResult>.Fail(
                    "Could not find root bone GameObject. Provide a valid RootInstanceId or RootName.",
                    ErrorCodes.NOT_FOUND);

            // Auto-detect chain length from hierarchy
            int chainLength = p.ChainLength ?? 0;
            if (chainLength <= 0)
            {
                chainLength = 0;
                var current = root.transform;
                while (current.childCount > 0)
                {
                    chainLength++;
                    current = current.GetChild(0);
                }
                chainLength++; // Include the last bone
                if (chainLength < 2) chainLength = 2;
            }

            var iterations = p.Iterations ?? 10;
            var tolerance  = p.Tolerance ?? 0.01f;
            var outputDir  = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/Navigation/IK"
                : p.OutputDirectory;

            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<NavIKFabrikResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            var scriptPath  = Path.Combine(outputDir, "FABRIKSolver.cs").Replace("\\", "/");
            var projectRoot = Application.dataPath.Replace("/Assets", "");

            var scriptSrc = $@"using UnityEngine;

/// <summary>
/// FABRIK (Forward And Backward Reaching Inverse Kinematics) solver.
/// Walks a bone chain from root, runs forward+backward passes each frame to reach the target.
/// </summary>
public class FABRIKSolver : MonoBehaviour
{{
    [Header(""IK Settings"")]
    public Transform target;
    public int chainLength = {chainLength};
    public int iterations = {iterations};
    public float tolerance = {tolerance}f;

    Transform[] bones;
    float[] boneLengths;
    float totalLength;
    Vector3[] positions;

    void Start()
    {{
        InitChain();
    }}

    void InitChain()
    {{
        bones = new Transform[chainLength];
        boneLengths = new float[chainLength - 1];
        positions = new Vector3[chainLength];

        var current = transform;
        for (int i = 0; i < chainLength; i++)
        {{
            bones[i] = current;
            if (i < chainLength - 1)
            {{
                if (current.childCount > 0)
                {{
                    var child = current.GetChild(0);
                    boneLengths[i] = Vector3.Distance(current.position, child.position);
                    totalLength += boneLengths[i];
                    current = child;
                }}
                else
                {{
                    boneLengths[i] = 0.5f;
                    totalLength += boneLengths[i];
                }}
            }}
        }}
    }}

    void LateUpdate()
    {{
        if (target == null || bones == null || bones.Length == 0) return;

        SolveFABRIK();
    }}

    void SolveFABRIK()
    {{
        Vector3 targetPos = target.position;
        Vector3 rootPos = bones[0].position;

        // Copy current positions
        for (int i = 0; i < chainLength; i++)
            positions[i] = bones[i].position;

        // Check if target is reachable
        float distToTarget = Vector3.Distance(rootPos, targetPos);
        if (distToTarget > totalLength)
        {{
            // Stretch toward target
            Vector3 dir = (targetPos - rootPos).normalized;
            for (int i = 1; i < chainLength; i++)
                positions[i] = positions[i - 1] + dir * boneLengths[i - 1];
        }}
        else
        {{
            for (int iteration = 0; iteration < iterations; iteration++)
            {{
                // Check convergence
                if (Vector3.Distance(positions[chainLength - 1], targetPos) < tolerance)
                    break;

                // Forward pass: end effector to root
                positions[chainLength - 1] = targetPos;
                for (int i = chainLength - 2; i >= 0; i--)
                {{
                    Vector3 dir = (positions[i] - positions[i + 1]).normalized;
                    positions[i] = positions[i + 1] + dir * boneLengths[i];
                }}

                // Backward pass: root to end effector
                positions[0] = rootPos;
                for (int i = 1; i < chainLength; i++)
                {{
                    Vector3 dir = (positions[i] - positions[i - 1]).normalized;
                    positions[i] = positions[i - 1] + dir * boneLengths[i - 1];
                }}
            }}
        }}

        // Apply positions and rotations
        for (int i = 0; i < chainLength - 1; i++)
        {{
            bones[i].position = positions[i];
            Vector3 lookDir = positions[i + 1] - positions[i];
            if (lookDir.sqrMagnitude > 0.0001f)
                bones[i].rotation = Quaternion.LookRotation(lookDir);
        }}
        bones[chainLength - 1].position = positions[chainLength - 1];
    }}
}}";

            var fullPath = Path.Combine(projectRoot, scriptPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, scriptSrc, Encoding.UTF8);
            AssetDatabase.ImportAsset(scriptPath);

            return ToolResult<NavIKFabrikResult>.Ok(new NavIKFabrikResult
            {
                SolverScriptPath = scriptPath,
                ChainLength      = chainLength,
                RootInstanceId   = root.GetInstanceID(),
                TargetPosition   = p.TargetPosition
            });
        }
    }
}
