using System;
using Mosaic.Bridge.Core.Security;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// Client-side license key activation for Mosaic Bridge.
    /// MVP validates key format and stores locally; full server validation is deferred to Phase 2.
    /// License keys are stored in OS-native credential storage (Keychain / DPAPI / AES-encrypted).
    /// </summary>
    public sealed class LicenseActivator
    {
        private const string CredentialKey = "LicenseKey";

        /// <summary>Legacy EditorPrefs key — used only for one-time migration.</summary>
        private const string LegacyKeyLicenseKey = "MosaicBridge.LicenseKey";
        private const string KeyLicenseTier = "MosaicBridge.LicenseTier";
        private const string KeyActivationToken = "MosaicBridge.ActivationToken";
        private const string KeyLastValidated = "MosaicBridge.LastValidatedAt";
        private const string KeyMigrated = "MosaicBridge.CredentialMigrated";

        private readonly ICredentialStore _credentialStore;

        public LicenseActivator() : this(CredentialStoreFactory.Create()) { }

        internal LicenseActivator(ICredentialStore credentialStore)
        {
            _credentialStore = credentialStore;
            MigrateFromEditorPrefsIfNeeded();
        }

        /// <summary>Raised when the active license tier changes (activation or deactivation).</summary>
        public event Action<LicenseTier> LicenseChanged;

        /// <summary>Current stored license key masked for display (first 4 + **** + last 4).</summary>
        public string MaskedLicenseKey
        {
            get
            {
                var key = _credentialStore.Retrieve(CredentialKey);
                if (string.IsNullOrEmpty(key) || key.Length < 8) return "";
                return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
            }
        }

        /// <summary>True if a license key is currently stored.</summary>
        public bool HasLicenseKey => !string.IsNullOrEmpty(_credentialStore.Retrieve(CredentialKey));

        /// <summary>
        /// Activates a license key. For MVP, validates format and stores locally.
        /// Returns an <see cref="ActivationResult"/> with success/failure.
        /// </summary>
        public ActivationResult Activate(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return ActivationResult.Fail("License key cannot be empty.");

            // MVP format validation: MOSAIC-XXXX-XXXX-XXXX (16+ chars)
            var cleaned = licenseKey.Trim().ToUpperInvariant();
            if (cleaned.Length < 16)
                return ActivationResult.Fail("Invalid license key format. Expected: MOSAIC-XXXX-XXXX-XXXX");

            // Store the key in OS-native credential storage
            if (!_credentialStore.Store(CredentialKey, cleaned))
            {
                Debug.LogWarning("[Mosaic] Failed to store license key in credential store.");
                return ActivationResult.Fail("Failed to store license key securely.");
            }

            EditorPrefs.SetString(KeyLastValidated, DateTime.UtcNow.ToString("O"));

            // Determine tier from key prefix (MVP heuristic)
            var tier = DetermineTier(cleaned);
            EditorPrefs.SetString(KeyLicenseTier, tier.ToString().ToLowerInvariant());

            LicenseChanged?.Invoke(tier);

            return ActivationResult.Success(tier);
        }

        /// <summary>Deactivates the current license, reverting to trial.</summary>
        public void Deactivate()
        {
            _credentialStore.Delete(CredentialKey);
            EditorPrefs.DeleteKey(KeyActivationToken);
            EditorPrefs.DeleteKey(KeyLastValidated);
            EditorPrefs.SetString(KeyLicenseTier, "trial");
            LicenseChanged?.Invoke(LicenseTier.Trial);
        }

        /// <summary>
        /// One-time migration: if a license key still exists in plaintext EditorPrefs,
        /// move it to the credential store and clear the legacy entry.
        /// </summary>
        private void MigrateFromEditorPrefsIfNeeded()
        {
            if (EditorPrefs.GetBool(KeyMigrated, false))
                return;

            var legacyKey = EditorPrefs.GetString(LegacyKeyLicenseKey, "");
            if (!string.IsNullOrEmpty(legacyKey))
            {
                if (_credentialStore.Store(CredentialKey, legacyKey))
                {
                    EditorPrefs.DeleteKey(LegacyKeyLicenseKey);
                    Debug.Log("[Mosaic] License key migrated from EditorPrefs to secure credential store.");
                }
                else
                {
                    Debug.LogWarning("[Mosaic] Failed to migrate license key to credential store. Will retry next launch.");
                    return; // Don't mark as migrated so we retry
                }
            }

            EditorPrefs.SetBool(KeyMigrated, true);
        }

        private static LicenseTier DetermineTier(string key)
        {
            if (key.StartsWith("MOSAIC-PILOT")) return LicenseTier.Pilot;
            if (key.StartsWith("MOSAIC-TEAM")) return LicenseTier.Team;
            if (key.StartsWith("MOSAIC-PRO")) return LicenseTier.Pro;
            return LicenseTier.Indie; // default paid tier
        }
    }

    /// <summary>
    /// Result of a license activation attempt.
    /// </summary>
    public sealed class ActivationResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public LicenseTier Tier { get; private set; }

        public static ActivationResult Success(LicenseTier tier) =>
            new ActivationResult { IsSuccess = true, Tier = tier };

        public static ActivationResult Fail(string message) =>
            new ActivationResult { IsSuccess = false, ErrorMessage = message };
    }
}
