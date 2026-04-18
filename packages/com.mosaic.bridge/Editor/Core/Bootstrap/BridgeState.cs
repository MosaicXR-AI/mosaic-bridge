namespace Mosaic.Bridge.Core.Bootstrap
{
    public enum BridgeState
    {
        Uninitialized,
        Starting,
        Running,
        Reloading,   // beforeAssemblyReload fired, teardown in progress
        Stopped,     // EditorApplication.quitting fired
        Error        // Critical startup failure (Story 1.12) — see status.json for details
    }
}
