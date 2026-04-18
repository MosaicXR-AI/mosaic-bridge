using System.Diagnostics;

namespace Mosaic.Bridge.Core.Mcp
{
    public interface IProcessLauncher
    {
        IProcessHandle Start(ProcessStartInfo psi);
        bool IsProcessAlive(int pid);
    }
}
