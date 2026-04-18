using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiContextSteeringTool
    {
        private static readonly HashSet<string> ValidInterestTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "target", "direction" };

        private static readonly HashSet<string> ValidDangerTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "obstacle", "agent", "zone" };

        [MosaicTool("ai/context-steering",
                    "Generates a context-based steering MonoBehaviour with interest/danger maps and attaches it to a GameObject",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AiContextSteeringResult> Execute(AiContextSteeringParams p)
        {
            // ---- Validate inputs ----
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<AiContextSteeringResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<AiContextSteeringResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            if (p.InterestSources == null || p.InterestSources.Count == 0)
                return ToolResult<AiContextSteeringResult>.Fail(
                    "At least one interest source is required", ErrorCodes.INVALID_PARAM);

            // Validate interest sources
            foreach (var src in p.InterestSources)
            {
                if (string.IsNullOrEmpty(src.Type))
                    return ToolResult<AiContextSteeringResult>.Fail(
                        "Each interest source must have a Type", ErrorCodes.INVALID_PARAM);

                string normalized = src.Type.Trim().ToLowerInvariant();
                if (!ValidInterestTypes.Contains(normalized))
                    return ToolResult<AiContextSteeringResult>.Fail(
                        $"Unknown interest source type '{src.Type}'. Valid types: target, direction",
                        ErrorCodes.INVALID_PARAM);

                if (string.IsNullOrEmpty(src.Value))
                    return ToolResult<AiContextSteeringResult>.Fail(
                        "Each interest source must have a Value", ErrorCodes.INVALID_PARAM);

                if (normalized == "direction")
                {
                    if (!TryParseVector3(src.Value, out _))
                        return ToolResult<AiContextSteeringResult>.Fail(
                            $"Invalid direction format '{src.Value}'. Expected 'x,y,z'",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            // Validate danger sources
            var dangerSources = p.DangerSources ?? new List<DangerSource>();
            foreach (var src in dangerSources)
            {
                if (string.IsNullOrEmpty(src.Type))
                    return ToolResult<AiContextSteeringResult>.Fail(
                        "Each danger source must have a Type", ErrorCodes.INVALID_PARAM);

                string normalized = src.Type.Trim().ToLowerInvariant();
                if (!ValidDangerTypes.Contains(normalized))
                    return ToolResult<AiContextSteeringResult>.Fail(
                        $"Unknown danger source type '{src.Type}'. Valid types: obstacle, agent, zone",
                        ErrorCodes.INVALID_PARAM);

                if (string.IsNullOrEmpty(src.Value))
                    return ToolResult<AiContextSteeringResult>.Fail(
                        "Each danger source must have a Value", ErrorCodes.INVALID_PARAM);
            }

            int resolution = p.Resolution ?? 16;
            float maxSpeed = p.MaxSpeed ?? 5f;

            // Build class name
            string nameSuffix = !string.IsNullOrEmpty(p.Name)
                ? SanitizeClassName(p.Name)
                : SanitizeClassName(p.GameObjectName);
            string className = $"ContextSteeringAgent_{nameSuffix}";

            // Output path
            string outputDir = !string.IsNullOrEmpty(p.SavePath)
                ? p.SavePath.TrimEnd('/', '\\')
                : "Assets/Generated/AI";
            string fullDir = Path.Combine(Application.dataPath, "..", outputDir);
            Directory.CreateDirectory(fullDir);

            string scriptAssetPath = $"{outputDir}/{className}.cs";
            string scriptFullPath = Path.Combine(Application.dataPath, "..", scriptAssetPath);

            string scriptContent = GenerateContextSteeringScript(
                className, p.InterestSources, dangerSources, resolution, maxSpeed);

            File.WriteAllText(scriptFullPath, scriptContent);
            AssetDatabase.ImportAsset(scriptAssetPath);
            AssetDatabase.Refresh();

            // Try to add the component via the compiled type
            var scriptType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == className);

            if (scriptType != null)
            {
                Undo.AddComponent(go, scriptType);
            }

            return ToolResult<AiContextSteeringResult>.Ok(new AiContextSteeringResult
            {
                GameObjectName      = go.name,
                InstanceId          = go.GetInstanceID(),
                ScriptPath          = scriptAssetPath,
                Resolution          = resolution,
                InterestSourceCount = p.InterestSources.Count,
                DangerSourceCount   = dangerSources.Count
            });
        }

        // ----------------------------------------------------------------
        // Script generation
        // ----------------------------------------------------------------

        private static string GenerateContextSteeringScript(
            string className,
            List<InterestSource> interestSources,
            List<DangerSource> dangerSources,
            int resolution,
            float maxSpeed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Generated by Mosaic Bridge - ai/context-steering");
            sb.AppendLine("// Context-based steering with interest/danger maps — customize as needed");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            // ---- Parameters ----
            sb.AppendLine("    [Header(\"Steering Parameters\")]");
            sb.AppendLine($"    public float maxSpeed = {FormatFloat(maxSpeed)};");
            sb.AppendLine($"    public int resolution = {resolution};");
            sb.AppendLine();

            // ---- Interest source fields ----
            sb.AppendLine("    [Header(\"Interest Sources\")]");
            int targetIdx = 0;
            int dirIdx = 0;
            foreach (var src in interestSources)
            {
                string type = src.Type.Trim().ToLowerInvariant();
                float weight = src.Weight ?? 1.0f;
                if (type == "target")
                {
                    sb.AppendLine($"    [Tooltip(\"Interest target (weight {FormatFloat(weight)})\")]");
                    sb.AppendLine($"    public Transform interestTarget_{targetIdx};");
                    sb.AppendLine($"    public float interestTargetWeight_{targetIdx} = {FormatFloat(weight)};");
                    targetIdx++;
                }
                else // direction
                {
                    TryParseVector3(src.Value, out var dir);
                    sb.AppendLine($"    [Tooltip(\"Interest direction (weight {FormatFloat(weight)})\")]");
                    sb.AppendLine($"    public Vector3 interestDirection_{dirIdx} = new Vector3({FormatFloat(dir.x)}, {FormatFloat(dir.y)}, {FormatFloat(dir.z)});");
                    sb.AppendLine($"    public float interestDirectionWeight_{dirIdx} = {FormatFloat(weight)};");
                    dirIdx++;
                }
            }
            sb.AppendLine();

            // ---- Danger source fields ----
            if (dangerSources.Count > 0)
            {
                sb.AppendLine("    [Header(\"Danger Sources\")]");
                for (int i = 0; i < dangerSources.Count; i++)
                {
                    var src = dangerSources[i];
                    string type = src.Type.Trim().ToLowerInvariant();
                    float weight = src.Weight ?? 1.0f;
                    float radius = src.Radius ?? 5.0f;
                    sb.AppendLine($"    [Tooltip(\"Danger {type} (weight {FormatFloat(weight)}, radius {FormatFloat(radius)})\")]");
                    sb.AppendLine($"    public Transform dangerSource_{i};");
                    sb.AppendLine($"    public float dangerWeight_{i} = {FormatFloat(weight)};");
                    sb.AppendLine($"    public float dangerRadius_{i} = {FormatFloat(radius)};");
                }
                sb.AppendLine();
            }

            // ---- Runtime arrays ----
            sb.AppendLine("    // Runtime arrays (allocated once)");
            sb.AppendLine("    private Vector3[] _directions;");
            sb.AppendLine("    private float[] _interestMap;");
            sb.AppendLine("    private float[] _dangerMap;");
            sb.AppendLine("    private float[] _contextMap;");
            sb.AppendLine("    [HideInInspector] public Vector3 velocity;");
            sb.AppendLine();

            // ---- Start() ----
            sb.AppendLine("    void Start()");
            sb.AppendLine("    {");
            sb.AppendLine("        AllocateArrays();");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void AllocateArrays()");
            sb.AppendLine("    {");
            sb.AppendLine("        _directions = new Vector3[resolution];");
            sb.AppendLine("        _interestMap = new float[resolution];");
            sb.AppendLine("        _dangerMap = new float[resolution];");
            sb.AppendLine("        _contextMap = new float[resolution];");
            sb.AppendLine();
            sb.AppendLine("        for (int i = 0; i < resolution; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            float angle = i * 2f * Mathf.PI / resolution;");
            sb.AppendLine("            _directions[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // ---- Update() ----
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_directions == null || _directions.Length != resolution)");
            sb.AppendLine("            AllocateArrays();");
            sb.AppendLine();
            sb.AppendLine("        // Clear maps");
            sb.AppendLine("        Array.Clear(_interestMap, 0, resolution);");
            sb.AppendLine("        Array.Clear(_dangerMap, 0, resolution);");
            sb.AppendLine("        Array.Clear(_contextMap, 0, resolution);");
            sb.AppendLine();

            // Compute interest from targets
            sb.AppendLine("        // ---- Interest map ----");
            targetIdx = 0;
            dirIdx = 0;
            foreach (var src in interestSources)
            {
                string type = src.Type.Trim().ToLowerInvariant();
                if (type == "target")
                {
                    sb.AppendLine($"        if (interestTarget_{targetIdx} != null)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            Vector3 dirToTarget = (interestTarget_{targetIdx}.position - transform.position).normalized;");
                    sb.AppendLine("            for (int i = 0; i < resolution; i++)");
                    sb.AppendLine($"                _interestMap[i] += Mathf.Max(0f, Vector3.Dot(_directions[i], dirToTarget)) * interestTargetWeight_{targetIdx};");
                    sb.AppendLine("        }");
                    targetIdx++;
                }
                else // direction
                {
                    sb.AppendLine($"        if (interestDirection_{dirIdx}.sqrMagnitude > 0.0001f)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            Vector3 normDir = interestDirection_{dirIdx}.normalized;");
                    sb.AppendLine("            for (int i = 0; i < resolution; i++)");
                    sb.AppendLine($"                _interestMap[i] += Mathf.Max(0f, Vector3.Dot(_directions[i], normDir)) * interestDirectionWeight_{dirIdx};");
                    sb.AppendLine("        }");
                    dirIdx++;
                }
            }
            sb.AppendLine();

            // Compute danger from sources
            if (dangerSources.Count > 0)
            {
                sb.AppendLine("        // ---- Danger map ----");
                for (int d = 0; d < dangerSources.Count; d++)
                {
                    sb.AppendLine($"        if (dangerSource_{d} != null)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            Vector3 toDanger_{d} = dangerSource_{d}.position - transform.position;");
                    sb.AppendLine($"            float dist_{d} = toDanger_{d}.magnitude;");
                    sb.AppendLine($"            if (dist_{d} < dangerRadius_{d} && dist_{d} > 0.001f)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                Vector3 dirToDanger_{d} = toDanger_{d}.normalized;");
                    sb.AppendLine($"                float falloff_{d} = 1f - dist_{d} / dangerRadius_{d};");
                    sb.AppendLine("                for (int j = 0; j < resolution; j++)");
                    sb.AppendLine($"                    _dangerMap[j] += Mathf.Max(0f, Vector3.Dot(_directions[j], dirToDanger_{d})) * dangerWeight_{d} * falloff_{d};");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                }
                sb.AppendLine();
            }

            // Context map + choose direction
            sb.AppendLine("        // ---- Context map (interest - danger, clamped to 0) ----");
            sb.AppendLine("        float bestValue = 0f;");
            sb.AppendLine("        int bestSlot = 0;");
            sb.AppendLine("        for (int i = 0; i < resolution; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            _contextMap[i] = Mathf.Max(0f, _interestMap[i] - _dangerMap[i]);");
            sb.AppendLine("            if (_contextMap[i] > bestValue)");
            sb.AppendLine("            {");
            sb.AppendLine("                bestValue = _contextMap[i];");
            sb.AppendLine("                bestSlot = i;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Interpolate with neighbors for smoother movement
            sb.AppendLine("        // ---- Interpolate with neighbors for smooth direction ----");
            sb.AppendLine("        Vector3 chosenDirection = Vector3.zero;");
            sb.AppendLine("        if (bestValue > 0f)");
            sb.AppendLine("        {");
            sb.AppendLine("            int prev = (bestSlot - 1 + resolution) % resolution;");
            sb.AppendLine("            int next = (bestSlot + 1) % resolution;");
            sb.AppendLine("            float total = _contextMap[prev] + _contextMap[bestSlot] + _contextMap[next];");
            sb.AppendLine("            if (total > 0f)");
            sb.AppendLine("            {");
            sb.AppendLine("                chosenDirection = (_directions[prev] * _contextMap[prev]");
            sb.AppendLine("                                + _directions[bestSlot] * _contextMap[bestSlot]");
            sb.AppendLine("                                + _directions[next] * _contextMap[next]) / total;");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                chosenDirection = _directions[bestSlot];");
            sb.AppendLine("            }");
            sb.AppendLine("            chosenDirection = chosenDirection.normalized;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Apply velocity
            sb.AppendLine("        // ---- Apply movement ----");
            sb.AppendLine("        velocity = chosenDirection * maxSpeed;");
            sb.AppendLine("        if (velocity.sqrMagnitude > 0.0001f)");
            sb.AppendLine("        {");
            sb.AppendLine("            transform.position += velocity * Time.deltaTime;");
            sb.AppendLine("            transform.forward = velocity.normalized;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // ---- OnDrawGizmosSelected ----
            sb.AppendLine("    void OnDrawGizmosSelected()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_directions == null || _interestMap == null || _dangerMap == null) return;");
            sb.AppendLine();
            sb.AppendLine("        Vector3 origin = transform.position + Vector3.up * 0.1f;");
            sb.AppendLine("        for (int i = 0; i < resolution && i < _directions.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Interest rays (green)");
            sb.AppendLine("            Gizmos.color = Color.green;");
            sb.AppendLine("            Gizmos.DrawRay(origin, _directions[i] * _interestMap[i]);");
            sb.AppendLine();
            sb.AppendLine("            // Danger rays (red)");
            sb.AppendLine("            Gizmos.color = Color.red;");
            sb.AppendLine("            Gizmos.DrawRay(origin, _directions[i] * _dangerMap[i]);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Chosen direction (yellow)");
            sb.AppendLine("        Gizmos.color = Color.yellow;");
            sb.AppendLine("        Gizmos.DrawRay(origin, velocity.normalized * 2f);");
            sb.AppendLine("    }");

            sb.AppendLine("}");
            return sb.ToString();
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string SanitizeClassName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
            }
            string result = sb.ToString();
            if (result.Length == 0 || char.IsDigit(result[0]))
                result = "_" + result;
            return result;
        }

        internal static bool TryParseVector3(string value, out Vector3 result)
        {
            result = Vector3.zero;
            if (string.IsNullOrEmpty(value)) return false;

            string[] parts = value.Split(',');
            if (parts.Length != 3) return false;

            if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float z))
            {
                result = new Vector3(x, y, z);
                return true;
            }
            return false;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture) + "f";
        }
    }
}
