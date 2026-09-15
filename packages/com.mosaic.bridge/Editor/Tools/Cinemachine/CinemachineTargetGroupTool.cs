#if MOSAIC_HAS_CINEMACHINE
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineTargetGroupTool
    {
        [MosaicTool("cinemachine/target-group",
                    "create: makes a new CinemachineTargetGroup (PositionMode: GroupAverage|GroupCenter; " +
                    "RotationMode: GroupAverage|Manual). add-member/remove-member: MemberName " +
                    "(+ Weight/Radius for add). info: reports members, weights, radii, and the group's " +
                    "current bounding box. A camera that follows the group (e.g. GroupFraming aim, or " +
                    "OrbitalFollow's target) frames every member automatically.",
                    isReadOnly: false)]
        public static ToolResult<CinemachineTargetGroupResult> Execute(CinemachineTargetGroupParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":         return Create(p);
                case "add-member":     return AddMember(p);
                case "remove-member":  return RemoveMember(p);
                case "info":           return Info(p);
                default:
                    return ToolResult<CinemachineTargetGroupResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: create, add-member, remove-member, info",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<CinemachineTargetGroupResult> Create(CinemachineTargetGroupParams p)
        {
            var go = new GameObject(p.Name);
            var group = go.AddComponent<CinemachineTargetGroup>();

            if (!string.IsNullOrEmpty(p.PositionMode))
            {
                if (!TryParsePositionMode(p.PositionMode, out var positionMode))
                {
                    Object.DestroyImmediate(go);
                    return ToolResult<CinemachineTargetGroupResult>.Fail(
                        $"Unknown PositionMode '{p.PositionMode}'. Valid: GroupAverage, GroupCenter", ErrorCodes.INVALID_PARAM);
                }
                group.PositionMode = positionMode;
            }
            if (!string.IsNullOrEmpty(p.RotationMode))
            {
                if (!TryParseRotationMode(p.RotationMode, out var rotationMode))
                {
                    Object.DestroyImmediate(go);
                    return ToolResult<CinemachineTargetGroupResult>.Fail(
                        $"Unknown RotationMode '{p.RotationMode}'. Valid: GroupAverage, Manual", ErrorCodes.INVALID_PARAM);
                }
                group.RotationMode = rotationMode;
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Cinemachine Target Group");

            return ToolResult<CinemachineTargetGroupResult>.Ok(new CinemachineTargetGroupResult
            {
                Action = "create", Name = go.name,
                PositionMode = group.PositionMode.ToString(), RotationMode = group.RotationMode.ToString(),
                MemberCount = 0, Members = new CinemachineTargetGroupMemberInfo[0],
            });
        }

        private static ToolResult<CinemachineTargetGroupResult> AddMember(CinemachineTargetGroupParams p)
        {
            if (!TryResolveGroup(p.Name, out var group, out var groupError))
                return ToolResult<CinemachineTargetGroupResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            if (string.IsNullOrEmpty(p.MemberName))
                return ToolResult<CinemachineTargetGroupResult>.Fail("MemberName is required", ErrorCodes.INVALID_PARAM);

            var memberGo = GameObject.Find(p.MemberName);
            if (memberGo == null)
                return ToolResult<CinemachineTargetGroupResult>.Fail(
                    $"GameObject '{p.MemberName}' not found", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(group, "Mosaic: Add Target Group Member");
            group.AddMember(memberGo.transform, p.Weight, p.Radius);
            EditorUtility.SetDirty(group);

            return ToResultOk("add-member", group);
        }

        private static ToolResult<CinemachineTargetGroupResult> RemoveMember(CinemachineTargetGroupParams p)
        {
            if (!TryResolveGroup(p.Name, out var group, out var groupError))
                return ToolResult<CinemachineTargetGroupResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            if (string.IsNullOrEmpty(p.MemberName))
                return ToolResult<CinemachineTargetGroupResult>.Fail("MemberName is required", ErrorCodes.INVALID_PARAM);

            var memberGo = GameObject.Find(p.MemberName);
            if (memberGo == null || group.FindMember(memberGo.transform) < 0)
                return ToolResult<CinemachineTargetGroupResult>.Fail(
                    $"'{p.MemberName}' is not a member of target group '{p.Name}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(group, "Mosaic: Remove Target Group Member");
            group.RemoveMember(memberGo.transform);
            EditorUtility.SetDirty(group);

            return ToResultOk("remove-member", group);
        }

        private static ToolResult<CinemachineTargetGroupResult> Info(CinemachineTargetGroupParams p)
        {
            if (!TryResolveGroup(p.Name, out var group, out var groupError))
                return ToolResult<CinemachineTargetGroupResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            return ToResultOk("info", group);
        }

        private static ToolResult<CinemachineTargetGroupResult> ToResultOk(string action, CinemachineTargetGroup group)
        {
            var members = group.Targets
                .Where(t => t.Object != null)
                .Select(t => new CinemachineTargetGroupMemberInfo { Name = t.Object.name, Weight = t.Weight, Radius = t.Radius })
                .ToArray();

            return ToolResult<CinemachineTargetGroupResult>.Ok(new CinemachineTargetGroupResult
            {
                Action = action, Name = group.name,
                PositionMode = group.PositionMode.ToString(), RotationMode = group.RotationMode.ToString(),
                MemberCount = members.Length, Members = members,
                BoundingBoxCenter = group.BoundingBox.center, BoundingBoxSize = group.BoundingBox.size,
            });
        }

        private static bool TryResolveGroup(string name, out CinemachineTargetGroup group, out string error)
        {
            group = null;
            error = null;
            var go = GameObject.Find(name);
            if (go == null)
            {
                error = $"GameObject '{name}' not found";
                return false;
            }
            group = go.GetComponent<CinemachineTargetGroup>();
            if (group == null)
            {
                error = $"GameObject '{name}' does not have a CinemachineTargetGroup component";
                return false;
            }
            return true;
        }

        private static bool TryParsePositionMode(string value, out CinemachineTargetGroup.PositionModes result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "groupaverage": result = CinemachineTargetGroup.PositionModes.GroupAverage; return true;
                case "groupcenter":  result = CinemachineTargetGroup.PositionModes.GroupCenter;  return true;
                default:             result = CinemachineTargetGroup.PositionModes.GroupAverage; return false;
            }
        }

        private static bool TryParseRotationMode(string value, out CinemachineTargetGroup.RotationModes result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "groupaverage": result = CinemachineTargetGroup.RotationModes.GroupAverage; return true;
                case "manual":       result = CinemachineTargetGroup.RotationModes.Manual;       return true;
                default:             result = CinemachineTargetGroup.RotationModes.GroupAverage; return false;
            }
        }
    }
}
#endif
