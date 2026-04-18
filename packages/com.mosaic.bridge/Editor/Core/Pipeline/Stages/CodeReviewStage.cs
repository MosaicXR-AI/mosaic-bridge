using System;
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace Mosaic.Bridge.Core.Pipeline.Stages
{
    /// <summary>
    /// Post-execution pipeline stage that checks script compilation after
    /// script create/update tools. Purely informational — never aborts the pipeline.
    /// </summary>
    public sealed class CodeReviewStage : IPipelineStage
    {
        private readonly PipelineConfiguration _config;
        private readonly IMosaicLogger _logger;

        public CodeReviewStage(PipelineConfiguration config, IMosaicLogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
        {
            // Only run for script category tools
            if (!IsScriptTool(context))
                return true;

            // Check config
            if (!_config.CodeReviewEnabled)
                return true;

            // Only run for modes >= Validated
            if (context.Mode < ExecutionMode.Validated)
                return true;

            // Only run for write operations (create/update)
            if (context.ToolName != null &&
                !context.ToolName.Contains("create") &&
                !context.ToolName.Contains("update"))
                return true;

            // Trigger asset refresh to compile
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // Collect compilation results
            var codeReview = new JObject();
            var startTime = EditorApplication.timeSinceStartup;

            // Check if compilation succeeded or failed
            // Note: AssetDatabase.Refresh is synchronous for script compilation in most cases
            bool hasErrors = EditorUtility.scriptCompilationFailed;
            codeReview["compilationSucceeded"] = !hasErrors;
            codeReview["compilationTimeMs"] = (int)((EditorApplication.timeSinceStartup - startTime) * 1000);

            if (hasErrors)
            {
                context.Warnings.Add("Script compilation failed. Check Unity Console for details.");
                _logger.Warn("Code review: compilation failed after script tool execution",
                    ("tool", (object)context.ToolName));
            }
            else
            {
                _logger.Info("Code review: compilation succeeded",
                    ("tool", (object)context.ToolName));
            }

            // Merge code review into the response
            try
            {
                var body = JObject.Parse(toolResult.Body);
                body["codeReview"] = codeReview;
                toolResult = new HandlerResponse
                {
                    StatusCode = toolResult.StatusCode,
                    ContentType = toolResult.ContentType,
                    Body = body.ToString(Formatting.None),
                    Headers = toolResult.Headers
                };
            }
            catch
            {
                // If body isn't JSON, add as warning instead
                context.Warnings.Add($"Code review: compilation {(hasErrors ? "failed" : "succeeded")}");
            }

            return true; // Never abort the pipeline — code review is informational
        }

        private static bool IsScriptTool(ExecutionContext context)
        {
            var category = context.ToolEntry?.Category;
            if (string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(context.ToolName))
            {
                // Parse category from tool name format: "mosaic_category_action"
                var parts = context.ToolName.Split('_');
                if (parts.Length >= 2) category = parts[1];
            }
            return string.Equals(category, "script", StringComparison.OrdinalIgnoreCase);
        }
    }
}
