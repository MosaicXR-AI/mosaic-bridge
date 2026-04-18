#if MOSAIC_HAS_NAVIGATION || UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Navigation
{
    public static class NavigationInfoTool
    {
        [MosaicTool("navigation/info",
                    "Queries the current NavMesh state including bake status, agent count, obstacle count, and areas",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<NavigationInfoResult> Execute(NavigationInfoParams p)
        {
            // Check if NavMesh is baked
            var triangulation = NavMesh.CalculateTriangulation();
            bool hasBaked = triangulation.vertices != null && triangulation.vertices.Length > 0;

            // Count agents and obstacles in the scene
            var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            var obstacles = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None);

            // Collect named NavMesh areas
            string[] areaNames = NavMesh.GetAreaNames();
            var areas = new List<string>();
            foreach (string name in areaNames)
            {
                if (!string.IsNullOrEmpty(name))
                    areas.Add(name);
            }

            return ToolResult<NavigationInfoResult>.Ok(new NavigationInfoResult
            {
                HasBakedNavMesh = hasBaked,
                AgentCount      = agents.Length,
                ObstacleCount   = obstacles.Length,
                Areas           = areas.ToArray()
            });
        }
    }
}
#endif
