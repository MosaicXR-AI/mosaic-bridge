using System.Collections.Generic;
using System.Diagnostics;
using Mosaic.Bridge.Core.Mcp;

namespace Mosaic.Bridge.Tests.Mcp
{
    public class MockProcessLauncher : IProcessLauncher
    {
        public List<ProcessStartInfo> StartCalls { get; } = new List<ProcessStartInfo>();
        public MockProcessHandle NextHandle { get; set; }
        public bool IsProcessAliveResult { get; set; }

        private readonly Queue<MockProcessHandle> _handleQueue = new Queue<MockProcessHandle>();

        public void EnqueueHandle(MockProcessHandle handle)
        {
            _handleQueue.Enqueue(handle);
        }

        public IProcessHandle Start(ProcessStartInfo psi)
        {
            StartCalls.Add(psi);

            if (_handleQueue.Count > 0)
                return _handleQueue.Dequeue();

            return NextHandle;
        }

        public bool IsProcessAlive(int pid)
        {
            return IsProcessAliveResult;
        }
    }
}
