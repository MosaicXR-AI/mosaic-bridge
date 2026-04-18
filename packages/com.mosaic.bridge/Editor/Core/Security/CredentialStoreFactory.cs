namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// Creates the appropriate <see cref="ICredentialStore"/> for the current platform.
    /// macOS uses Keychain, Windows uses DPAPI, others use AES-encrypted EditorPrefs.
    /// </summary>
    public static class CredentialStoreFactory
    {
        private static ICredentialStore _instance;

        /// <summary>
        /// Returns a shared credential store instance for the current platform.
        /// </summary>
        public static ICredentialStore Create()
        {
            if (_instance != null) return _instance;

#if UNITY_EDITOR_OSX
            _instance = new KeychainCredentialStore();
#elif UNITY_EDITOR_WIN
            _instance = new DpapiCredentialStore();
#else
            _instance = new EditorPrefsCredentialStore();
#endif
            return _instance;
        }

        /// <summary>
        /// Replaces the shared instance (useful for testing).
        /// </summary>
        internal static void SetOverride(ICredentialStore store)
        {
            _instance = store;
        }
    }
}
