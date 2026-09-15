#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineTargetGroupMemberInfo
    {
        public string Name { get; set; }
        public float Weight { get; set; }
        public float Radius { get; set; }
    }

    public sealed class CinemachineTargetGroupResult
    {
        public string Action { get; set; }
        public string Name { get; set; }
        public string PositionMode { get; set; }
        public string RotationMode { get; set; }
        public int MemberCount { get; set; }
        public CinemachineTargetGroupMemberInfo[] Members { get; set; }
        public Vector3 BoundingBoxCenter { get; set; }
        public Vector3 BoundingBoxSize { get; set; }
    }
}
#endif
