#if MOSAIC_HAS_NAVIGATION || UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.AI;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Navigation
{
    public static class NavigationSetDestinationTool
    {
        [MosaicTool("navigation/set-destination",
                    "Sets the destination for a NavMeshAgent (requires play mode)",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavigationSetDestinationResult> Execute(NavigationSetDestinationParams p)
        {
            // Must be in play mode
            if (!Application.isPlaying)
                return ToolResult<NavigationSetDestinationResult>.Fail(
                    "navigation/set-destination requires play mode. Use editor/play-mode to enter play mode first.",
                    ErrorCodes.NOT_PERMITTED,
                    "Call editor/play-mode with action 'Play' before setting a destination.");

            if (p.Destination == null || p.Destination.Length != 3)
                return ToolResult<NavigationSetDestinationResult>.Fail(
                    "Destination must be a float array of length 3 [x, y, z]",
                    ErrorCodes.INVALID_PARAM);

            var go = NavigationToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<NavigationSetDestinationResult>.Fail(
                    NavigationToolHelpers.GameObjectNotFoundMessage(p.InstanceId, p.Name),
                    ErrorCodes.NOT_FOUND);

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null)
                return ToolResult<NavigationSetDestinationResult>.Fail(
                    $"GameObject '{go.name}' does not have a NavMeshAgent component",
                    ErrorCodes.NOT_FOUND);

            if (!agent.isOnNavMesh)
                return ToolResult<NavigationSetDestinationResult>.Fail(
                    $"NavMeshAgent on '{go.name}' is not placed on a NavMesh. Bake the NavMesh first.",
                    ErrorCodes.NOT_PERMITTED,
                    "Call navigation/bake to bake the NavMesh before setting a destination.");

            var dest = new Vector3(p.Destination[0], p.Destination[1], p.Destination[2]);
            agent.SetDestination(dest);

            return ToolResult<NavigationSetDestinationResult>.Ok(new NavigationSetDestinationResult
            {
                InstanceId     = go.GetInstanceID(),
                GameObjectName = go.name,
                Destination    = p.Destination,
                PathPending    = agent.pathPending
            });
        }
    }
}
#endif
