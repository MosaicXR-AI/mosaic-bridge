using System;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Licensing;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Dockable Editor window displaying license tier, trial status, quota usage,
    /// and activation controls. Accessible via Window > Mosaic > License Status.
    /// </summary>
    public class LicenseStatusPanel : EditorWindow
    {
        private const string PurchaseUrl = "https://mosaicxr.com/pricing";

        [MenuItem("Window/Mosaic/License Status", priority = 5)]
        public static void ShowWindow()
        {
            GetWindow<LicenseStatusPanel>("License Status");
        }

        private Vector2 _scrollPos;
        private int _frameCounter;

        private EditorPrefsLicenseStatusProvider _statusProvider;
        private LicenseActivator _activator;

        private bool _showActivationField;
        private string _licenseKeyInput = "";
        private string _activationMessage = "";
        private MessageType _activationMessageType = MessageType.None;

        private void OnEnable()
        {
            _statusProvider = new EditorPrefsLicenseStatusProvider();
            _activator = new LicenseActivator();
            _activator.LicenseChanged += OnLicenseChanged;
            EditorApplication.update += ThrottledRepaint;
        }

        private void OnDisable()
        {
            if (_activator != null)
                _activator.LicenseChanged -= OnLicenseChanged;
            EditorApplication.update -= ThrottledRepaint;
        }

        private void ThrottledRepaint()
        {
            if (++_frameCounter >= 30)
            {
                _frameCounter = 0;
                Repaint();
            }
        }

        private void OnLicenseChanged(LicenseTier newTier)
        {
            Repaint();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawLicenseStatus();
            EditorGUILayout.Space(10);

            DrawQuotaUsage();
            EditorGUILayout.Space(10);

            DrawLicenseActions();

            EditorGUILayout.EndScrollView();
        }

        // --------------------------------------------------------------------
        // Section 1: Current License Status
        // --------------------------------------------------------------------

        private void DrawLicenseStatus()
        {
            EditorGUILayout.LabelField("Current License Status", EditorStyles.boldLabel);

            var tier = _statusProvider.CurrentTier;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Tier badge
                using (new EditorGUILayout.HorizontalScope())
                {
                    var oldColor = GUI.color;
                    GUI.color = GetTierColor(tier);
                    GUILayout.Label("\u25cf", GUILayout.Width(16));
                    GUI.color = oldColor;

                    EditorGUILayout.LabelField(tier.ToString(), EditorStyles.boldLabel);
                }

                switch (tier)
                {
                    case LicenseTier.Trial:
                        DrawTrialDetails();
                        break;

                    case LicenseTier.Expired:
                        EditorGUILayout.HelpBox(
                            "Your trial has expired. Activate a license key or purchase a plan to continue using Mosaic Bridge.",
                            MessageType.Error);
                        break;

                    default:
                        DrawLicensedDetails(tier);
                        break;
                }
            }
        }

        private void DrawTrialDetails()
        {
            var daysRemaining = _statusProvider.TrialDaysRemaining;
            EditorGUILayout.LabelField($"Trial: {daysRemaining} day{(daysRemaining == 1 ? "" : "s")} remaining");

            // Inline quota summary
            var used = _statusProvider.DailyQuotaUsed;
            var limit = _statusProvider.DailyQuota;
            EditorGUILayout.LabelField($"{used}/{limit} calls today");
        }

        private void DrawLicensedDetails(LicenseTier tier)
        {
            EditorGUILayout.LabelField("Tier", tier.ToString());

            var masked = _activator.MaskedLicenseKey;
            if (!string.IsNullOrEmpty(masked))
                EditorGUILayout.LabelField("License Key", masked);
        }

        // --------------------------------------------------------------------
        // Section 2: Quota & Usage
        // --------------------------------------------------------------------

        private void DrawQuotaUsage()
        {
            EditorGUILayout.LabelField("Quota & Usage", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var tier = _statusProvider.CurrentTier;
                var used = _statusProvider.DailyQuotaUsed;
                var limit = _statusProvider.DailyQuota;

                // Paid tiers have unlimited quota
                if (tier != LicenseTier.Trial && tier != LicenseTier.Expired)
                {
                    EditorGUILayout.LabelField("Daily API Calls", $"{used} (unlimited)");
                }
                else
                {
                    EditorGUILayout.LabelField("Daily API Calls", $"{used} / {limit}");

                    // Progress bar
                    var ratio = limit > 0 ? (float)used / limit : 0f;
                    var barRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                    DrawQuotaBar(barRect, ratio);

                    // Block warning
                    var blockReason = _statusProvider.GetBlockReason();
                    if (blockReason.HasValue)
                    {
                        var msg = blockReason.Value switch
                        {
                            BlockReason.TrialExpired => "Trial expired. Activate a license to continue.",
                            BlockReason.QuotaExhausted => "Daily quota exhausted. Resets at midnight.",
                            BlockReason.GraceExpired => "Offline grace period expired. Connect to validate your license.",
                            _ => "Access blocked."
                        };
                        EditorGUILayout.HelpBox(msg, MessageType.Warning);
                    }
                }

                // Reset time
                EditorGUILayout.LabelField("Quota Resets", "Midnight (local time)");
            }
        }

        private static void DrawQuotaBar(Rect rect, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);

            // Background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            // Fill
            Color fillColor;
            if (ratio < 0.7f)
                fillColor = new Color(0.2f, 0.7f, 0.2f);      // green
            else if (ratio < 0.9f)
                fillColor = new Color(0.85f, 0.7f, 0.1f);     // yellow
            else
                fillColor = new Color(0.8f, 0.2f, 0.2f);      // red

            var fillRect = new Rect(rect.x, rect.y, rect.width * ratio, rect.height);
            EditorGUI.DrawRect(fillRect, fillColor);

            // Percentage label centered
            var label = $"{Mathf.RoundToInt(ratio * 100)}%";
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, label, style);
        }

        // --------------------------------------------------------------------
        // Section 3: License Actions
        // --------------------------------------------------------------------

        private void DrawLicenseActions()
        {
            EditorGUILayout.LabelField("License Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_activator.HasLicenseKey)
                {
                    // Deactivate button
                    if (GUILayout.Button("Deactivate License"))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Deactivate License",
                                "Are you sure you want to deactivate your license? You will revert to trial mode.",
                                "Deactivate", "Cancel"))
                        {
                            _activator.Deactivate();
                            _activationMessage = "License deactivated. Reverted to trial.";
                            _activationMessageType = MessageType.Info;
                            _showActivationField = false;
                            _licenseKeyInput = "";
                        }
                    }
                }

                // Activate section
                if (_showActivationField)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Enter License Key:");
                    _licenseKeyInput = EditorGUILayout.TextField(_licenseKeyInput);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Activate"))
                        {
                            var result = _activator.Activate(_licenseKeyInput);
                            if (result.IsSuccess)
                            {
                                _activationMessage = $"License activated! Tier: {result.Tier}";
                                _activationMessageType = MessageType.Info;
                                _showActivationField = false;
                                _licenseKeyInput = "";
                            }
                            else
                            {
                                _activationMessage = result.ErrorMessage;
                                _activationMessageType = MessageType.Error;
                            }
                        }

                        if (GUILayout.Button("Cancel"))
                        {
                            _showActivationField = false;
                            _licenseKeyInput = "";
                            _activationMessage = "";
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("Activate License"))
                    {
                        _showActivationField = true;
                        _activationMessage = "";
                    }
                }

                // Feedback message
                if (!string.IsNullOrEmpty(_activationMessage))
                {
                    EditorGUILayout.HelpBox(_activationMessage, _activationMessageType);
                }

                EditorGUILayout.Space(4);

                // Purchase link
                if (GUILayout.Button("Purchase License", EditorStyles.linkLabel))
                {
                    Application.OpenURL(PurchaseUrl);
                }
            }
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private static Color GetTierColor(LicenseTier tier)
        {
            return tier switch
            {
                LicenseTier.Trial   => new Color(0.3f, 0.7f, 1f),     // blue
                LicenseTier.Indie   => new Color(0.2f, 0.8f, 0.4f),   // green
                LicenseTier.Pro     => new Color(0.9f, 0.7f, 0.1f),   // gold
                LicenseTier.Team    => new Color(0.6f, 0.4f, 0.9f),   // purple
                LicenseTier.Pilot   => new Color(0.1f, 0.9f, 0.9f),   // cyan
                LicenseTier.Expired => new Color(0.9f, 0.2f, 0.2f),   // red
                _                   => Color.gray
            };
        }
    }
}
