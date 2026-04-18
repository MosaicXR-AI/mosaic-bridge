namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneGetInfoResult
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDirty { get; set; }
        public int RootObjectCount { get; set; }
        public bool IsLoaded { get; set; }
    }
}
