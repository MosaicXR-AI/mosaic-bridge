using System;
using UnityEditor;

namespace Mosaic.Bridge.Core.Discovery
{
    /// <summary>
    /// Who is signed in to this Editor, cached on the main thread for the health endpoint.
    /// </summary>
    /// <remarks>
    /// An access code cannot be bound to a person by itself. The Editor knows who is signed in
    /// to Unity, and reporting that lets the service refuse an Editor whose account is not the
    /// one the code was issued to. It is the Editor's own word, read from
    /// <see cref="CloudProjectSettings"/> — no network, no prompt — and it is deliberately the
    /// cached identity, so an offline Editor still reports the person who last signed in.
    ///
    /// Cached because the health handler answers on the listener thread, where Editor APIs
    /// throw. Refreshed from <see cref="EditorApplication.update"/> a few times a minute, which
    /// is fast enough to notice a sign-out and cheap enough to never be noticed.
    /// </remarks>
    [InitializeOnLoad]
    public static class EditorIdentity
    {
        private const double RefreshSeconds = 15;
        private static double _lastRefresh = -RefreshSeconds;

        public static string UserName { get; private set; } = "";
        public static string UserId { get; private set; } = "";
        public static string DisplayName { get; private set; } = "";
        public static string OrganizationId { get; private set; } = "";
        /// <summary>UTC time the values above were last read, ISO 8601; empty until the first read.</summary>
        public static string RefreshedAt { get; private set; } = "";

        static EditorIdentity()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _lastRefresh < RefreshSeconds) return;
            _lastRefresh = EditorApplication.timeSinceStartup;
            try
            {
                UserName = CloudProjectSettings.userName ?? "";
                UserId = CloudProjectSettings.userId ?? "";
                DisplayName = CloudProjectSettings.userDisplayName ?? "";
                OrganizationId = CloudProjectSettings.organizationId ?? "";
                RefreshedAt = DateTime.UtcNow.ToString("o");
            }
            catch (Exception)
            {
                // Not signed in, or the API is unavailable on this Editor: report nothing, and
                // let the service decide what an absent identity means.
            }
        }
    }
}
