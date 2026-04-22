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
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiSteeringAddTool
    {
        private static readonly HashSet<string> ValidBehaviors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "seek", "flee", "arrive", "wander", "pursue", "evade",
            "obstacle_avoidance", "separation", "alignment", "cohesion",
            "path_follow", "leader_follow"
        };

        [MosaicTool("ai/steering-add",
                    "Generates a Craig Reynolds steering behaviors MonoBehaviour and attaches it to a GameObject",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AiSteeringAddResult> Execute(AiSteeringAddParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<AiSteeringAddResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<AiSteeringAddResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            if (p.Behaviors == null || p.Behaviors.Count == 0)
                return ToolResult<AiSteeringAddResult>.Fail(
                    "At least one behavior is required", ErrorCodes.INVALID_PARAM);

            // Validate all behavior types
            var behaviorTypes = new List<string>();
            foreach (var b in p.Behaviors)
            {
                if (string.IsNullOrEmpty(b.Type))
                    return ToolResult<AiSteeringAddResult>.Fail(
                        "Each behavior must have a Type", ErrorCodes.INVALID_PARAM);

                string normalized = b.Type.Trim().ToLowerInvariant();
                if (!ValidBehaviors.Contains(normalized))
                    return ToolResult<AiSteeringAddResult>.Fail(
                        $"Unknown behavior type '{b.Type}'. Valid types: {string.Join(", ", ValidBehaviors)}",
                        ErrorCodes.INVALID_PARAM);

                behaviorTypes.Add(normalized);
            }

            float maxSpeed       = p.MaxSpeed       ?? 5f;
            float maxForce       = p.MaxForce       ?? 10f;
            float mass           = p.Mass           ?? 1f;
            float neighborRadius = p.NeighborRadius ?? 10f;

            // Sanitize GO name for use as class name
            string safeName = SanitizeClassName(p.GameObjectName);
            string className = $"SteeringAgent_{safeName}";

            string outputDir = "Assets/Generated/AI";
            AssetDatabaseHelper.EnsureFolder(outputDir);
            string fullDir = Path.Combine(Application.dataPath, "..", outputDir);

            string scriptAssetPath = $"{outputDir}/{className}.cs";
            string scriptFullPath = Path.Combine(Application.dataPath, "..", scriptAssetPath);

            string scriptContent = GenerateSteeringScript(
                className, p.Behaviors, maxSpeed, maxForce, mass, neighborRadius);

            File.WriteAllText(scriptFullPath, scriptContent);
            AssetDatabase.ImportAsset(scriptAssetPath);

            // Force a script compilation so the type becomes available
            AssetDatabase.Refresh();

            // Try to add the component via the compiled type
            var scriptType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == className);

            if (scriptType != null)
            {
                Undo.AddComponent(go, scriptType);
            }

            return ToolResult<AiSteeringAddResult>.Ok(new AiSteeringAddResult
            {
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                BehaviorCount  = p.Behaviors.Count,
                MaxSpeed       = maxSpeed,
                Behaviors      = behaviorTypes.ToArray(),
                ScriptPath     = scriptAssetPath
            });
        }

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

        private static string GenerateSteeringScript(
            string className,
            List<SteeringBehavior> behaviors,
            float maxSpeed, float maxForce, float mass, float neighborRadius)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Generated by Mosaic Bridge - ai/steering-add");
            sb.AppendLine("// Craig Reynolds steering behaviors — customize as needed");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            // Fields
            sb.AppendLine("    [Header(\"Agent Parameters\")]");
            sb.AppendLine($"    public float maxSpeed = {FormatFloat(maxSpeed)};");
            sb.AppendLine($"    public float maxForce = {FormatFloat(maxForce)};");
            sb.AppendLine($"    public float mass = {FormatFloat(mass)};");
            sb.AppendLine($"    public float neighborRadius = {FormatFloat(neighborRadius)};");
            sb.AppendLine();
            sb.AppendLine("    [HideInInspector] public Vector3 velocity;");
            sb.AppendLine();

            // Per-behavior fields
            sb.AppendLine("    [Header(\"Behavior Weights\")]");
            foreach (var b in behaviors)
            {
                string norm = b.Type.Trim().ToLowerInvariant();
                float weight = b.Weight ?? 1.0f;
                sb.AppendLine($"    public float weight_{norm} = {FormatFloat(weight)};");
            }
            sb.AppendLine();

            // Target/radius fields where needed
            bool hasTargetBehavior = behaviors.Any(b =>
            {
                string n = b.Type.Trim().ToLowerInvariant();
                return n == "seek" || n == "flee" || n == "arrive" || n == "pursue" || n == "evade" || n == "leader_follow";
            });
            bool hasArrive = behaviors.Any(b => b.Type.Trim().ToLowerInvariant() == "arrive");
            bool hasWander = behaviors.Any(b => b.Type.Trim().ToLowerInvariant() == "wander");
            bool hasPathFollow = behaviors.Any(b => b.Type.Trim().ToLowerInvariant() == "path_follow");

            if (hasTargetBehavior)
            {
                sb.AppendLine("    [Header(\"Targets\")]");
                foreach (var b in behaviors)
                {
                    string n = b.Type.Trim().ToLowerInvariant();
                    if (n == "seek" || n == "flee" || n == "arrive" || n == "pursue" || n == "evade" || n == "leader_follow")
                    {
                        string targetComment = string.IsNullOrEmpty(b.Target) ? "" : $" // default: {b.Target}";
                        sb.AppendLine($"    public Transform target_{n};{targetComment}");
                    }
                }
                sb.AppendLine();
            }

            if (hasArrive)
            {
                float arriveRadius = behaviors.First(b => b.Type.Trim().ToLowerInvariant() == "arrive").Radius ?? 5f;
                sb.AppendLine($"    public float arriveRadius = {FormatFloat(arriveRadius)};");
            }
            if (hasWander)
            {
                float wanderRadius = behaviors.First(b => b.Type.Trim().ToLowerInvariant() == "wander").Radius ?? 2f;
                sb.AppendLine($"    public float wanderRadius = {FormatFloat(wanderRadius)};");
                sb.AppendLine($"    public float wanderDistance = 4f;");
                sb.AppendLine($"    public float wanderJitter = 0.5f;");
                sb.AppendLine("    private Vector3 _wanderTarget;");
            }
            if (hasPathFollow)
            {
                sb.AppendLine("    public Transform[] waypoints;");
                sb.AppendLine("    private int _currentWaypoint;");
            }

            sb.AppendLine();

            // Start
            if (hasWander)
            {
                sb.AppendLine("    void Start()");
                sb.AppendLine("    {");
                sb.AppendLine("        _wanderTarget = Random.insideUnitSphere * wanderRadius;");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            // Update
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        Vector3 steeringForce = Vector3.zero;");
            sb.AppendLine();

            foreach (var b in behaviors)
            {
                string norm = b.Type.Trim().ToLowerInvariant();
                string methodName = BehaviorMethodName(norm);
                sb.AppendLine($"        steeringForce += {methodName}() * weight_{norm};");
            }

            sb.AppendLine();
            sb.AppendLine("        // Clamp to max force and apply mass");
            sb.AppendLine("        steeringForce = Vector3.ClampMagnitude(steeringForce, maxForce);");
            sb.AppendLine("        Vector3 acceleration = steeringForce / mass;");
            sb.AppendLine();
            sb.AppendLine("        velocity += acceleration * Time.deltaTime;");
            sb.AppendLine("        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);");
            sb.AppendLine();
            sb.AppendLine("        if (velocity.sqrMagnitude > 0.0001f)");
            sb.AppendLine("        {");
            sb.AppendLine("            transform.position += velocity * Time.deltaTime;");
            sb.AppendLine("            transform.forward = velocity.normalized;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Generate each behavior method
            foreach (var b in behaviors)
            {
                string norm = b.Type.Trim().ToLowerInvariant();
                GenerateBehaviorMethod(sb, norm);
            }

            // Helper: get nearby agents
            bool needsNeighbors = behaviors.Any(b =>
            {
                string n = b.Type.Trim().ToLowerInvariant();
                return n == "separation" || n == "alignment" || n == "cohesion" || n == "leader_follow";
            });
            if (needsNeighbors)
            {
                sb.AppendLine("    private Collider[] GetNeighbors()");
                sb.AppendLine("    {");
                sb.AppendLine("        return Physics.OverlapSphere(transform.position, neighborRadius);");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BehaviorMethodName(string type)
        {
            switch (type)
            {
                case "seek":                return "Seek";
                case "flee":                return "Flee";
                case "arrive":              return "Arrive";
                case "wander":              return "Wander";
                case "pursue":              return "Pursue";
                case "evade":               return "Evade";
                case "obstacle_avoidance":  return "ObstacleAvoidance";
                case "separation":          return "Separation";
                case "alignment":           return "Alignment";
                case "cohesion":            return "Cohesion";
                case "path_follow":         return "PathFollow";
                case "leader_follow":       return "LeaderFollow";
                default:                    return "Unknown";
            }
        }

        private static void GenerateBehaviorMethod(StringBuilder sb, string type)
        {
            string method = BehaviorMethodName(type);

            switch (type)
            {
                case "seek":
                    sb.AppendLine($"    /// <summary>Steer toward target position.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        if (target_seek == null) return Vector3.zero;");
                    sb.AppendLine($"        Vector3 desired = (target_seek.position - transform.position).normalized * maxSpeed;");
                    sb.AppendLine("        return desired - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "flee":
                    sb.AppendLine($"    /// <summary>Steer away from target position.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        if (target_flee == null) return Vector3.zero;");
                    sb.AppendLine($"        Vector3 desired = (transform.position - target_flee.position).normalized * maxSpeed;");
                    sb.AppendLine("        return desired - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "arrive":
                    sb.AppendLine($"    /// <summary>Seek with deceleration near target.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        if (target_arrive == null) return Vector3.zero;");
                    sb.AppendLine($"        Vector3 toTarget = target_arrive.position - transform.position;");
                    sb.AppendLine("        float distance = toTarget.magnitude;");
                    sb.AppendLine("        if (distance < 0.001f) return -velocity;");
                    sb.AppendLine("        float speed = (distance < arriveRadius)");
                    sb.AppendLine("            ? maxSpeed * (distance / arriveRadius)");
                    sb.AppendLine("            : maxSpeed;");
                    sb.AppendLine("        Vector3 desired = toTarget.normalized * speed;");
                    sb.AppendLine("        return desired - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "wander":
                    sb.AppendLine($"    /// <summary>Projected circle + random displacement for natural wandering.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        _wanderTarget += Random.insideUnitSphere * wanderJitter;");
                    sb.AppendLine("        _wanderTarget = _wanderTarget.normalized * wanderRadius;");
                    sb.AppendLine("        Vector3 targetLocal = _wanderTarget + Vector3.forward * wanderDistance;");
                    sb.AppendLine("        Vector3 targetWorld = transform.TransformPoint(targetLocal);");
                    sb.AppendLine("        return (targetWorld - transform.position).normalized * maxSpeed - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "pursue":
                    sb.AppendLine($"    /// <summary>Seek predicted future position of target.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        if (target_pursue == null) return Vector3.zero;");
                    sb.AppendLine($"        Vector3 toTarget = target_pursue.position - transform.position;");
                    sb.AppendLine("        float lookAhead = toTarget.magnitude / maxSpeed;");
                    sb.AppendLine($"        var targetRb = target_pursue.GetComponent<Rigidbody>();");
                    sb.AppendLine("        Vector3 futurePos = targetRb != null");
                    sb.AppendLine($"            ? target_pursue.position + targetRb.linearVelocity * lookAhead");
                    sb.AppendLine($"            : target_pursue.position;");
                    sb.AppendLine("        Vector3 desired = (futurePos - transform.position).normalized * maxSpeed;");
                    sb.AppendLine("        return desired - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "evade":
                    sb.AppendLine($"    /// <summary>Flee from predicted future position of target.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        if (target_evade == null) return Vector3.zero;");
                    sb.AppendLine($"        Vector3 toTarget = target_evade.position - transform.position;");
                    sb.AppendLine("        float lookAhead = toTarget.magnitude / maxSpeed;");
                    sb.AppendLine($"        var targetRb = target_evade.GetComponent<Rigidbody>();");
                    sb.AppendLine("        Vector3 futurePos = targetRb != null");
                    sb.AppendLine($"            ? target_evade.position + targetRb.linearVelocity * lookAhead");
                    sb.AppendLine($"            : target_evade.position;");
                    sb.AppendLine("        Vector3 desired = (transform.position - futurePos).normalized * maxSpeed;");
                    sb.AppendLine("        return desired - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "obstacle_avoidance":
                    sb.AppendLine($"    /// <summary>Raycast ahead and steer perpendicular on hit.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        if (velocity.sqrMagnitude < 0.0001f) return Vector3.zero;");
                    sb.AppendLine("        float lookAhead = velocity.magnitude / maxSpeed * 5f;");
                    sb.AppendLine("        RaycastHit hit;");
                    sb.AppendLine("        if (Physics.Raycast(transform.position, velocity.normalized, out hit, lookAhead))");
                    sb.AppendLine("        {");
                    sb.AppendLine("            Vector3 perpendicular = Vector3.Cross(velocity.normalized, hit.normal);");
                    sb.AppendLine("            Vector3 avoidDir = Vector3.Cross(perpendicular, velocity.normalized).normalized;");
                    sb.AppendLine("            return avoidDir * maxSpeed;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return Vector3.zero;");
                    sb.AppendLine("    }");
                    break;

                case "separation":
                    sb.AppendLine($"    /// <summary>Steer away from nearby agents, weighted by 1/distance.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        Vector3 force = Vector3.zero;");
                    sb.AppendLine("        foreach (var col in GetNeighbors())");
                    sb.AppendLine("        {");
                    sb.AppendLine("            if (col.gameObject == gameObject) continue;");
                    sb.AppendLine("            Vector3 away = transform.position - col.transform.position;");
                    sb.AppendLine("            float dist = away.magnitude;");
                    sb.AppendLine("            if (dist > 0.001f) force += away.normalized / dist;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return force;");
                    sb.AppendLine("    }");
                    break;

                case "alignment":
                    sb.AppendLine($"    /// <summary>Match average heading of nearby agents.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        Vector3 avgHeading = Vector3.zero;");
                    sb.AppendLine("        int count = 0;");
                    sb.AppendLine("        foreach (var col in GetNeighbors())");
                    sb.AppendLine("        {");
                    sb.AppendLine("            if (col.gameObject == gameObject) continue;");
                    sb.AppendLine("            avgHeading += col.transform.forward;");
                    sb.AppendLine("            count++;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        if (count == 0) return Vector3.zero;");
                    sb.AppendLine("        avgHeading /= count;");
                    sb.AppendLine("        return avgHeading.normalized * maxSpeed - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "cohesion":
                    sb.AppendLine($"    /// <summary>Steer toward center of mass of nearby agents.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        Vector3 centerOfMass = Vector3.zero;");
                    sb.AppendLine("        int count = 0;");
                    sb.AppendLine("        foreach (var col in GetNeighbors())");
                    sb.AppendLine("        {");
                    sb.AppendLine("            if (col.gameObject == gameObject) continue;");
                    sb.AppendLine("            centerOfMass += col.transform.position;");
                    sb.AppendLine("            count++;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        if (count == 0) return Vector3.zero;");
                    sb.AppendLine("        centerOfMass /= count;");
                    sb.AppendLine("        return (centerOfMass - transform.position).normalized * maxSpeed - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "path_follow":
                    sb.AppendLine($"    /// <summary>Follow waypoint array sequentially.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        if (waypoints == null || waypoints.Length == 0) return Vector3.zero;");
                    sb.AppendLine("        if (_currentWaypoint >= waypoints.Length) _currentWaypoint = 0;");
                    sb.AppendLine("        Transform wp = waypoints[_currentWaypoint];");
                    sb.AppendLine("        if (wp == null) return Vector3.zero;");
                    sb.AppendLine("        Vector3 toWP = wp.position - transform.position;");
                    sb.AppendLine("        if (toWP.magnitude < 1.5f) _currentWaypoint++;");
                    sb.AppendLine("        return toWP.normalized * maxSpeed - velocity;");
                    sb.AppendLine("    }");
                    break;

                case "leader_follow":
                    sb.AppendLine($"    /// <summary>Arrive at offset behind leader + separation from other followers.</summary>");
                    sb.AppendLine($"    private Vector3 {method}()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        if (target_leader_follow == null) return Vector3.zero;");
                    sb.AppendLine("        // Follow position behind the leader");
                    sb.AppendLine($"        Vector3 behindLeader = target_leader_follow.position - target_leader_follow.forward * 3f;");
                    sb.AppendLine("        Vector3 toTarget = behindLeader - transform.position;");
                    sb.AppendLine("        float distance = toTarget.magnitude;");
                    sb.AppendLine("        float speed = (distance < 5f) ? maxSpeed * (distance / 5f) : maxSpeed;");
                    sb.AppendLine("        Vector3 desired = toTarget.normalized * speed;");
                    sb.AppendLine("        Vector3 arriveForce = desired - velocity;");
                    sb.AppendLine();
                    sb.AppendLine("        // Separation from other followers");
                    sb.AppendLine("        Vector3 sepForce = Vector3.zero;");
                    sb.AppendLine("        foreach (var col in GetNeighbors())");
                    sb.AppendLine("        {");
                    sb.AppendLine("            if (col.gameObject == gameObject) continue;");
                    sb.AppendLine($"            if (col.transform == target_leader_follow) continue;");
                    sb.AppendLine("            Vector3 away = transform.position - col.transform.position;");
                    sb.AppendLine("            float dist = away.magnitude;");
                    sb.AppendLine("            if (dist > 0.001f) sepForce += away.normalized / dist;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return arriveForce + sepForce;");
                    sb.AppendLine("    }");
                    break;
            }

            sb.AppendLine();
        }

        private static string FormatFloat(float value)
        {
            // Ensure 'f' suffix and culture-invariant formatting
            return value.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture) + "f";
        }
    }
}
