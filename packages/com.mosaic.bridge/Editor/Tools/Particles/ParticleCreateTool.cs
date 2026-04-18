using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleCreateTool
    {
        [MosaicTool("particle/create",
                    "Creates a new ParticleSystem in the scene with optional preset configuration",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticleCreateResult> Execute(ParticleCreateParams p)
        {
            string name = string.IsNullOrEmpty(p.Name) ? "Particle System" : p.Name;
            var go = new GameObject(name);

            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            var ps = go.AddComponent<ParticleSystem>();

            // Disable default renderer shape so presets start clean
            string presetUsed = null;
            if (!string.IsNullOrEmpty(p.Preset))
            {
                presetUsed = p.Preset.ToLowerInvariant();
                ApplyPreset(ps, presetUsed);
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create ParticleSystem");

            return ToolResult<ParticleCreateResult>.Ok(new ParticleCreateResult
            {
                InstanceId    = go.GetInstanceID(),
                Name          = go.name,
                HierarchyPath = ParticleToolHelpers.GetHierarchyPath(go.transform),
                Preset        = presetUsed
            });
        }

        private static void ApplyPreset(ParticleSystem ps, string preset)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            switch (preset)
            {
                case "fire":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.6f, 0f, 1f), new Color(1f, 0.2f, 0f, 1f));
                    main.gravityModifier = -0.2f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 30f;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 15f;
                    shape.radius = 0.3f;
                    break;

                case "smoke":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.5f, 0.5f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.3f, 0.3f));
                    main.gravityModifier = -0.05f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 15f;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25f;
                    shape.radius = 0.5f;
                    break;

                case "sparks":
                    main.duration = 2f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.9f, 0.4f, 1f), new Color(1f, 0.6f, 0.1f, 1f));
                    main.gravityModifier = 1f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 50f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.1f;
                    break;

                case "rain":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 12f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.7f, 0.8f, 1f, 0.8f));
                    main.gravityModifier = 0.5f;
                    main.maxParticles = 5000;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 500f;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(10f, 0f, 10f);
                    break;

                case "snow":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 1f, 1f, 0.9f));
                    main.gravityModifier = 0.1f;
                    main.maxParticles = 3000;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 100f;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(10f, 0f, 10f);
                    break;

                case "explosion":
                    main.duration = 0.5f;
                    main.loop = false;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.8f, 0.2f, 1f), new Color(1f, 0.3f, 0f, 1f));
                    main.gravityModifier = 0.5f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 0f;
                    var burst = new ParticleSystem.Burst(0f, 50);
                    emission.SetBursts(new[] { burst });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.2f;
                    break;
            }
        }
    }
}
