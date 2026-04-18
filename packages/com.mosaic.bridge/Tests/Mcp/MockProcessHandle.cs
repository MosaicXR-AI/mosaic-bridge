using System;
using Mosaic.Bridge.Core.Mcp;

namespace Mosaic.Bridge.Tests.Mcp
{
    public class MockProcessHandle : IProcessHandle
    {
        private EventHandler _exited;

        public int Id { get; set; } = 1234;
        public bool HasExited { get; set; }

        public event EventHandler Exited
        {
            add => _exited += value;
            remove => _exited -= value;
        }

        public bool EnableRaisingEvents { set { EnableRaisingEventsSet = value; } }
        public bool EnableRaisingEventsSet { get; private set; }

        public bool CloseMainWindowCalled { get; private set; }
        public bool KillCalled { get; private set; }
        public bool WaitForExitCalled { get; private set; }
        public int WaitForExitTimeout { get; private set; }
        public bool WaitForExitResult { get; set; } = true;
        public bool DisposeCalled { get; private set; }

        public bool CloseMainWindow()
        {
            CloseMainWindowCalled = true;
            return true;
        }

        public void Kill()
        {
            KillCalled = true;
        }

        public bool WaitForExit(int milliseconds)
        {
            WaitForExitCalled = true;
            WaitForExitTimeout = milliseconds;
            return WaitForExitResult;
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }

        /// <summary>
        /// Simulates the process exiting (raises the Exited event).
        /// </summary>
        public void SimulateExit()
        {
            HasExited = true;
            _exited?.Invoke(this, EventArgs.Empty);
        }
    }
}
