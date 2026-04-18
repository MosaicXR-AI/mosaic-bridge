using UnityEditor;

namespace Mosaic.Bridge.Core.Bootstrap
{
    [InitializeOnLoad]
    static class DomainReloadHandler
    {
        static DomainReloadHandler()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        static void OnBeforeAssemblyReload()
        {
            BridgeBootstrap.Logger?.Info("Domain reload detected — shutting down bridge");
            BridgeBootstrap.ShutdownForReload();
        }
    }
}
