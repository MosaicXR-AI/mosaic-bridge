using System;
using System.Diagnostics;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Discovery;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Tools.Meta
{
    public static class MetaAdvancedToolTool
    {
        [MosaicTool("meta/advanced_tool",
                    "Invokes any tool by route name. Use meta/list_advanced_tools to discover available tools.",
                    isReadOnly: false)]
        public static ToolResult<MetaAdvancedToolResult> Execute(MetaAdvancedToolParams p)
        {
            if (string.IsNullOrEmpty(p.ToolName))
                return ToolResult<MetaAdvancedToolResult>.Fail(
                    "ToolName is required", ErrorCodes.INVALID_PARAM);

            var registry = BridgeBootstrap.ToolRegistry;
            if (registry == null)
                return ToolResult<MetaAdvancedToolResult>.Fail(
                    "ToolRegistry not initialized", ErrorCodes.INTERNAL_ERROR);

            var entry = registry.GetEntry(p.ToolName);
            if (entry == null)
                return ToolResult<MetaAdvancedToolResult>.Fail(
                    $"Tool not found: '{p.ToolName}'", ErrorCodes.NOT_FOUND);

            var sw = Stopwatch.StartNew();

            try
            {
                // Deserialize arguments to the tool's parameter type
                object paramObj = null;
                if (entry.ParamType != null)
                {
                    var json = p.Arguments?.ToString(Formatting.None) ?? "{}";
                    var validation = ParameterValidator.Bind(json, entry.ParamType);
                    if (!validation.IsValid)
                        return ToolResult<MetaAdvancedToolResult>.Fail(
                            validation.ErrorMessage, validation.ErrorCode);
                    paramObj = validation.Value;
                }

                var args = entry.ParamType != null
                    ? new[] { paramObj }
                    : Array.Empty<object>();

                var result = entry.Method.Invoke(null, args);
                sw.Stop();

                return ToolResult<MetaAdvancedToolResult>.Ok(new MetaAdvancedToolResult
                {
                    ToolName = p.ToolName,
                    Success = true,
                    Data = result,
                    DurationMs = sw.ElapsedMilliseconds
                });
            }
            catch (TargetInvocationException tie)
            {
                sw.Stop();
                var inner = tie.InnerException ?? tie;
                return ToolResult<MetaAdvancedToolResult>.Ok(new MetaAdvancedToolResult
                {
                    ToolName = p.ToolName,
                    Success = false,
                    Error = inner.Message,
                    DurationMs = sw.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                return ToolResult<MetaAdvancedToolResult>.Ok(new MetaAdvancedToolResult
                {
                    ToolName = p.ToolName,
                    Success = false,
                    Error = ex.Message,
                    DurationMs = sw.ElapsedMilliseconds
                });
            }
        }
    }
}
