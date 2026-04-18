namespace Mosaic.Bridge.Tools.Cameras
{
    public sealed class CameraScreenshotGameParams
    {
        public int? CameraInstanceId { get; set; }  // null defaults to Camera.main
        public int? Width { get; set; }              // null defaults to 1920
        public int? Height { get; set; }             // null defaults to 1080
        /// <summary>"png" (default, lossless) or "jpeg" (lossy, much smaller).</summary>
        public string Format { get; set; }
        /// <summary>JPEG quality 1-100 (default 75). Ignored for PNG.</summary>
        public int? Quality { get; set; }
        /// <summary>File path to save the screenshot. If null, saves to a temp directory.</summary>
        public string SavePath { get; set; }
        /// <summary>If true, also include base64 data in the response. Default false — file path only.</summary>
        public bool IncludeBase64 { get; set; }
    }
}
