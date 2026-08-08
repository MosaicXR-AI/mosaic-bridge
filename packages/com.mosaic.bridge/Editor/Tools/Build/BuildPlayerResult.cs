namespace Mosaic.Bridge.Tools.Build
{
    public sealed class BuildPlayerResult
    {
        public bool BuildSucceeded { get; set; }
        public string OutputPath { get; set; }
        public string TargetPlatform { get; set; }
        public double DurationSeconds { get; set; }
        public string[] Errors { get; set; }
        public string[] Warnings { get; set; }
    }
}
