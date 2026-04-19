using System;
using System.Text;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Authentication;
using Mosaic.Bridge.Core.Diagnostics;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Mcp;
using Mosaic.Bridge.Core.Licensing;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Pipeline.Capture;
using Mosaic.Bridge.Core.Pipeline.Stages;
using Mosaic.Bridge.Core.Pipeline.Validation;
using Mosaic.Bridge.Core.Runtime;
using Mosaic.Bridge.Core.Security;
using Mosaic.Bridge.Core.Server;
using UnityEditor;

namespace Mosaic.Bridge.Core.Bootstrap
{
    /// <summary>
    /// Unity Editor entry point for Mosaic Bridge. Runs on Editor load and after every domain
    /// reload. Sets up the runtime directory, shared secret, nonce cache, HMAC authenticator,
    /// and HTTP server.
    /// </summary>
    [InitializeOnLoad]
    public sealed class BridgeBootstrap
    {
        private const string SecretSessionKey = "Mosaic.Bridge.SecretBase64";

        /// <summary>The 32-byte shared HMAC secret for this Editor session.</summary>
        public static byte[] Secret { get; private set; }

        /// <summary>The singleton nonce replay cache.</summary>
        public static NonceCache NonceCache { get; private set; }

        /// <summary>The singleton HMAC authenticator wired to <see cref="Secret"/> and <see cref="NonceCache"/>.</summary>
        public static HmacAuthenticator Authenticator { get; private set; }

        /// <summary>The shared logger for all Mosaic Bridge components.</summary>
        public static IMosaicLogger Logger { get; private set; }

        /// <summary>The absolute path to the per-user runtime directory.</summary>
        public static string RuntimeDirectory { get; private set; }

        /// <summary>The HTTP server. Null if bootstrap failed.</summary>
        public static BridgeServer Server { get; private set; }

        /// <summary>The main-thread dispatcher. Null if bootstrap failed.</summary>
        public static MainThreadDispatcher Dispatcher { get; private set; }

        /// <summary>The tool registry built from TypeCache discovery. Null if bootstrap failed.</summary>
        public static ToolRegistry ToolRegistry { get; private set; }

        /// <summary>The MCP server child process manager. Null if bootstrap failed.</summary>
        public static McpServerProcess McpProcess { get; private set; }

        /// <summary>The file logger for structured JSONL diagnostics. Null if bootstrap failed.</summary>
        public static FileLogger FileLog { get; private set; }

        /// <summary>Current lifecycle state of the bridge.</summary>
        public static BridgeState State { get; private set; } = BridgeState.Uninitialized;

