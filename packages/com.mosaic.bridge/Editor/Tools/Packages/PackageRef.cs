namespace Mosaic.Bridge.Tools.Packages
{
    /// <summary>
    /// Shared DTO representing a Unity package reference.
    /// Used by list, add, remove, and search results.
    /// </summary>
    public sealed class PackageRef
    {
        public string Name        { get; set; }
        public string Version     { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Source      { get; set; }
    }
}
