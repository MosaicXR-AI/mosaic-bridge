using System;
using System.Diagnostics;

namespace Mosaic.Bridge.Core.Mcp
{
    public class SystemProcessLauncher : IProcessLauncher
    {
        public IProcessHandle Start(ProcessStartInfo psi)
        {
            var process = Process.Start(psi);
            return new SystemProcessHandle(process);
        }

        public bool IsProcessAlive(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
