using System;

namespace Mosaic.Bridge.Core.Mcp
{
    public interface IProcessHandle : IDisposable
    {
        int Id { get; }
        bool HasExited { get; }
        event EventHandler Exited;
        bool EnableRaisingEvents { set; }
        bool CloseMainWindow();
        void Kill();
        bool WaitForExit(int milliseconds);
    }
}
