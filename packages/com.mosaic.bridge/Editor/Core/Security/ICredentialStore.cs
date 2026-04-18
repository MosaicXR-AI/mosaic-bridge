namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// Cross-platform abstraction for OS-native credential storage.
    /// Implementations use the platform keychain (macOS), DPAPI (Windows),
    /// or AES-encrypted EditorPrefs (Linux/fallback).
    /// </summary>
    public interface ICredentialStore
    {
        /// <summary>Stores a credential value under the given key. Returns true on success.</summary>
        bool Store(string key, string value);

        /// <summary>Retrieves a credential value by key, or null if not found.</summary>
        string Retrieve(string key);

        /// <summary>Deletes a credential by key. Returns true if the entry was removed.</summary>
        bool Delete(string key);
    }
}
