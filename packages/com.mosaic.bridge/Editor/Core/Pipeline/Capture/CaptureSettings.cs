namespace Mosaic.Bridge.Core.Pipeline.Capture
{
    /// <summary>
    /// Configuration for a scene-capture pass: resolution and which camera
    /// angles to render.
    /// </summary>
    public sealed class CaptureSettings
    {
        public int Resolution { get; set; } = 512;
        public string[] Angles { get; set; } = { "front", "right", "top", "perspective" };
    }
}