        static BridgeBootstrap()
        {
            // Guard: only run in the interactive Editor process. Unity spawns helper
            // subprocesses (AssetImportWorker, -batchmode builds, etc.) that also fire
            // [InitializeOnLoad]. If those run the bootstrap, they (a) fight for the
            // bridge port and (b) subscribe to EditorApplication.quitting, which then
            // deletes the shared discovery file when the worker exits — leaving the
            // main Editor's bridge unreachable to the MCP server.
            if (IsHelperSubprocess())
            {
                // State remains Uninitialized; no handlers registered, no files written.
                return;
            }

            try
            {
                State = BridgeState.Starting;
                // Boot with console-only so RuntimeDirectory resolution can log
                var consoleLogger = new UnityConsoleLogger();
                Logger = new RedactingLogger(consoleLogger);

                RuntimeDirectory = RuntimeDirectoryResolver.Resolve(Logger);

                // Story 10.1: Structured JSONL file logging alongside Unity console
                FileLog = new FileLogger(RuntimeDirectory);
                var compositeLogger = new CompositeLogger(consoleLogger, FileLog);
                Logger = new RedactingLogger(compositeLogger);

                // Reuse the secret across domain reloads so in-flight requests survive recompiles.
                var existingB64 = SessionState.GetString(SecretSessionKey, null);
                if (!string.IsNullOrEmpty(existingB64) && IsValidSecret(existingB64))
                {
                    Secret = Convert.FromBase64String(existingB64);
                }
                else
                {
                    var b64 = SecretGenerator.GenerateBase64();
                    Secret = Convert.FromBase64String(b64);
                    SessionState.SetString(SecretSessionKey, b64);
                }

                NonceCache = new NonceCache();
                Authenticator = new HmacAuthenticator(Secret, NonceCache, Logger);

                // Story 1.9: Prune stale instances from previous crashes before we register
                InstanceRegistry.PruneStale();

                Server = new BridgeServer(Authenticator, Logger);
                int preferredPort = EditorPrefs.GetInt("Mosaic.Bridge.Port", 8282);
                try
                {
                    Server.Start(preferredPort);
                }
                catch (Exception portEx)
                {
                    StartupStatusWriter.WriteError(RuntimeDirectory, ErrorCodes.PORT_BIND_FAILED,
                        $"Could not bind to port {preferredPort}: {portEx.Message}",
                        "Check if another process is using this port, or set Mosaic.Bridge.Port in EditorPrefs to 0 for auto-assign.");
                    State = BridgeState.Error;
                    Logger.Error($"Port bind failed: {portEx.Message}");
                    return;
                }

                WriteDiscoveryFile();

                // Story 1.10: Spawn MCP server child process
                McpProcess = new McpServerProcess(new SystemProcessLauncher(), Logger);
                var discoveryPath = RuntimeDirectoryResolver.GetDiscoveryFilePath();
                var mcpPid = McpProcess.Start(discoveryPath);
                if (mcpPid > 0)
                {
                    Logger.Info("MCP server process managed", ("pid", (object)mcpPid));
                }

                // Story 1.11: Queue capacity 200 with read/write classification
                Dispatcher = new MainThreadDispatcher(
                    new ToolQueue(capacity: 200),
                    Logger,
                    toolReadOnlyLookup: toolName => ToolRegistry?.IsReadOnly(toolName) ?? false);
                Dispatcher.Start();
                RegisterHandler(Dispatcher);

                try
                {
                    ToolRegistry = ToolRegistry.BuildFromTypeCache(Logger);
                }
                catch (Exception tcEx)
                {
                    StartupStatusWriter.WriteError(RuntimeDirectory, ErrorCodes.TYPECACHE_FAILED,
                        $"TypeCache tool discovery failed: {tcEx.Message}");
                    Server.Stop();
                    State = BridgeState.Error;
                    Logger.Error($"TypeCache failed: {tcEx.Message}");
                    return;
                }

                if (ToolRegistry.Count == 0)
                {
                    StartupStatusWriter.WriteError(RuntimeDirectory, ErrorCodes.NO_TOOLS_FOUND,
                        "TypeCache found zero [MosaicTool] methods. Check assembly compilation errors.");
                    Server.Stop();
                    State = BridgeState.Error;
                    Logger.Error("No tools found after TypeCache discovery");
                    return;
                }

                // Story 12.1: Wrap ToolRegistry with ExecutionPipeline
                var pipelineConfig = new PipelineConfiguration();
                var pipeline = new ExecutionPipeline(
                    ToolRegistry,
                    pipelineConfig,
                    Logger,
                    toolName => ToolRegistry.GetEntry(toolName));
                // Story 7.1: Trial gate — blocks tool calls when trial expired or quota exhausted
                var trialManager = new TrialManager();
                pipeline.AddPreStage(new TrialGateStage(trialManager));

                // Story 12.2: Semantic pre-validation (runs for modes >= Validated)
                pipeline.AddPreStage(new SemanticValidatorStage(new IValidationRule[]
                {
                    new TransformRangeRule(),
                    new PbrRangeRule(),
                    new DuplicateComponentRule(),
                    new ScriptExistsRule()
                }));

                // Story 8.3: Script approval gate — blocks script writes behind human approval token
                pipeline.AddPreStage(new ScriptApprovalStage(Logger));

                // Story 12.3: Knowledge advisor — proactive KB suggestions for materials and physics
                pipeline.AddPreStage(new KnowledgeAdvisorStage(Logger));

                // Story 12.5: Visual verification — captures screenshots after visual tool execution
                pipeline.AddPostStage(new VisualVerificationStage(
                    new SceneCaptureService(), pipelineConfig, Logger));

                // Story 12.7: Code review — checks compilation after script create/update
                pipeline.AddPostStage(new CodeReviewStage(pipelineConfig, Logger));

                // Auto test runner — runs EditMode tests after script changes (opt-in)
                pipeline.AddPostStage(new TestRunnerStage(pipelineConfig, Logger));

                RegisterToolRunner(pipeline);
                State = BridgeState.Running;

                // Story 1.12: Write success status
                StartupStatusWriter.WriteSuccess(RuntimeDirectory, Server.Port, ToolRegistry.Count);

                // Story 1.9: Register this instance
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var projectHash = RuntimeDirectoryResolver.GetProjectHash();
                var projectPath = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
                InstanceRegistry.Register(InstanceRegistryEntry.Create(pid, Server.Port, projectHash, projectPath));

                EditorApplication.quitting += OnEditorQuitting;

                Logger.Info("Mosaic Bridge bootstrap complete",
                    ("runtimeDirectory", (object)RuntimeDirectory),
                    ("port", (object)Server.Port),
                    ("tools", (object)ToolRegistry.Count));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Mosaic.Bridge] Bootstrap failed: {ex}");
                if (!string.IsNullOrEmpty(RuntimeDirectory))
                    StartupStatusWriter.WriteError(RuntimeDirectory, ErrorCodes.BOOTSTRAP_UNKNOWN, ex.Message);
                State = BridgeState.Error;
            }
        }

