using System;
using System.Text;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Pipeline.Capture;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Mosaic.Bridge.Core.Pipeline.Stages
{
    /// <summary>
    /// Post-execution stage that captures screenshots of visual tool results.
    /// Only runs for tools in visual categories (gameobject, material, component, prefab)
    /// when the execution mode is Verified or Reviewed.
    /// </summary>
    public sealed class VisualVerificationStage : IPipelineStage
    {
        private readonly SceneCaptureService _captureService;
        private readonly PipelineConfiguration _config;
        private readonly IMosaicLogger _logger;

        private static readonly string[] VisualCategories =
            { "gameobject", "material", "component", "prefab", "asset" };

        public VisualVerificationStage(
            SceneCaptureService captureService,
            PipelineConfiguration config,
            IMosaicLogger logger)
        {
            _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
        {
            // Only capture in Verified or Reviewed modes
            if (context.Mode < ExecutionMode.Verified)
                return true;

            // Only capture for visual tool categories
            if (!IsVisualTool(context))
                return true;

            // Try to find the target GameObject from the tool result
            var target = FindTargetFromResult(toolResult, context);
            if (target == null)
            {
                _logger.Trace("Visual verification skipped — no target GameObject found",
                    ("tool", (object)context.ToolName));
                return true;
            }

            try
            {
                var settings = new CaptureSettings
                {
                    Resolution = _config.CaptureResolution,
                    Angles = _config.CaptureAngles.Split(',')
                };

                var screenshots = _captureService.CaptureAroundTarget(target, settings);
                context.Screenshots.AddRange(screenshots);

                _logger.Info("Visual verification captured screenshots",
                    ("tool", (object)context.ToolName),
                    ("count", (object)screenshots.Count));
            }
            catch (Exception ex)
            {
                // Screenshot failure should not break the tool call
                context.Warnings.Add($"Screenshot capture failed: {ex.Message}");
                _logger.Warn($"Screenshot capture failed: {ex.Message}");
            }

            return true;
        }

        private static bool IsVisualTool(ExecutionContext context)
        {
            var category = context.ToolEntry?.Category;
            if (string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(context.ToolName))
            {
                var parts = context.ToolName.Split('_');
                if (parts.Length >= 2) category = parts[1];
            }

            if (string.IsNullOrEmpty(category)) return false;

            foreach (var vc in VisualCategories)
            {
                if (string.Equals(category, vc, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static GameObject FindTargetFromResult(HandlerResponse toolResult, ExecutionContext context)
        {
            if (toolResult?.Body == null) return null;

            try
            {
                var body = JObject.Parse(toolResult.Body);

                // Try to find InstanceId in the result data
                var instanceId = body["data"]?["InstanceId"]?.Value<int>()
                              ?? body["InstanceId"]?.Value<int>()
                              ?? body["data"]?["instanceId"]?.Value<int>()
                              ?? body["instanceId"]?.Value<int>();

                if (instanceId.HasValue && instanceId.Value != 0)
                {
                    var obj = UnityEditor.EditorUtility.EntityIdToObject(instanceId.Value) as GameObject;
                    if (obj != null) return obj;
                }

                // Try to find by name from result
                var name = (string)body["data"]?["Name"]
                        ?? (string)body["Name"]
                        ?? (string)body["data"]?["name"]
                        ?? (string)body["name"];

                if (!string.IsNullOrEmpty(name))
                    return GameObject.Find(name);

                // Try to find by name from parameters
                var paramName = (string)context.Parameters?["name"]
                             ?? (string)context.Parameters?["gameObjectName"];

                if (!string.IsNullOrEmpty(paramName))
                    return GameObject.Find(paramName);
            }
            catch
            {
                // Best-effort — if parsing fails, skip screenshot
            }

            return null;
        }
    }
}
