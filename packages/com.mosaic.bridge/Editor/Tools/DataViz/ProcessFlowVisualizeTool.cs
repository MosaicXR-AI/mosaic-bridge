using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Visualizes a set of process flows (From -> To GameObject pairs) by spawning a
    /// ParticleSystem per flow that emits particles traveling from origin to destination.
    /// Particle rate scales with flow Value; color may optionally be mapped to value via
    /// a low->high gradient.
    /// </summary>
    public static class ProcessFlowVisualizeTool
    {
        [MosaicTool("process/flow-visualize",
                    "Visualizes process flows between GameObjects using ParticleSystems. Each flow spawns particles traveling from From to To at a rate proportional to Value, with configurable color (or gradient by value), particle size, and speed.",
                    isReadOnly: false, category: "process", Context = ToolContext.Both)]
        public static ToolResult<ProcessFlowVisualizeResult> Execute(ProcessFlowVisualizeParams p)
        {
            if (p == null)
                return ToolResult<ProcessFlowVisualizeResult>.Fail(
                    "Params are required", ErrorCodes.INVALID_PARAM);

            if (p.Flows == null || p.Flows.Count == 0)
                return ToolResult<ProcessFlowVisualizeResult>.Fail(
                    "Flows is required and must be non-empty", ErrorCodes.INVALID_PARAM);

            float rate    = p.ParticleRate > 0f ? p.ParticleRate : 10f;
            float speed   = p.ParticleSpeed > 0f ? p.ParticleSpeed : 2f;
            float size    = p.ParticleSize > 0f ? p.ParticleSize : 0.1f;

            Color flowColor = new Color(0f, 1f, 1f, 1f);
            if (p.FlowColor != null && p.FlowColor.Length >= 3)
            {
                float a = p.FlowColor.Length >= 4 ? p.FlowColor[3] : 1f;
                flowColor = new Color(p.FlowColor[0], p.FlowColor[1], p.FlowColor[2], a);
            }

            // Validate all flows up-front for clean errors.
            foreach (var f in p.Flows)
            {
                if (f == null)
                    return ToolResult<ProcessFlowVisualizeResult>.Fail(
                        "Flow entry is null", ErrorCodes.INVALID_PARAM);
                if (string.IsNullOrWhiteSpace(f.From))
                    return ToolResult<ProcessFlowVisualizeResult>.Fail(
                        "Flow.From is required", ErrorCodes.INVALID_PARAM);
                if (string.IsNullOrWhiteSpace(f.To))
                    return ToolResult<ProcessFlowVisualizeResult>.Fail(
                        "Flow.To is required", ErrorCodes.INVALID_PARAM);

                if (GameObject.Find(f.From) == null)
                    return ToolResult<ProcessFlowVisualizeResult>.Fail(
                        $"Flow.From GameObject '{f.From}' not found", ErrorCodes.NOT_FOUND);
                if (GameObject.Find(f.To) == null)
                    return ToolResult<ProcessFlowVisualizeResult>.Fail(
                        $"Flow.To GameObject '{f.To}' not found", ErrorCodes.NOT_FOUND);
            }

            // Find min/max value for gradient mapping.
            float vMin = float.PositiveInfinity;
            float vMax = float.NegativeInfinity;
            foreach (var f in p.Flows)
            {
                if (f.Value < vMin) vMin = f.Value;
                if (f.Value > vMax) vMax = f.Value;
            }
            if (!float.IsFinite(vMin)) vMin = 0f;
            if (!float.IsFinite(vMax)) vMax = 1f;

            string rootName = string.IsNullOrEmpty(p.Name) ? "ProcessFlowViz" : p.Name;
            var root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Mosaic: Create Process Flow Viz");

            int created = 0;
            int activePs = 0;

            foreach (var f in p.Flows)
            {
                var fromGo = GameObject.Find(f.From);
                var toGo   = GameObject.Find(f.To);
                if (fromGo == null || toGo == null) continue;

                var psGo = new GameObject($"Flow_{f.From}_to_{f.To}");
                psGo.transform.SetParent(root.transform, false);
                psGo.transform.position = fromGo.transform.position;

                var ps = psGo.AddComponent<ParticleSystem>();
                // Stop before configuring so changes apply cleanly.
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                Vector3 fromPos = fromGo.transform.position;
                Vector3 toPos   = toGo.transform.position;
                Vector3 dir     = toPos - fromPos;
                float distance  = dir.magnitude;
                Vector3 vel     = distance > 1e-6f ? dir.normalized * speed : Vector3.forward * speed;

                Color particleColor = flowColor;
                if (p.ColorByValue)
                {
                    float t = vMax > vMin ? (f.Value - vMin) / (vMax - vMin) : 0.5f;
                    // Low->high: blue -> green -> yellow -> red.
                    particleColor = SampleValueGradient(Mathf.Clamp01(t));
                    particleColor.a = flowColor.a;
                }

                var main = ps.main;
                main.startSpeed = speed;
                main.startSize = size;
                main.startColor = particleColor;
                main.startLifetime = distance > 1e-6f ? distance / Mathf.Max(speed, 1e-3f) : 1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Mathf.Max(64, Mathf.CeilToInt(rate * Mathf.Max(1f, f.Value) * main.startLifetime.constant + 16f));

                var emission = ps.emission;
                emission.rateOverTime = rate * Mathf.Max(0f, f.Value);

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                // Initial velocity toward target via a velocity-over-lifetime module.
                var vol = ps.velocityOverLifetime;
                vol.enabled = true;
                vol.space = ParticleSystemSimulationSpace.World;
                vol.x = new ParticleSystem.MinMaxCurve(vel.x);
                vol.y = new ParticleSystem.MinMaxCurve(vel.y);
                vol.z = new ParticleSystem.MinMaxCurve(vel.z);

                // Ensure a basic renderer material so particles are visible.
                var psr = psGo.GetComponent<ParticleSystemRenderer>();
                if (psr != null && psr.sharedMaterial == null)
                {
                    var defaultMat = new Material(Shader.Find("Sprites/Default"));
                    defaultMat.color = particleColor;
                    psr.sharedMaterial = defaultMat;
                }

                ps.Play(true);

                created++;
                activePs++;
            }

            return ToolResult<ProcessFlowVisualizeResult>.Ok(new ProcessFlowVisualizeResult
            {
                GameObjectName = root.name,
                FlowCount = created,
                ActiveParticleSystems = activePs
            });
        }

        // Low (blue) -> mid (green/yellow) -> high (red).
        static Color SampleValueGradient(float t)
        {
            if (t < 0.5f)
            {
                float k = t / 0.5f;
                return Color.Lerp(new Color(0f, 0.4f, 1f, 1f), new Color(0f, 1f, 0f, 1f), k);
            }
            else
            {
                float k = (t - 0.5f) / 0.5f;
                return Color.Lerp(new Color(1f, 1f, 0f, 1f), new Color(1f, 0f, 0f, 1f), k);
            }
        }
    }
}
