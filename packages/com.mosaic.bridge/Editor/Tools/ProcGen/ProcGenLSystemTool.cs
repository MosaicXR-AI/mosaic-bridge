using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public static class ProcGenLSystemTool
    {
        private static readonly string[] ValidPresets = { "tree", "fern", "bush", "coral", "vine" };

        // ═══════════════════════════════════════════════════════════════════
        //  Presets
        // ═══════════════════════════════════════════════════════════════════

        private static void ApplyPreset(string preset, out string axiom, out List<LSystemRule> rules, out float angle)
        {
            switch (preset)
            {
                case "tree":
                    axiom = "F";
                    rules = new List<LSystemRule>
                    {
                        new LSystemRule { Symbol = "F", Replacement = "FF+[+F-F-F]-[-F+F+F]" }
                    };
                    angle = 22.5f;
                    break;

                case "fern":
                    axiom = "X";
                    rules = new List<LSystemRule>
                    {
                        new LSystemRule { Symbol = "X", Replacement = "F+[[X]-X]-F[-FX]+X" },
                        new LSystemRule { Symbol = "F", Replacement = "FF" }
                    };
                    angle = 25f;
                    break;

                case "bush":
                    axiom = "F";
                    rules = new List<LSystemRule>
                    {
                        new LSystemRule { Symbol = "F", Replacement = "F[+F]F[-F]F" }
                    };
                    angle = 25.7f;
                    break;

                case "coral":
                    axiom = "F";
                    rules = new List<LSystemRule>
                    {
                        new LSystemRule { Symbol = "F", Replacement = "FF-[-F+F+F]+[+F-F-F]" }
                    };
                    angle = 22.5f;
                    break;

                case "vine":
                    axiom = "F";
                    rules = new List<LSystemRule>
                    {
                        new LSystemRule { Symbol = "F", Replacement = "F[+F][-F]F" }
                    };
                    angle = 20f;
                    break;

                default:
                    axiom = "F";
                    rules = new List<LSystemRule>();
                    angle = 25f;
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Tool entry point
        // ═══════════════════════════════════════════════════════════════════

        [MosaicTool("procgen/lsystem",
                    "Generates procedural vegetation using L-system string rewriting with turtle-graphics mesh generation",
                    isReadOnly: false, category: "procgen")]
        public static ToolResult<ProcGenLSystemResult> Execute(ProcGenLSystemParams p)
        {
            // --- Resolve preset or custom params ---
            string axiom;
            List<LSystemRule> rules;
            float angle;
            string usedPreset = null;

            if (!string.IsNullOrEmpty(p.Preset))
            {
                string preset = p.Preset.Trim().ToLowerInvariant();
                if (Array.IndexOf(ValidPresets, preset) < 0)
                    return ToolResult<ProcGenLSystemResult>.Fail(
                        $"Invalid Preset '{p.Preset}'. Valid: tree, fern, bush, coral, vine.",
                        ErrorCodes.INVALID_PARAM);

                ApplyPreset(preset, out axiom, out rules, out angle);
                usedPreset = preset;

                // Allow overrides on top of preset
                if (!string.IsNullOrEmpty(p.Axiom)) axiom = p.Axiom;
                if (p.Rules != null && p.Rules.Count > 0) rules = p.Rules;
                if (p.Angle.HasValue) angle = p.Angle.Value;
            }
            else
            {
                axiom = p.Axiom ?? "F";
                rules = p.Rules ?? new List<LSystemRule>();
                angle = p.Angle ?? 25f;
            }

            int   iterations    = Mathf.Clamp(p.Iterations ?? 4, 1, 8);
            float stepLength    = p.StepLength ?? 1.0f;
            float lengthDecay   = p.LengthDecay ?? 0.8f;
            float radiusDecay   = p.RadiusDecay ?? 0.7f;
            float initialRadius = p.InitialRadius ?? 0.1f;
            bool  generateMesh  = p.GenerateMesh ?? true;
            string goName       = p.Name ?? "LSystem";

            // Validate rules
            if (rules.Count > 0)
            {
                foreach (var rule in rules)
                {
                    if (string.IsNullOrEmpty(rule.Symbol) || rule.Symbol.Length != 1)
                        return ToolResult<ProcGenLSystemResult>.Fail(
                            $"Each rule Symbol must be exactly one character. Got '{rule.Symbol}'.",
                            ErrorCodes.INVALID_PARAM);

                    if (string.IsNullOrEmpty(rule.Replacement))
                        return ToolResult<ProcGenLSystemResult>.Fail(
                            $"Rule for symbol '{rule.Symbol}' has an empty Replacement.",
                            ErrorCodes.INVALID_PARAM);

                    float prob = rule.Probability ?? 1.0f;
                    if (prob < 0f || prob > 1f)
                        return ToolResult<ProcGenLSystemResult>.Fail(
                            $"Rule probability for '{rule.Symbol}' must be between 0 and 1. Got {prob}.",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            // Seed
            int seed = p.Seed ?? new System.Random().Next();
            var rng  = new System.Random(seed);

            // --- String expansion ---
            string expanded = ExpandString(axiom, rules, iterations, rng);

            if (!generateMesh)
            {
                int branchCountNoMesh = CountBranches(expanded);
                string truncated = expanded.Length > 1000
                    ? expanded.Substring(0, 1000) + "..."
                    : expanded;

                return ToolResult<ProcGenLSystemResult>.Ok(new ProcGenLSystemResult
                {
                    GeneratedString = truncated,
                    StringLength    = expanded.Length,
                    GameObjectName  = null,
                    InstanceId      = 0,
                    VertexCount     = 0,
                    BranchCount     = branchCountNoMesh,
                    Iterations      = iterations,
                    Preset          = usedPreset
                });
            }

            // --- Turtle interpretation & mesh generation ---
            var meshData = InterpretTurtle(expanded, angle, stepLength, lengthDecay,
                                           radiusDecay, initialRadius, iterations);

            // Build Unity mesh
            var mesh = new Mesh { name = goName + "_Mesh" };
            if (meshData.Vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(meshData.Vertices);
            mesh.SetTriangles(meshData.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Create GameObject
            var go = new GameObject(goName);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            // Assign a default material
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.35f, 0.55f, 0.2f); // green-brown
            mr.sharedMaterial = mat;

            // Position
            if (p.Position != null && p.Position.Length >= 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            // Parent
            if (!string.IsNullOrEmpty(p.ParentObject))
            {
                var parent = GameObject.Find(p.ParentObject);
                if (parent != null)
                    go.transform.SetParent(parent.transform, true);
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic L-System Generate");

            string truncatedStr = expanded.Length > 1000
                ? expanded.Substring(0, 1000) + "..."
                : expanded;

            return ToolResult<ProcGenLSystemResult>.Ok(new ProcGenLSystemResult
            {
                GeneratedString = truncatedStr,
                StringLength    = expanded.Length,
                GameObjectName  = go.name,
                InstanceId      = go.GetInstanceID(),
                VertexCount     = meshData.Vertices.Count,
                BranchCount     = meshData.BranchCount,
                Iterations      = iterations,
                Preset          = usedPreset
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        //  String expansion
        // ═══════════════════════════════════════════════════════════════════

        internal static string ExpandString(string axiom, List<LSystemRule> rules, int iterations, System.Random rng)
        {
            string current = axiom;

            // Group rules by symbol for efficient lookup (supports stochastic: multiple rules per symbol)
            var ruleMap = new Dictionary<char, List<LSystemRule>>();
            foreach (var rule in rules)
            {
                char sym = rule.Symbol[0];
                if (!ruleMap.ContainsKey(sym))
                    ruleMap[sym] = new List<LSystemRule>();
                ruleMap[sym].Add(rule);
            }

            for (int iter = 0; iter < iterations; iter++)
            {
                var sb = new StringBuilder(current.Length * 2);
                foreach (char c in current)
                {
                    if (ruleMap.TryGetValue(c, out var matching))
                    {
                        string replacement = PickRule(matching, rng);
                        if (replacement != null)
                            sb.Append(replacement);
                        else
                            sb.Append(c);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                current = sb.ToString();

                // Safety: cap string length to prevent runaway expansion
                if (current.Length > 500000)
                    break;
            }

            return current;
        }

        private static string PickRule(List<LSystemRule> candidates, System.Random rng)
        {
            if (candidates.Count == 1)
            {
                float prob = candidates[0].Probability ?? 1.0f;
                if (prob >= 1.0f || (float)rng.NextDouble() < prob)
                    return candidates[0].Replacement;
                return null;
            }

            // Stochastic: pick based on weighted probability
            float roll = (float)rng.NextDouble();
            float cumulative = 0f;
            foreach (var rule in candidates)
            {
                cumulative += rule.Probability ?? 1.0f;
                if (roll < cumulative)
                    return rule.Replacement;
            }

            // Fallback: return last rule's replacement
            return candidates[candidates.Count - 1].Replacement;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Turtle interpretation
        // ═══════════════════════════════════════════════════════════════════

        private struct TurtleState
        {
            public Vector3    Position;
            public Quaternion Rotation;
            public float      Radius;
            public float      StepLength;
        }

        private class TurtleMeshData
        {
            public List<Vector3> Vertices  = new List<Vector3>();
            public List<int>     Triangles = new List<int>();
            public int           BranchCount;
        }

        private const int CylinderSides = 6;

        private static TurtleMeshData InterpretTurtle(string lString, float angle, float stepLength,
                                                       float lengthDecay, float radiusDecay,
                                                       float initialRadius, int iterations)
        {
            var data = new TurtleMeshData();
            var stack = new Stack<TurtleState>();

            // Apply length decay based on total iterations to get initial effective step
            float effectiveStep = stepLength;
            for (int i = 0; i < iterations; i++)
                effectiveStep *= lengthDecay;

            var state = new TurtleState
            {
                Position   = Vector3.zero,
                Rotation   = Quaternion.identity,
                Radius     = initialRadius,
                StepLength = effectiveStep
            };

            foreach (char c in lString)
            {
                switch (c)
                {
                    case 'F':
                    {
                        Vector3 dir = state.Rotation * Vector3.up;
                        Vector3 endPos = state.Position + dir * state.StepLength;
                        AddCylinderSegment(data, state.Position, endPos, state.Radius, state.Rotation);
                        state.Position = endPos;
                        data.BranchCount++;
                        break;
                    }

                    case '+':
                        state.Rotation *= Quaternion.Euler(0f, 0f, -angle);
                        break;

                    case '-':
                        state.Rotation *= Quaternion.Euler(0f, 0f, angle);
                        break;

                    case '&':
                        state.Rotation *= Quaternion.Euler(angle, 0f, 0f);
                        break;

                    case '^':
                        state.Rotation *= Quaternion.Euler(-angle, 0f, 0f);
                        break;

                    case '[':
                        stack.Push(state);
                        state.Radius *= radiusDecay;
                        break;

                    case ']':
                        if (stack.Count > 0)
                            state = stack.Pop();
                        break;
                }
            }

            return data;
        }

        private static void AddCylinderSegment(TurtleMeshData data, Vector3 start, Vector3 end,
                                                 float radius, Quaternion rotation)
        {
            int baseIndex = data.Vertices.Count;

            // Generate ring vertices at start and end
            for (int i = 0; i < CylinderSides; i++)
            {
                float theta = 2f * Mathf.PI * i / CylinderSides;
                Vector3 offset = rotation * new Vector3(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius);
                data.Vertices.Add(start + offset);
                data.Vertices.Add(end + offset);
            }

            // Generate triangles connecting the two rings
            for (int i = 0; i < CylinderSides; i++)
            {
                int curr0 = baseIndex + i * 2;       // start ring vertex
                int curr1 = baseIndex + i * 2 + 1;   // end ring vertex
                int next0 = baseIndex + ((i + 1) % CylinderSides) * 2;
                int next1 = baseIndex + ((i + 1) % CylinderSides) * 2 + 1;

                // Two triangles per quad
                data.Triangles.Add(curr0);
                data.Triangles.Add(curr1);
                data.Triangles.Add(next0);

                data.Triangles.Add(next0);
                data.Triangles.Add(curr1);
                data.Triangles.Add(next1);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════════

        private static int CountBranches(string lString)
        {
            int count = 0;
            foreach (char c in lString)
            {
                if (c == 'F') count++;
            }
            return count;
        }
    }
}
