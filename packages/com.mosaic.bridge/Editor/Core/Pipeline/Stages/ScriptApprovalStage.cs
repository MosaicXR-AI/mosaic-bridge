using System;
using System.Collections.Generic;
using System.Linq;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Pipeline.Stages
{
    /// <summary>
    /// Read-only snapshot of a pending approval exposed to UI consumers.
    /// </summary>
    public sealed class PendingApprovalInfo
    {
        public string Token { get; }
        public string ToolName { get; }
        public string Path { get; }
        public string Content { get; }
        public DateTime CreatedAt { get; }
        public DateTime ExpiresAt { get; }

        internal PendingApprovalInfo(string token, string toolName, string path,
            string content, DateTime createdAt)
        {
            Token = token;
            ToolName = toolName;
            Path = path;
            Content = content;
            CreatedAt = createdAt;
            ExpiresAt = createdAt.AddMinutes(5);
        }
    }

    /// <summary>
    /// Pre-execution stage that gates script write operations behind human approval.
    /// When enabled, script/create and script/update return a preview token instead
    /// of executing. The user must approve via the approval tool to proceed.
    /// Controlled by MosaicBridge.ScriptApprovalEnabled EditorPref (default: false).
    /// </summary>
    public sealed class ScriptApprovalStage : IPipelineStage
    {
        private readonly IMosaicLogger _logger;

        // Pending approvals keyed by token
        private static readonly Dictionary<string, PendingApproval> _pending =
            new Dictionary<string, PendingApproval>();

        public ScriptApprovalStage(IMosaicLogger logger)
        {
            _logger = logger;
        }

        public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
        {
            // Check if approval is enabled
            if (!EditorPrefs.GetBool("MosaicBridge.ScriptApprovalEnabled", false))
                return true;

            // Only gate script write tools
            if (!IsScriptWriteTool(context))
                return true;

            // Check if this is an approved request (has approval token)
            var approvalToken = context.Parameters?["_approvalToken"]?.Value<string>();
            if (!string.IsNullOrEmpty(approvalToken))
            {
                if (_pending.TryGetValue(approvalToken, out var approved))
                {
                    _pending.Remove(approvalToken);
                    _logger.Info("Script approval consumed",
                        ("token", (object)approvalToken));
                    return true; // approved -- let it execute
                }

                // Invalid or expired token
                toolResult = new HandlerResponse
                {
                    StatusCode = 403,
                    ContentType = "application/json",
                    Body = JsonConvert.SerializeObject(new
                    {
                        error = "APPROVAL_REQUIRED",
                        message = "Invalid or expired approval token.",
                        suggestedFix =
                            "Request a new preview by calling the script tool without _approvalToken."
                    })
                };
                return false;
            }

            // Generate preview token and return approval-required response
            var token = Guid.NewGuid().ToString("N").Substring(0, 12);
            var path = context.Parameters?["path"]?.Value<string>() ?? "unknown";
            var content = context.Parameters?["content"]?.Value<string>();

            // Check for Assets/Editor/ path warning
            var isEditorPath = path.Contains("/Editor/") || path.StartsWith("Assets/Editor");

            _pending[token] = new PendingApproval
            {
                Token = token,
                ToolName = context.ToolName,
                Path = path,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            // Clean up old approvals (older than 5 minutes)
            CleanupExpired();

            var preview = new JObject
            {
                ["status"] = "approval_required",
                ["approvalToken"] = token,
                ["path"] = path,
                ["contentPreview"] = content?.Length > 500
                    ? content.Substring(0, 500) + "..."
                    : content,
                ["contentLength"] = content?.Length ?? 0,
                ["expiresInSeconds"] = 300
            };

            if (isEditorPath)
            {
                preview["warning"] =
                    "This script targets an Editor/ directory. " +
                    "Changes here affect the Unity Editor, not the runtime build.";
            }

            toolResult = new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = JsonConvert.SerializeObject(new
                {
                    success = true,
                    data = preview,
                    errorCode = "APPROVAL_REQUIRED",
                    message =
                        "Script change requires human approval. " +
                        "Include _approvalToken in your next call to execute."
                })
            };

            _logger.Info("Script approval requested",
                ("token", (object)token), ("path", (object)path));
            return false; // block execution until approved
        }

        /// <summary>
        /// Returns the number of currently pending approvals. Useful for testing.
        /// </summary>
        public static int PendingCount => _pending.Count;

        /// <summary>
        /// Clears all pending approvals. Intended for test cleanup.
        /// </summary>
        public static void ClearPending() => _pending.Clear();

        /// <summary>
        /// Returns a read-only snapshot of all pending approvals for UI display.
        /// Automatically cleans up expired entries before returning.
        /// </summary>
        public static IReadOnlyList<PendingApprovalInfo> GetPendingApprovals()
        {
            CleanupExpired();
            return _pending.Values
                .Select(p => new PendingApprovalInfo(
                    p.Token, p.ToolName, p.Path, p.Content, p.CreatedAt))
                .ToList();
        }

        /// <summary>
        /// Removes a pending approval by token (user rejected).
        /// Returns true if the token was found and removed.
        /// </summary>
        public static bool RejectApproval(string token)
        {
            return _pending.Remove(token);
        }

        private static bool IsScriptWriteTool(ExecutionContext context)
        {
            var category = context.ToolEntry?.Category;
            if (string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(context.ToolName))
            {
                var parts = context.ToolName.Split('_');
                if (parts.Length >= 2) category = parts[1];
            }

            if (!string.Equals(category, "script", StringComparison.OrdinalIgnoreCase))
                return false;

            return context.ToolName != null &&
                   (context.ToolName.Contains("create") || context.ToolName.Contains("update"));
        }

        private static void CleanupExpired()
        {
            var expired = new List<string>();
            foreach (var kvp in _pending)
            {
                if ((DateTime.UtcNow - kvp.Value.CreatedAt).TotalMinutes > 5)
                    expired.Add(kvp.Key);
            }

            foreach (var key in expired)
                _pending.Remove(key);
        }

        private sealed class PendingApproval
        {
            public string Token;
            public string ToolName;
            public string Path;
            public string Content;
            public DateTime CreatedAt;
        }
    }
}
