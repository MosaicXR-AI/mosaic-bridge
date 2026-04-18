using UnityEngine;

namespace Mosaic.Bridge.Core.Pipeline.Capture
{
    /// <summary>
    /// Maps named camera angles to direction/up vector pairs used to
    /// position a capture camera relative to a target's bounding centre.
    /// </summary>
    public static class CameraAngle
    {
        /// <summary>
        /// Returns (direction from target, up vector) for the given named angle.
        /// Unknown names fall back to "front".
        /// </summary>
        public static (Vector3 direction, Vector3 up) GetAngle(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "front":       return (Vector3.forward, Vector3.up);
                case "back":        return (Vector3.back, Vector3.up);
                case "right":       return (Vector3.right, Vector3.up);
                case "left":        return (Vector3.left, Vector3.up);
                case "top":         return (Vector3.up, Vector3.forward);
                case "perspective": return (new Vector3(1, 1, -1).normalized, Vector3.up);
                default:            return (Vector3.forward, Vector3.up);
            }
        }
    }
}
