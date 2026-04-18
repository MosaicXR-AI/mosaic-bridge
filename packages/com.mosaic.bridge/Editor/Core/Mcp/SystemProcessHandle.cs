using System;
using System.Diagnostics;

namespace Mosaic.Bridge.Core.Mcp
{
    public class SystemProcessHandle : IProcessHandle
    {
        private readonly Process _process;

        public SystemProcessHandle(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public int Id => _process.Id;

        public bool HasExited => _process.HasExited;

        public event EventHandler Exited
        {
            add => _process.Exited += value;
            remove => _process.Exited -= value;
        }

        public bool EnableRaisingEvents
        {
            set => _process.EnableRaisingEvents = value;
        }

        public bool CloseMainWindow()
        {
            return _process.CloseMainWindow();
        }

        public void Kill()
        {
            _process.Kill();
        }

        public bool WaitForExit(int milliseconds)
        {
            return _process.WaitForExit(milliseconds);
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}
