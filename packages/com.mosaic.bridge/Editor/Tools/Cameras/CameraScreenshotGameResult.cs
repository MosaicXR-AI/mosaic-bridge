namespace Mosaic.Bridge.Tools.Cameras
{
    public sealed class CameraScreenshotGameResult
    {
        /// <summary>Absolute path to the saved screenshot file.</summary>
        public string FilePath { get; set; }
        /// <summary>Base64-encoded image data. Only populated when IncludeBase64=true.</summary>
        public string Base64Png { get; set; }
        public string Format { get; set; }
        public int ByteSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int CameraInstanceId { get; set; }
        public string CameraName { get; set; }
    }
}
