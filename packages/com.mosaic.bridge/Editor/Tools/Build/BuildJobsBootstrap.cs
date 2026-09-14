using UnityEditor;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Build
{
    /// <summary>Registers build/start with JobRegistry on every load — JobRegistry's own Kinds
    /// dictionary is static and does not survive a domain reload any more than any other
    /// in-memory state does, same as PackageJobsBootstrap.</summary>
    [InitializeOnLoad]
    internal static class BuildJobsBootstrap
    {
        internal const string Kind = "build/start";

        static BuildJobsBootstrap()
        {
            JobRegistry.RegisterKind(Kind, new BuildJobKind());
        }
    }
}
