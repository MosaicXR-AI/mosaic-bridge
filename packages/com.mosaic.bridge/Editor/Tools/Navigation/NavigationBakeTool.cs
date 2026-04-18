#if MOSAIC_HAS_NAVIGATION || UNITY_6000_0_OR_NEWER

using UnityEngine.AI;
using UnityEditor.AI;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Navigation
{
    public static class NavigationBakeTool
    {
        [MosaicTool("navigation/bake",
                    "Bakes the NavMesh for the current scene. Optional params override the default agent settings for this bake.",
                    isReadOnly: false)]
        public static ToolResult<NavigationBakeResult> Execute(NavigationBakeParams p)
        {
            // Read the default Humanoid agent settings (index 0)
            var settings = NavMesh.GetSettingsByIndex(0);

            // Override with user-supplied values if provided
            if (p.AgentRadius.HasValue)
                settings.agentRadius = p.AgentRadius.Value;
            if (p.AgentHeight.HasValue)
                settings.agentHeight = p.AgentHeight.Value;
            if (p.StepHeight.HasValue)
                settings.agentClimb = p.StepHeight.Value;
            if (p.SlopeAngle.HasValue)
                settings.agentSlope = p.SlopeAngle.Value;

            // Bake using the legacy NavMeshBuilder (works with the Navigation window settings)
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            // Verify bake succeeded by checking triangulation
            var triangulation = NavMesh.CalculateTriangulation();
            bool hasMesh = triangulation.vertices != null && triangulation.vertices.Length > 0;

            return ToolResult<NavigationBakeResult>.Ok(new NavigationBakeResult
            {
                Baked       = hasMesh,
                AgentRadius = settings.agentRadius,
                AgentHeight = settings.agentHeight,
                StepHeight  = settings.agentClimb,
                SlopeAngle  = settings.agentSlope
            });
        }
    }
}
#endif
