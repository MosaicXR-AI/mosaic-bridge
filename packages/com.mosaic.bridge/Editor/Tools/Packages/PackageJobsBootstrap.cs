using UnityEditor;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Packages
{
    /// <summary>Registers the package job kinds with JobRegistry on every load — JobRegistry's
    /// own Kinds dictionary is static and does not survive a domain reload any more than any
    /// other in-memory state does, so this has to re-run every time, same as every other
    /// self-registration in this codebase.</summary>
    [InitializeOnLoad]
    internal static class PackageJobsBootstrap
    {
        static PackageJobsBootstrap()
        {
            JobRegistry.RegisterKind("package/add", new PackageAddJobKind());
            JobRegistry.RegisterKind("package/remove", new PackageRemoveJobKind());
        }
    }
}
