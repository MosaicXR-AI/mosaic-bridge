using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Terrains;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectSnapToGroundTool
    {
        [MosaicTool("gameobject/snap-to-ground",
                    "Snaps a GameObject's Y position to the terrain surface (or nearest physics surface) at its current XZ. " +
                    "Use after bulk-placing objects to fix Y=0 placements, or after terrain sculpting changes ground level. " +
                    "SnapMode=terrain uses Unity Terrain.SampleHeight (fast, no play mode needed). " +
                    "SnapMode=raycast fires a downward physics ray (works for non-terrain floors, buildings, bridges).",
                    isReadOnly: false)]
        public static ToolResult<GameObjectSnapToGroundResult> Execute(GameObjectSnapToGroundParams p)
        {
            // Resolve target GameObject
            GameObject go;
            if (p.InstanceId != 0)
            {
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId) as GameObject;
                if (go == null)
                    return ToolResult<GameObjectSnapToGroundResult>.Fail(
                        $"No GameObject found with InstanceId {p.InstanceId}", ErrorCodes.NOT_FOUND);
            }
            else if (!string.IsNullOrEmpty(p.GameObjectPath))
            {
                go = GameObject.Find(p.GameObjectPath);
                if (go == null)
                    return ToolResult<GameObjectSnapToGroundResult>.Fail(
                        $"No GameObject found at path '{p.GameObjectPath}'", ErrorCodes.NOT_FOUND);
            }
            else
            {
                return ToolResult<GameObjectSnapToGroundResult>.Fail(
                    "Either GameObjectPath or InstanceId must be provided", ErrorCodes.INVALID_PARAM);
            }

            float prevY = go.transform.position.y;
            float x     = go.transform.position.x;
            float z     = go.transform.position.z;
            float groundY;
            string modeUsed;

            if (p.SnapMode == "raycast")
            {
                int mask = p.LayerMask == 0 ? ~0 : p.LayerMask;
                var ray = new Ray(new Vector3(x, prevY + 1000f, z), Vector3.down);
                if (UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 2000f, mask))
                {
                    groundY  = hit.point.y;
                    modeUsed = "raycast";
                }
                else
                {
                    return ToolResult<GameObjectSnapToGroundResult>.Fail(
                        $"Raycast downward from ({x}, {prevY + 1000f}, {z}) found no collider. " +
                        "Check that ground geometry has a collider, or use SnapMode=terrain.",
                        ErrorCodes.NOT_FOUND);
                }
            }
            else
            {
                // Terrain mode (default)
                UnityEngine.Terrain terrain;
                if (!string.IsNullOrEmpty(p.TerrainName))
                {
                    terrain = TerrainToolHelpers.ResolveTerrain(0, p.TerrainName, out string err);
                    if (terrain == null)
                        return ToolResult<GameObjectSnapToGroundResult>.Fail(err, ErrorCodes.NOT_FOUND);
                }
                else
                {
                    terrain = UnityEngine.Terrain.activeTerrain;
                    if (terrain == null)
                        return ToolResult<GameObjectSnapToGroundResult>.Fail(
                            "No active terrain in scene. Provide TerrainName or use SnapMode=raycast.",
                            ErrorCodes.NOT_FOUND);
                }

                groundY  = terrain.SampleHeight(new Vector3(x, 0f, z));
                modeUsed = "terrain";
            }

            float newY = groundY + p.YOffset;

            Undo.RecordObject(go.transform, "Mosaic: Snap To Ground");
            go.transform.position = new Vector3(x, newY, z);

            return ToolResult<GameObjectSnapToGroundResult>.Ok(new GameObjectSnapToGroundResult
            {
                GameObjectName = go.name,
                HierarchyPath  = GameObjectToolHelpers.GetHierarchyPath(go.transform),
                PreviousY      = prevY,
                NewY           = newY,
                TerrainHeight  = groundY,
                YOffset        = p.YOffset,
                SnapMode       = modeUsed
            });
        }
    }
}
