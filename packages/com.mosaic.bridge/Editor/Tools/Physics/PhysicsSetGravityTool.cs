using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsSetGravityTool
    {
        [MosaicTool("physics/set-gravity",
                    "Sets the global Physics.gravity vector",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsSetGravityResult> Execute(PhysicsSetGravityParams p)
        {
            if (p.Gravity == null || p.Gravity.Length != 3)
                return ToolResult<PhysicsSetGravityResult>.Fail(
                    "Gravity must be a float[3] array", ErrorCodes.INVALID_PARAM);

            // Record undo on the PhysicsManager asset so Ctrl+Z restores previous gravity
            var physicsManager = AssetDatabase.LoadAssetAtPath<Object>(
                "ProjectSettings/DynamicsManager.asset");
            if (physicsManager != null)
                Undo.RecordObject(physicsManager, "Mosaic: Set Gravity");

            UnityEngine.Physics.gravity = new Vector3(p.Gravity[0], p.Gravity[1], p.Gravity[2]);

            return ToolResult<PhysicsSetGravityResult>.Ok(new PhysicsSetGravityResult
            {
                Gravity = PhysicsToolHelpers.ToFloatArray(UnityEngine.Physics.gravity)
            });
        }
    }
}
