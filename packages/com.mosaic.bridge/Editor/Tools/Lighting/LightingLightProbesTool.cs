using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingLightProbesTool
    {
        [MosaicTool("lighting/light-probes",
                    "create: makes a new LightProbeGroup GameObject. grid: fills BoundsMin/Max with " +
                    "a grid at Spacing — RaycastAboveGround places each column just above the actual " +
                    "ground surface (HeightOffset) instead of filling Y blindly. add-positions: " +
                    "appends explicit [x,y,z,...] positions. clear: empties the group. Dynamic " +
                    "objects need probes to be lit correctly in baked scenes — nothing reads or " +
                    "writes probes without this.",
                    isReadOnly: false)]
        public static ToolResult<LightingLightProbesResult> Execute(LightingLightProbesParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":        return Create(p);
                case "grid":          return Grid(p);
                case "add-positions": return AddPositions(p);
                case "clear":         return Clear(p);
                default:
                    return ToolResult<LightingLightProbesResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: create, grid, add-positions, clear",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<LightingLightProbesResult> Create(LightingLightProbesParams p)
        {
            var go = new GameObject(p.Name);
            var group = go.AddComponent<LightProbeGroup>();
            // AddComponent seeds 8 default cube-corner positions (via LightProbeGroup's own
            // Reset()) — surprising and undocumented if left in place; a fresh group from this
            // tool should start genuinely empty so grid/add-positions are the only source of probes.
            group.probePositions = new Vector3[0];

            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Light Probe Group");

            return ToolResult<LightingLightProbesResult>.Ok(new LightingLightProbesResult
            {
                Action = "create", InstanceId = UnityIds.Of(go), Name = go.name,
                ProbeCount = group.probePositions.Length,
            });
        }

        private static ToolResult<LightingLightProbesResult> Grid(LightingLightProbesParams p)
        {
            if (!TryResolveGroup(p.Name, out var group, out var groupError))
                return ToolResult<LightingLightProbesResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            if (p.BoundsMin == null || p.BoundsMin.Length != 3 || p.BoundsMax == null || p.BoundsMax.Length != 3)
                return ToolResult<LightingLightProbesResult>.Fail(
                    "BoundsMin and BoundsMax each require exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
            if (p.Spacing == null || p.Spacing.Length != 3 || p.Spacing[0] <= 0 || p.Spacing[1] <= 0 || p.Spacing[2] <= 0)
                return ToolResult<LightingLightProbesResult>.Fail(
                    "Spacing requires exactly [x, y, z], all positive", ErrorCodes.INVALID_PARAM);

            var min = new Vector3(p.BoundsMin[0], p.BoundsMin[1], p.BoundsMin[2]);
            var max = new Vector3(p.BoundsMax[0], p.BoundsMax[1], p.BoundsMax[2]);
            var layerMask = string.IsNullOrEmpty(p.GroundLayerMask)
                ? ~0
                : LayerMask.GetMask(System.Array.ConvertAll(p.GroundLayerMask.Split(','), s => s.Trim()));

            var positions = new List<Vector3>();
            for (var x = min.x; x <= max.x; x += p.Spacing[0])
            {
                for (var z = min.z; z <= max.z; z += p.Spacing[2])
                {
                    if (p.RaycastAboveGround)
                    {
                        var origin = new Vector3(x, max.y, z);
                        if (UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit, max.y - min.y + 1f, layerMask))
                            positions.Add(hit.point + Vector3.up * p.HeightOffset);
                    }
                    else
                    {
                        for (var y = min.y; y <= max.y; y += p.Spacing[1])
                            positions.Add(new Vector3(x, y, z));
                    }
                }
            }

            if (positions.Count == 0)
                return ToolResult<LightingLightProbesResult>.Fail(
                    "No probe positions generated — bounds/spacing too small, or RaycastAboveGround " +
                    "found no ground hits", ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(group, "Mosaic: Fill Light Probe Grid");
            group.probePositions = positions.ToArray();
            EditorUtility.SetDirty(group);

            return ToolResult<LightingLightProbesResult>.Ok(new LightingLightProbesResult
            {
                Action = "grid", InstanceId = UnityIds.Of(group.gameObject), Name = group.gameObject.name,
                ProbeCount = group.probePositions.Length,
            });
        }

        private static ToolResult<LightingLightProbesResult> AddPositions(LightingLightProbesParams p)
        {
            if (!TryResolveGroup(p.Name, out var group, out var groupError))
                return ToolResult<LightingLightProbesResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            if (p.Positions == null || p.Positions.Length == 0 || p.Positions.Length % 3 != 0)
                return ToolResult<LightingLightProbesResult>.Fail(
                    "Positions must be a non-empty flat array of [x,y,z, x,y,z, ...]", ErrorCodes.INVALID_PARAM);

            var existing = group.probePositions ?? new Vector3[0];
            var added = new Vector3[p.Positions.Length / 3];
            for (var i = 0; i < added.Length; i++)
                added[i] = new Vector3(p.Positions[i * 3], p.Positions[i * 3 + 1], p.Positions[i * 3 + 2]);

            var combined = new Vector3[existing.Length + added.Length];
            existing.CopyTo(combined, 0);
            added.CopyTo(combined, existing.Length);

            Undo.RecordObject(group, "Mosaic: Add Light Probe Positions");
            group.probePositions = combined;
            EditorUtility.SetDirty(group);

            return ToolResult<LightingLightProbesResult>.Ok(new LightingLightProbesResult
            {
                Action = "add-positions", InstanceId = UnityIds.Of(group.gameObject), Name = group.gameObject.name,
                ProbeCount = group.probePositions.Length,
            });
        }

        private static ToolResult<LightingLightProbesResult> Clear(LightingLightProbesParams p)
        {
            if (!TryResolveGroup(p.Name, out var group, out var groupError))
                return ToolResult<LightingLightProbesResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(group, "Mosaic: Clear Light Probe Group");
            group.probePositions = new Vector3[0];
            EditorUtility.SetDirty(group);

            return ToolResult<LightingLightProbesResult>.Ok(new LightingLightProbesResult
            {
                Action = "clear", InstanceId = UnityIds.Of(group.gameObject), Name = group.gameObject.name, ProbeCount = 0,
            });
        }

        private static bool TryResolveGroup(string name, out LightProbeGroup group, out string error)
        {
            group = null;
            error = null;
            var go = GameObject.Find(name);
            if (go == null)
            {
                error = $"GameObject '{name}' not found";
                return false;
            }
            group = go.GetComponent<LightProbeGroup>();
            if (group == null)
            {
                error = $"GameObject '{name}' does not have a LightProbeGroup component";
                return false;
            }
            return true;
        }
    }
}
