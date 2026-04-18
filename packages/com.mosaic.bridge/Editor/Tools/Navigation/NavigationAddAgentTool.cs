#if MOSAIC_HAS_NAVIGATION || UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Navigation
{
    public static class NavigationAddAgentTool
    {
        [MosaicTool("navigation/add-agent",
                    "Adds a NavMeshAgent component to a GameObject",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavigationAddAgentResult> Execute(NavigationAddAgentParams p)
        {
            var go = NavigationToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<NavigationAddAgentResult>.Fail(
                    NavigationToolHelpers.GameObjectNotFoundMessage(p.InstanceId, p.Name),
                    ErrorCodes.NOT_FOUND);

            var agent = Undo.AddComponent<NavMeshAgent>(go);

            if (p.Speed.HasValue)
                agent.speed = p.Speed.Value;
            if (p.AngularSpeed.HasValue)
                agent.angularSpeed = p.AngularSpeed.Value;
            if (p.Radius.HasValue)
                agent.radius = p.Radius.Value;
            if (p.Height.HasValue)
                agent.height = p.Height.Value;
            if (p.StoppingDistance.HasValue)
                agent.stoppingDistance = p.StoppingDistance.Value;

            return ToolResult<NavigationAddAgentResult>.Ok(new NavigationAddAgentResult
            {
                InstanceId       = go.GetInstanceID(),
                GameObjectName   = go.name,
                Speed            = agent.speed,
                AngularSpeed     = agent.angularSpeed,
                Radius           = agent.radius,
                Height           = agent.height,
                StoppingDistance  = agent.stoppingDistance
            });
        }
    }
}
#endif
