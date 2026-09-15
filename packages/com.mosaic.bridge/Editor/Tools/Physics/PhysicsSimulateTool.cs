using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsSimulateTool
    {
        [MosaicTool("physics/simulate",
                    "Runs Steps fixed-timestep physics steps in edit mode (Physics.simulationMode=Script + " +
                    "Physics.Simulate), e.g. 'drop crates and verify they land in the pit' without entering " +
                    "Play Mode. Targets lists which Rigidbodies should actually move — every other Rigidbody " +
                    "in the scene is temporarily forced kinematic and restored afterward. Reports before/after " +
                    "position+rotation. Refuses while Play Mode is active. No collision/trigger callbacks fire.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsSimulateResult> Execute(PhysicsSimulateParams p)
        {
            if (EditorApplication.isPlaying)
                return ToolResult<PhysicsSimulateResult>.Fail(
                    "physics/simulate cannot run while Play Mode is active — Unity's own play-mode " +
                    "simulation is already stepping physics.", ErrorCodes.NOT_PERMITTED);
            if (p.Steps <= 0)
                return ToolResult<PhysicsSimulateResult>.Fail("Steps must be > 0", ErrorCodes.INVALID_PARAM);

            float stepSize = p.StepSize ?? Time.fixedDeltaTime;
            var allBodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);

            var targetNames = p.Targets != null ? new HashSet<string>(p.Targets) : null;
            var forcedKinematic = new List<(Rigidbody body, bool originalKinematic)>();
            if (targetNames != null)
            {
                foreach (var body in allBodies)
                {
                    if (!targetNames.Contains(body.gameObject.name) && !body.isKinematic)
                    {
                        forcedKinematic.Add((body, body.isKinematic));
                        body.isKinematic = true;
                    }
                }
            }

            var poses = allBodies.Select(body => new PhysicsBodyPose
            {
                GameObjectName = body.gameObject.name,
                PositionBefore = PhysicsToolHelpers.ToFloatArray(body.transform.position),
                RotationBefore = PhysicsToolHelpers.ToFloatArray(body.transform.eulerAngles),
            }).ToArray();

            var originalMode = UnityEngine.Physics.simulationMode;
            try
            {
                UnityEngine.Physics.simulationMode = SimulationMode.Script;
                for (int i = 0; i < p.Steps; i++)
                    UnityEngine.Physics.Simulate(stepSize);
            }
            finally
            {
                UnityEngine.Physics.simulationMode = originalMode;
                foreach (var (body, originalKinematic) in forcedKinematic)
                    if (body != null) body.isKinematic = originalKinematic;
            }

            foreach (var pose in poses)
            {
                var body = allBodies.First(b => b.gameObject.name == pose.GameObjectName);
                pose.PositionAfter = PhysicsToolHelpers.ToFloatArray(body.transform.position);
                pose.RotationAfter = PhysicsToolHelpers.ToFloatArray(body.transform.eulerAngles);
            }

            return ToolResult<PhysicsSimulateResult>.Ok(new PhysicsSimulateResult
            {
                StepsSimulated = p.Steps,
                TotalTime      = p.Steps * stepSize,
                Poses          = poses,
                Message        = $"Simulated {p.Steps} step(s) of {stepSize}s across {allBodies.Length} Rigidbody(ies).",
            });
        }
    }
}
