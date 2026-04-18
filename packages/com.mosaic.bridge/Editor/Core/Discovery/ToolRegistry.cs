using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Knowledge;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Security;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Discovery
{
    public sealed class ToolRegistry : IToolRunner
    {
        private readonly IReadOnlyDictionary<string, ToolRegistryEntry> _entries;
        private readonly IMosaicLogger _logger;

        /// <summary>Number of registered tools.</summary>
        public int Count => _entries.Count;

        /// <summary>Returns true if the named tool is marked as read-only.</summary>
        public bool IsReadOnly(string toolName)
        {
            return _entries.TryGetValue(toolName, out var entry) && entry.IsReadOnly;
        }

        /// <summary>Looks up a tool entry by name. Returns null if not found.</summary>
        public ToolRegistryEntry GetEntry(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return null;
            _entries.TryGetValue(toolName, out var entry);
            return entry;
        }

        /// <summary>
        /// Returns all tools that are safe to run in compiled builds (Context == Runtime or Both).
        /// </summary>
        public IReadOnlyList<ToolRegistryEntry> GetRuntimeTools()
        {
            return _entries.Values
                .Where(e => e.Context == ToolContext.Runtime || e.Context == ToolContext.Both)
                .ToList();
        }

        /// <summary>
        /// Returns all tools matching the specified context.
        /// For Editor context, returns Editor + Both tools (everything available in editor).
        /// For Runtime context, returns Runtime + Both tools.
        /// For Both context, returns only tools explicitly marked Both.
        /// </summary>
        public IReadOnlyList<ToolRegistryEntry> GetToolsByContext(ToolContext context)
        {
            return context switch
            {
                ToolContext.Editor => _entries.Values
                    .Where(e => e.Context == ToolContext.Editor || e.Context == ToolContext.Both)
                    .ToList(),
                ToolContext.Runtime => _entries.Values
                    .Where(e => e.Context == ToolContext.Runtime || e.Context == ToolContext.Both)
                    .ToList(),
                ToolContext.Both => _entries.Values
                    .Where(e => e.Context == ToolContext.Both)
                    .ToList(),
                _ => _entries.Values.ToList()
            };
        }

        public ToolRegistry(IReadOnlyList<ToolRegistryEntry> entries, IMosaicLogger logger)
        {
            _logger = logger;
            var dict = new Dictionary<string, ToolRegistryEntry>(entries.Count);
            foreach (var entry in entries)
                dict[entry.ToolName] = entry;
            _entries = dict;
        }

        /// <summary>
        /// Production factory: discovers all static methods decorated with [MosaicTool]
        /// via Unity's TypeCache and builds the registry.
        /// </summary>
        public static ToolRegistry BuildFromTypeCache(IMosaicLogger logger)
        {
            var methods = TypeCache.GetMethodsWithAttribute<MosaicToolAttribute>();
            var entries = new List<ToolRegistryEntry>();

            int skippedByGuard = 0;

            foreach (var method in methods)
            {
                // Assembly guard: only allow tools from explicitly trusted assemblies
                var assemblyName = method.DeclaringType?.Assembly.GetName().Name ?? "";
                if (!AssemblyGuard.IsAllowed(assemblyName))
                {
                    logger.Warn("Skipping [MosaicTool] in disallowed assembly — add it to " +
                                "Project Settings > Mosaic Bridge > Allowed Tool Assemblies",
                        ("method", (object)$"{method.DeclaringType?.Name}.{method.Name}"),
                        ("assembly", (object)assemblyName));
                    skippedByGuard++;
                    continue;
                }

                if (!method.IsStatic)
                {
                    logger.Warn("Skipping non-static [MosaicTool] method",
                        ("method", (object)$"{method.DeclaringType?.Name}.{method.Name}"));
                    continue;
                }

                var returnType = method.ReturnType;
                if (returnType != typeof(void) &&
                    !returnType.Name.StartsWith("ToolResult", StringComparison.Ordinal))
                {
                    logger.Warn("Skipping [MosaicTool] method with invalid return type",
                        ("method", (object)$"{method.DeclaringType?.Name}.{method.Name}"),
                        ("returnType", (object)returnType.Name));
                    continue;
                }

                var attr = method.GetCustomAttribute<MosaicToolAttribute>();
                var parameters = method.GetParameters();
                var paramType = parameters.Length > 0 ? parameters[0].ParameterType : null;
                var toolName = "mosaic_" + attr.Route.Replace('/', '_');

                entries.Add(new ToolRegistryEntry(toolName, attr, method, paramType));
            }

            if (skippedByGuard > 0)
                logger.Warn("Tools skipped by AssemblyGuard",
                    ("skippedCount", (object)skippedByGuard));

            logger.Info("ToolRegistry built", ("count", (object)entries.Count), ("status", (object)"registered"));
            return new ToolRegistry(entries, logger);
        }

        // ── IToolRunner ──────────────────────────────────────────────────────────

        public HandlerResponse Execute(HandlerRequest request)
        {
            var path = request.RawUrl?.Split('?')[0].TrimEnd('/') ?? string.Empty;

            if (request.Method == "GET" && path == "/health")
            {
                var payload = new
                {
                    status = "ok",
                    bridge_state = BridgeBootstrap.State.ToString(),
                    tool_count = _entries.Count,
                    version = "1.0.0"
                };
                return new HandlerResponse
                {
                    StatusCode = 200,
                    ContentType = "application/json",
                    Body = JsonConvert.SerializeObject(payload, Formatting.None)
                };
            }

            if (request.Method == "POST" && path == "/execute")
                return HandleExecute(request);

            if (request.Method == "GET" && path == "/tools")
                return HandleGetTools();

            // Story 5.5: Knowledge base endpoints
            if (request.Method == "GET" && path == "/kb/list")
                return HandleKbList();

            if (request.Method == "GET" && path.StartsWith("/kb/read/"))
                return HandleKbRead(path.Substring("/kb/read/".Length));

            return new HandlerResponse
            {
                StatusCode = 404,
                ContentType = "application/json",
                Body = "{\"error\":\"not_found\"}"
            };
        }

        // ── Private handlers ─────────────────────────────────────────────────────

        private HandlerResponse HandleExecute(HandlerRequest request)
        {
            string bodyText;
            try
            {
                bodyText = Encoding.UTF8.GetString(request.Body);
            }
            catch (Exception ex)
            {
                return HandlerResponse.InternalError(ex.Message);
            }

            JObject body;
            try
            {
                body = JObject.Parse(bodyText);
            }
            catch (JsonException)
            {
                return new HandlerResponse
                {
                    StatusCode = 400,
                    ContentType = "application/json",
                    Body = "{\"error\":\"invalid_json\"}"
                };
            }

            var toolName = body["tool"]?.Value<string>();
            if (string.IsNullOrEmpty(toolName))
            {
                return new HandlerResponse
                {
                    StatusCode = 400,
                    ContentType = "application/json",
                    Body = "{\"error\":\"INVALID_PARAM\",\"message\":\"'tool' field required\"}"
                };
            }

            if (!_entries.TryGetValue(toolName, out var entry))
            {
                return new HandlerResponse
                {
                    StatusCode = 404,
                    ContentType = "application/json",
                    Body = JsonConvert.SerializeObject(new { error = "NOT_FOUND", message = $"Unknown tool: {toolName}" })
                };
            }

            var paramsToken = body["parameters"];
            var paramsJson = paramsToken?.ToString(Formatting.None);

            var validation = ParameterValidator.Bind(paramsJson, entry.ParamType);
            if (!validation.IsValid)
            {
                return new HandlerResponse
                {
                    StatusCode = 400,
                    ContentType = "application/json",
                    Body = JsonConvert.SerializeObject(new { error = validation.ErrorCode, message = validation.ErrorMessage })
                };
            }

            object returnValue;
            try
            {
                var args = entry.ParamType != null
                    ? new[] { validation.Value }
                    : Array.Empty<object>();
                returnValue = entry.Method.Invoke(null, args);
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                _logger.Error("Tool threw exception", inner, ("tool", (object)toolName));
                return HandlerResponse.InternalError(inner.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Tool invocation failed", ex, ("tool", (object)toolName));
                return HandlerResponse.InternalError(ex.Message);
            }

            var status = ToHttpStatus(returnValue);
            var resultJson = JsonConvert.SerializeObject(returnValue, Formatting.None);

            return new HandlerResponse
            {
                StatusCode = status,
                ContentType = "application/json",
                Body = resultJson
            };
        }

        private HandlerResponse HandleGetTools()
        {
            var tools = _entries.Values.Select(e => new
            {
                name = e.ToolName,
                description = e.Description,
                category = e.Category ?? "general",
                isReadOnly = e.IsReadOnly,
                context = e.Context.ToString().ToLowerInvariant(),
                inputSchema = JsonSchemaGenerator.Generate(e.ParamType)
            }).ToList();

            return new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = JsonConvert.SerializeObject(new { tools }, Formatting.None)
            };
        }

        // ── Knowledge Base handlers (Story 5.5) ────────────────────────────────

        private HandlerResponse HandleKbList()
        {
            var entries = new JArray();

            // Physics constants
            var physics = KnowledgeBase.GetPhysicsConstants();
            if (physics != null)
            {
                var constants = physics["constants"] as JObject;
                if (constants != null)
                {
                    foreach (var prop in constants.Properties())
                    {
                        entries.Add(new JObject
                        {
                            ["uri"] = $"mosaic://knowledge/physics/{prop.Name}",
                            ["name"] = prop.Name,
                            ["category"] = "physics",
                            ["description"] = prop.Value["description"]?.Value<string>() ?? ""
                        });
                    }
                }
            }

            // PBR materials
            var pbrMaterials = KnowledgeBase.GetAllPbrMaterials();
            if (pbrMaterials != null)
            {
                foreach (var entry in pbrMaterials)
                {
                    var name = entry["name"]?.Value<string>() ?? "unknown";
                    entries.Add(new JObject
                    {
                        ["uri"] = $"mosaic://knowledge/rendering/{name}",
                        ["name"] = name,
                        ["category"] = "rendering",
                        ["description"] = entry["description"]?.Value<string>() ?? ""
                    });
                }
            }

            return new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = new JObject { ["entries"] = entries }.ToString(Formatting.None)
            };
        }

        private HandlerResponse HandleKbRead(string entryPath)
        {
            // entryPath format: "physics/gravity_earth" or "rendering/wood_oak"
            var parts = entryPath.Split(new[] { '/' }, 2);
            if (parts.Length != 2)
            {
                return new HandlerResponse
                {
                    StatusCode = 400,
                    ContentType = "application/json",
                    Body = "{\"error\":\"INVALID_PARAM\",\"message\":\"Expected format: category/entryKey\"}"
                };
            }

            var category = parts[0];
            var key = parts[1];
            JToken entry = null;

            if (category == "physics")
            {
                entry = KnowledgeBase.GetConstant(key);
            }
            else if (category == "rendering")
            {
                entry = KnowledgeBase.GetPbrMaterial(key);
            }

            if (entry == null)
            {
                return new HandlerResponse
                {
                    StatusCode = 404,
                    ContentType = "application/json",
                    Body = JsonConvert.SerializeObject(new { error = "NOT_FOUND", message = $"No KB entry: {category}/{key}" })
                };
            }

            return new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = new JObject
                {
                    ["uri"] = $"mosaic://knowledge/{category}/{key}",
                    ["category"] = category,
                    ["key"] = key,
                    ["data"] = entry
                }.ToString(Formatting.None)
            };
        }

        // ── HTTP status mapping ──────────────────────────────────────────────────

        private static int ToHttpStatus(object returnValue)
        {
            if (returnValue == null) return 200;

            var type = returnValue.GetType();

            // ToolResult<T> uses "Success" (not "IsSuccess")
            var isSuccessProp = type.GetProperty("Success");
            if (isSuccessProp == null) return 200;

            bool isSuccess = (bool)isSuccessProp.GetValue(returnValue);
            if (isSuccess) return 200;

            var errorCodeProp = type.GetProperty("ErrorCode");
            string errorCode = errorCodeProp?.GetValue(returnValue) as string;

            return errorCode switch
            {
                ErrorCodes.INVALID_PARAM => 400,
                ErrorCodes.TYPE_MISMATCH => 400,
                ErrorCodes.OUT_OF_RANGE  => 400,
                ErrorCodes.NOT_FOUND     => 404,
                ErrorCodes.NOT_PERMITTED => 403,
                ErrorCodes.CONFLICT      => 409,
                ErrorCodes.UNAUTHORIZED  => 401,
                ErrorCodes.RATE_LIMITED  => 429,
                ErrorCodes.BRIDGE_BUSY   => 503,
                _                        => 500
            };
        }
    }
}
