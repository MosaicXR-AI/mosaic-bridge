#if MOSAIC_HAS_NAVIGATION || UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Navigation
{
    public static class NavigationAddObstacleTool
    {
        [MosaicTool("navigation/add-obstacle",
                    "Adds a NavMeshObstacle component to a GameObject",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavigationAddObstacleResult> Execute(NavigationAddObstacleParams p)
        {
            var go = NavigationToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<NavigationAddObstacleResult>.Fail(
                    NavigationToolHelpers.GameObjectNotFoundMessage(p.InstanceId, p.Name),
                    ErrorCodes.NOT_FOUND);

            // Validate shape
            if (string.IsNullOrEmpty(p.Shape))
                return ToolResult<NavigationAddObstacleResult>.Fail(
                    "Shape is required (Box or Capsule)", ErrorCodes.INVALID_PARAM);

            NavMeshObstacleShape shape;
            if (string.Equals(p.Shape, "Box", StringComparison.OrdinalIgnoreCase))
                shape = NavMeshObstacleShape.Box;
            else if (string.Equals(p.Shape, "Capsule", StringComparison.OrdinalIgnoreCase))
                shape = NavMeshObstacleShape.Capsule;
            else
                return ToolResult<NavigationAddObstacleResult>.Fail(
                    $"Invalid shape '{p.Shape}'. Must be 'Box' or 'Capsule'.",
                    ErrorCodes.INVALID_PARAM);

            var obstacle = Undo.AddComponent<NavMeshObstacle>(go);
            obstacle.shape = shape;

            if (p.Size != null && p.Size.Length >= 3)
                obstacle.size = new Vector3(p.Size[0], p.Size[1], p.Size[2]);
            else if (p.Size != null && p.Size.Length == 2 && shape == NavMeshObstacleShape.Capsule)
            {
                // For capsule: [radius, height]
                obstacle.size = new Vector3(p.Size[0] * 2f, p.Size[1], p.Size[0] * 2f);
            }

            if (p.Carve.HasValue)
                obstacle.carving = p.Carve.Value;

            var sz = obstacle.size;
            return ToolResult<NavigationAddObstacleResult>.Ok(new NavigationAddObstacleResult
            {
                InstanceId     = go.GetInstanceID(),
                GameObjectName = go.name,
                Shape          = shape.ToString(),
                Size           = new[] { sz.x, sz.y, sz.z },
                Carve          = obstacle.carving
            });
        }
    }
}
#endif
