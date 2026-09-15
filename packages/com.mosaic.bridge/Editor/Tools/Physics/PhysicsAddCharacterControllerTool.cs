using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsAddCharacterControllerTool
    {
        [MosaicTool("physics/add-character-controller",
                    "Adds a CharacterController, auto-fit (center/radius/height) to the GameObject's mesh/" +
                    "renderer bounds unless explicitly overridden. Refuses if a Rigidbody is already present " +
                    "(the classic beginner conflict — CharacterController does its own movement) unless " +
                    "RemoveExistingRigidbody=true.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsAddCharacterControllerResult> Execute(PhysicsAddCharacterControllerParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsAddCharacterControllerResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            if (go.GetComponent<CharacterController>() != null)
                return ToolResult<PhysicsAddCharacterControllerResult>.Fail(
                    $"GameObject '{go.name}' already has a CharacterController.", ErrorCodes.CONFLICT);

            bool rigidbodyRemoved = false;
            var existingRb = go.GetComponent<Rigidbody>();
            if (existingRb != null)
            {
                if (!p.RemoveExistingRigidbody)
                    return ToolResult<PhysicsAddCharacterControllerResult>.Fail(
                        $"GameObject '{go.name}' has a Rigidbody, which conflicts with CharacterController's " +
                        "own movement. Set RemoveExistingRigidbody=true to remove it, or remove it manually first.",
                        ErrorCodes.CONFLICT);
                Undo.DestroyObjectImmediate(existingRb);
                rigidbodyRemoved = true;
            }

            var controller = Undo.AddComponent<CharacterController>(go);
            AutoFit(go, controller);

            if (p.Center != null)
            {
                if (p.Center.Length != 3)
                    return ToolResult<PhysicsAddCharacterControllerResult>.Fail(
                        "Center requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                controller.center = new Vector3(p.Center[0], p.Center[1], p.Center[2]);
            }
            if (p.Radius.HasValue) controller.radius = p.Radius.Value;
            if (p.Height.HasValue) controller.height = p.Height.Value;
            if (p.SlopeLimit.HasValue) controller.slopeLimit = p.SlopeLimit.Value;
            if (p.StepOffset.HasValue) controller.stepOffset = p.StepOffset.Value;
            if (p.SkinWidth.HasValue) controller.skinWidth = p.SkinWidth.Value;
            if (p.MinMoveDistance.HasValue) controller.minMoveDistance = p.MinMoveDistance.Value;

            return ToolResult<PhysicsAddCharacterControllerResult>.Ok(new PhysicsAddCharacterControllerResult
            {
                GameObjectName   = go.name,
                InstanceId       = UnityIds.Of(go),
                Center           = PhysicsToolHelpers.ToFloatArray(controller.center),
                Radius           = controller.radius,
                Height           = controller.height,
                SlopeLimit       = controller.slopeLimit,
                StepOffset       = controller.stepOffset,
                SkinWidth        = controller.skinWidth,
                MinMoveDistance  = controller.minMoveDistance,
                RigidbodyRemoved = rigidbodyRemoved,
            });
        }

        private static void AutoFit(GameObject go, CharacterController controller)
        {
            Bounds localBounds;
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
                localBounds = meshFilter.sharedMesh.bounds;
            else
            {
                var skinned = go.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && skinned.sharedMesh != null)
                    localBounds = skinned.sharedMesh.bounds;
                else
                    return; // keep CharacterController's own defaults
            }

            controller.center = localBounds.center;
            controller.height = localBounds.size.y;
            controller.radius = Mathf.Max(localBounds.extents.x, localBounds.extents.z);
        }
    }
}