        /// <summary>
        /// Graceful teardown for domain reload. Called by <see cref="DomainReloadHandler"/> via
        /// AssemblyReloadEvents.beforeAssemblyReload. Also called by <see cref="OnEditorQuitting"/>.
        /// </summary>
        public static void ShutdownForReload()
        {
            State = BridgeState.Reloading;
            Dispatcher?.DrainForDomainReload();   // unblock any waiting HTTP threads first
            Server?.Stop();                        // stop accepting new connections
            // NOTE: Do NOT delete the discovery file on domain reload.
            // The MCP server needs it to reconnect after the reload completes.
            // The bootstrap will overwrite it with the new port/secret on restart.
            // Discovery file is only deleted on actual editor quit (OnEditorQuitting).
            Logger?.Info("Bridge shutdown for domain reload complete");
            // NOTE: do NOT clear SessionState here — secret survives domain reload intentionally
        }

        private static void WriteDiscoveryFile()
        {
            DiscoveryFile.Write(new DiscoveryFileData
            {
                SchemaVersion = DiscoveryFileData.CurrentSchemaVersion,
                Port = Server.Port,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                StartedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SecretBase64 = SessionState.GetString("Mosaic.Bridge.SecretBase64", string.Empty),
                UnityProjectPath = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath),
                UnityVersion = UnityEngine.Application.unityVersion,
                TlsEnabled = EditorPrefs.GetBool("MosaicBridge.TlsEnabled", false)
            }, Secret, Logger);
        }

        /// <summary>
        /// Registers a handler with the running server. Safe to call before Start() completes
        /// (Server will be null and this is a no-op).
        /// </summary>
        public static void RegisterHandler(IRequestHandler handler)
        {
            Server?.SetHandler(handler);
        }

        private static bool IsValidSecret(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                return bytes.Length == 32;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Wires a tool runner into the dispatcher. Called by B06 (ToolDiscovery) after it builds
        /// the tool registry.
        /// </summary>
        public static void RegisterToolRunner(IToolRunner runner)
        {
            Dispatcher?.SetRunner(runner);
        }

        private static void OnEditorQuitting()
        {
            // Story 1.10: Stop MCP server BEFORE shutting down the bridge
            McpProcess?.Stop();

            // Story 1.9: Deregister from instance registry on actual quit
            try
            {
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                InstanceRegistry.Deregister(pid);
            }
            catch { /* best-effort */ }

            ShutdownForReload();
            DiscoveryFile.Delete(Logger);          // Only delete discovery file on actual quit
            StartupStatusWriter.CleanUp(RuntimeDirectory);  // Story 1.12: Clean up status.json
            SessionState.EraseString(SecretSessionKey);
            State = BridgeState.Stopped;
            Logger?.Info("Bridge stopped (editor quit)");

            // Story 10.1: Flush and close the file logger
            FileLog?.Dispose();
            FileLog = null;
        }

        /// <summary>
        /// Returns true for Unity helper subprocesses that should NOT run the bridge bootstrap:
        /// AssetImportWorker, -batchmode command-line sessions, -adb2 asset-db workers, etc.
        /// Only the interactive main Editor should bind the bridge port, write the shared
        /// discovery file, and subscribe to EditorApplication.quitting.
        /// </summary>
        private static bool IsHelperSubprocess()
        {
            // Fast path: Unity's public isBatchMode covers -batchmode, -nographics, and
            // AssetImportWorker subprocesses (which launch in batch mode by design).
            if (UnityEngine.Application.isBatchMode)
                return true;

            // Defense-in-depth: scan command-line args for markers of helper processes
            // that may not be caught by isBatchMode on all Unity versions.
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args != null)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        var a = args[i];
                        if (string.IsNullOrEmpty(a)) continue;

                        if (a.IndexOf("AssetImportWorker", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        if (a.Equals("-adb2", StringComparison.OrdinalIgnoreCase)) return true;
                        if (a.Equals("-batchmode", StringComparison.OrdinalIgnoreCase)) return true;
                        if (a.Equals("-importPackage", StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            catch
            {
                // If argv inspection fails we'd rather run the bootstrap than silently skip.
            }

            return false;
        }

        /// <summary>
        /// Minimal Unity console logger. All Mosaic Bridge components log through
        /// <see cref="IMosaicLogger"/> — this is the only place that touches UnityEngine.Debug.
        /// </summary>
        private sealed class UnityConsoleLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

            public void Trace(string message, params (string Key, object Value)[] context)
            {
                if (!IsEnabled(LogLevel.Trace)) return;
                UnityEngine.Debug.Log(Format("TRACE", message, context));
            }

            public void Debug(string message, params (string Key, object Value)[] context)
            {
                if (!IsEnabled(LogLevel.Debug)) return;
                UnityEngine.Debug.Log(Format("DEBUG", message, context));
            }

            public void Info(string message, params (string Key, object Value)[] context)
            {
                if (!IsEnabled(LogLevel.Info)) return;
                UnityEngine.Debug.Log(Format("INFO", message, context));
            }

            public void Warn(string message, params (string Key, object Value)[] context)
            {
                if (!IsEnabled(LogLevel.Warn)) return;
                UnityEngine.Debug.LogWarning(Format("WARN", message, context));
            }

            public void Error(string message, Exception exception = null, params (string Key, object Value)[] context)
            {
                if (!IsEnabled(LogLevel.Error)) return;
                var formatted = Format("ERROR", message, context);
                if (exception != null)
                    formatted += $"\n{exception}";
                UnityEngine.Debug.LogError(formatted);
            }

            private static string Format(string level, string message, (string Key, object Value)[] context)
            {
                var sb = new StringBuilder($"[Mosaic.Bridge] {level} {message}");
                foreach (var (key, value) in context)
                    sb.Append($" {key}={value}");
                return sb.ToString();
            }
        }
    }
}
