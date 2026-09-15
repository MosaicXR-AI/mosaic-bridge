using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsLayerCollisionTool
    {
        [MosaicTool("physics/layer-collision",
                    "Gets/sets whether two layers can physically collide (Physics.IgnoreLayerCollision), by " +
                    "layer name, e.g. \"collectables ignore Enemy\". Persists in edit mode (Undo on " +
                    "ProjectSettings/DynamicsManager.asset), matching Unity's own Physics settings panel. " +
                    "'matrix' lists every currently-ignored pair.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsLayerCollisionResult> Execute(PhysicsLayerCollisionParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "get": return Get(p);
                case "set": return Set(p);
                case "matrix": return Matrix();
                default:
                    return ToolResult<PhysicsLayerCollisionResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: get, set, matrix", ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<PhysicsLayerCollisionResult> Get(PhysicsLayerCollisionParams p)
        {
            if (!TryResolveLayers(p.LayerA, p.LayerB, out int a, out int b, out var error))
                return ToolResult<PhysicsLayerCollisionResult>.Fail(error, ErrorCodes.INVALID_PARAM);

            bool ignored = UnityEngine.Physics.GetIgnoreLayerCollision(a, b);
            return ToolResult<PhysicsLayerCollisionResult>.Ok(new PhysicsLayerCollisionResult
            {
                Action = "get", LayerA = p.LayerA, LayerB = p.LayerB, CanCollide = !ignored,
                Message = $"{p.LayerA} and {p.LayerB} {(ignored ? "do NOT" : "can")} collide.",
            });
        }

        private static ToolResult<PhysicsLayerCollisionResult> Set(PhysicsLayerCollisionParams p)
        {
            if (!p.CanCollide.HasValue)
                return ToolResult<PhysicsLayerCollisionResult>.Fail("CanCollide is required for set", ErrorCodes.INVALID_PARAM);
            if (!TryResolveLayers(p.LayerA, p.LayerB, out int a, out int b, out var error))
                return ToolResult<PhysicsLayerCollisionResult>.Fail(error, ErrorCodes.INVALID_PARAM);

            var physicsManager = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/DynamicsManager.asset");
            if (physicsManager != null)
                Undo.RecordObject(physicsManager, "Mosaic: Set Layer Collision");

            UnityEngine.Physics.IgnoreLayerCollision(a, b, !p.CanCollide.Value);
            if (physicsManager != null) EditorUtility.SetDirty(physicsManager);

            return ToolResult<PhysicsLayerCollisionResult>.Ok(new PhysicsLayerCollisionResult
            {
                Action = "set", LayerA = p.LayerA, LayerB = p.LayerB, CanCollide = p.CanCollide.Value,
                Message = $"{p.LayerA} and {p.LayerB} {(p.CanCollide.Value ? "now collide" : "now ignore each other")}.",
            });
        }

        private static ToolResult<PhysicsLayerCollisionResult> Matrix()
        {
            var ignored = new List<LayerCollisionIgnoredPair>();
            for (int a = 0; a < 32; a++)
            {
                var nameA = LayerMask.LayerToName(a);
                if (string.IsNullOrEmpty(nameA)) continue;
                for (int b = a + 1; b < 32; b++)
                {
                    var nameB = LayerMask.LayerToName(b);
                    if (string.IsNullOrEmpty(nameB)) continue;
                    if (UnityEngine.Physics.GetIgnoreLayerCollision(a, b))
                        ignored.Add(new LayerCollisionIgnoredPair { LayerA = nameA, LayerB = nameB });
                }
            }

            return ToolResult<PhysicsLayerCollisionResult>.Ok(new PhysicsLayerCollisionResult
            {
                Action = "matrix", IgnoredPairs = ignored.ToArray(),
                Message = $"{ignored.Count} ignored layer pair(s).",
            });
        }

        private static bool TryResolveLayers(string layerA, string layerB, out int a, out int b, out string error)
        {
            a = b = -1;
            if (string.IsNullOrEmpty(layerA) || string.IsNullOrEmpty(layerB))
            {
                error = "LayerA and LayerB are required";
                return false;
            }
            a = LayerMask.NameToLayer(layerA);
            if (a < 0) { error = $"Unknown layer '{layerA}'"; return false; }
            b = LayerMask.NameToLayer(layerB);
            if (b < 0) { error = $"Unknown layer '{layerB}'"; return false; }
            error = null;
            return true;
        }
    }
}
