using System;
using System.IO;
using System.Text;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.EditorOps
{
    // ── Submit ────────────────────────────────────────────────────────────────

    public static class EditorRunBlockTool
    {
        private const string TempFolderParent = "Assets";
        private const string TempFolderName   = "Editor";
        private const string TempFolder       = "Assets/Editor";
        private const string ClassPrefix      = "MosaicBridge_RunBlock_";
        private const string PrefPrefix       = "MosaicBridgeRunBlock_";

        [MosaicTool("editor/run-block",
                    "Submits a multi-statement C# code block for execution inside the Unity Editor. " +
                    "Use this for custom logic that has NO dedicated MCP tool: " +
                    "data queries, bulk renames, custom AssetDatabase operations, editor automation, etc. " +
                    "⛔ DO NOT use this tool for anything that has a dedicated MCP tool — it is slower, " +
                    "   harder to debug, and bypasses workflow enforcement. Specifically, NEVER use " +
                    "   editor/run-block to: create GameObjects (use gameobject/create or scene/create-object), " +
                    "   create ProBuilder meshes (use probuilder/create), create materials (use material/create), " +
                    "   instantiate prefabs (use asset/instantiate_prefab), write shaders (use shadergraph/*), " +
                    "   or build any 3D object from ProBuilder API calls. " +
                    "HOW IT WORKS: " +
                    "1. The block is wrapped in a temp [InitializeOnLoad] Editor class and compiled by Unity. " +
                    "2. After compilation (3-10 seconds), the code runs automatically on the next Editor tick. " +
                    "3. Call editor/run-block-poll with the returned JobId to check the result. " +
                    "NOTE: A domain reload occurs during compilation — this is normal. " +
                    "NOTE: Do NOT call this if the project already has compile errors. " +
                    "Default usings included: System, System.Collections.Generic, System.Linq, " +
                    "UnityEngine, UnityEditor. Add extras via Usings param if needed. " +
                    "Debug.Log() calls inside the block are captured and returned in Output.",
                    isReadOnly: false)]
        public static ToolResult<RunBlockSubmitResult> Submit(RunBlockParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Code))
                return ToolResult<RunBlockSubmitResult>.Fail(
                    "Code is required.", ErrorCodes.INVALID_PARAM);

            // Ensure Assets/Editor exists
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder(TempFolderParent, TempFolderName);

            string jobId     = Guid.NewGuid().ToString("N").Substring(0, 12);
            string className = ClassPrefix + jobId;
            string scriptPath = TempFolder + "/" + className + ".cs";
            string fullPath   = Path.GetFullPath(scriptPath);

            // Clear any stale prefs from a previous job with the same ID (astronomically unlikely)
            ClearJobPrefs(jobId);

            // Write the temp script
            string script = BuildScript(className, jobId, p.Code, p.Usings);
            File.WriteAllText(fullPath, script);

            // Record submission time BEFORE triggering compile (survives domain reload)
            long submitted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            EditorPrefs.SetString(PrefPrefix + jobId + "_scriptPath",  scriptPath);
            EditorPrefs.SetString(PrefPrefix + jobId + "_submitted",   submitted.ToString());

            // Trigger compilation
            AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceSynchronousImport);

            return ToolResult<RunBlockSubmitResult>.Ok(new RunBlockSubmitResult
            {
                JobId   = jobId,
                Status  = "pending",
                Message = "Script submitted. Unity is compiling (expect 3-10 seconds + domain reload). " +
                          "Call editor/run-block-poll with this JobId to get the result."
            });
        }

        // ── Script builder ────────────────────────────────────────────────────

        private static string BuildScript(string className, string jobId, string code, string[] extraUsings)
        {
            // Pre-compute EditorPrefs keys as string literals (avoids const expression issues)
            string doneKey   = PrefPrefix + jobId + "_done";
            string statusKey = PrefPrefix + jobId + "_status";
            string outputKey = PrefPrefix + jobId + "_output";
            string errorKey  = PrefPrefix + jobId + "_error";

            var sb = new StringBuilder();

            // ── Default usings ────────────────────────────────────────────────
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEditor;");

            if (extraUsings != null)
            {
                foreach (string raw in extraUsings)
                {
                    string ns = raw.Trim();
                    // Strip leading "using " and trailing ";"
                    if (ns.StartsWith("using ", StringComparison.Ordinal)) ns = ns.Substring(6).Trim();
                    ns = ns.TrimEnd(';').Trim();
                    if (!string.IsNullOrEmpty(ns))
                        sb.AppendLine("using " + ns + ";");
                }
            }

            sb.AppendLine();

            // ── Class shell ───────────────────────────────────────────────────
            sb.AppendLine("[InitializeOnLoad]");
            sb.AppendLine("public static class " + className);
            sb.AppendLine("{");

            // Static constructor: only schedule once
            sb.AppendLine("    static " + className + "()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (UnityEditor.EditorPrefs.GetBool(\"" + doneKey + "\", false)) return;");
            sb.AppendLine("        UnityEditor.EditorApplication.delayCall += _RunOnce;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // _RunOnce: captures Debug.Log, runs user code, writes result to EditorPrefs
            sb.AppendLine("    private static void _RunOnce()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (UnityEditor.EditorPrefs.GetBool(\"" + doneKey + "\", false)) return;");
            sb.AppendLine("        UnityEditor.EditorPrefs.SetBool(\"" + doneKey + "\", true);");
            sb.AppendLine();
            sb.AppendLine("        var _logs = new System.Collections.Generic.List<string>();");
            sb.AppendLine("        UnityEngine.Application.LogCallback _handler =");
            sb.AppendLine("            (msg, trace, type) => _logs.Add(\"[\" + type + \"] \" + msg);");
            sb.AppendLine("        UnityEngine.Application.logMessageReceived += _handler;");
            sb.AppendLine();
            sb.AppendLine("        try");
            sb.AppendLine("        {");

            // ── User code (indented) ──────────────────────────────────────────
            foreach (string line in code.Split('\n'))
                sb.AppendLine("            " + line.TrimEnd('\r'));

            sb.AppendLine();
            sb.AppendLine("            UnityEngine.Application.logMessageReceived -= _handler;");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + statusKey + "\", \"done\");");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + outputKey + "\",");
            sb.AppendLine("                string.Join(\"\\n\", _logs));");
            sb.AppendLine("        }");
            sb.AppendLine("        catch (System.Exception _ex)");
            sb.AppendLine("        {");
            sb.AppendLine("            UnityEngine.Application.logMessageReceived -= _handler;");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + statusKey + "\", \"error\");");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + errorKey + "\",");
            sb.AppendLine("                _ex.GetType().Name + \": \" + _ex.Message);");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + outputKey + "\",");
            sb.AppendLine("                string.Join(\"\\n\", _logs));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── EditorPrefs cleanup ───────────────────────────────────────────────

        internal static void ClearJobPrefs(string jobId)
        {
            string p = PrefPrefix + jobId;
            EditorPrefs.DeleteKey(p + "_done");
            EditorPrefs.DeleteKey(p + "_status");
            EditorPrefs.DeleteKey(p + "_output");
            EditorPrefs.DeleteKey(p + "_error");
            EditorPrefs.DeleteKey(p + "_scriptPath");
            EditorPrefs.DeleteKey(p + "_submitted");
        }
    }

    // ── Poll ──────────────────────────────────────────────────────────────────

    public static class EditorRunBlockPollTool
    {
        private const string PrefPrefix = "MosaicBridgeRunBlock_";

        // After this many seconds with no result and no active compilation → assume compile error
        private const int CompileErrorTimeoutSeconds = 20;

        [MosaicTool("editor/run-block-poll",
                    "Polls for the result of a previously submitted editor/run-block job. " +
                    "Pass the JobId returned by editor/run-block. " +
                    "Status: 'compiling' — still compiling, wait 3s and retry. " +
                    "Status: 'pending' — compilation done but code not yet executed, retry in 1s. " +
                    "Status: 'done' — code ran successfully; Output contains any Debug.Log lines. " +
                    "Status: 'error' — runtime exception (see Error) or compile failure (check console/get-errors). " +
                    "Cleans up the temp script automatically once result is read (causes one more domain reload).",
                    isReadOnly: true)]
        public static ToolResult<RunBlockPollResult> Poll(RunBlockPollParams p)
        {
            if (string.IsNullOrWhiteSpace(p.JobId))
                return ToolResult<RunBlockPollResult>.Fail(
                    "JobId is required.", ErrorCodes.INVALID_PARAM);

            string jobId = p.JobId.Trim();
            string prefBase = PrefPrefix + jobId;

            bool isDone = EditorPrefs.GetBool(prefBase + "_done", false);

            if (isDone)
            {
                string status = EditorPrefs.GetString(prefBase + "_status", "unknown");
                string output = EditorPrefs.GetString(prefBase + "_output", "");
                string error  = EditorPrefs.GetString(prefBase + "_error",  "");
                string script = EditorPrefs.GetString(prefBase + "_scriptPath", "");

                DeleteTempScript(jobId, script);

                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = status,
                    Output  = string.IsNullOrEmpty(output) ? null : output,
                    Error   = string.IsNullOrEmpty(error)  ? null : error,
                    Message = status == "done"
                        ? "Code executed successfully."
                        : "Execution failed: " + error
                });
            }

            // ── Not done yet ─────────────────────────────────────────────────

            if (EditorApplication.isCompiling)
                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = "compiling",
                    Message = "Unity is still compiling. Try again in 3 seconds."
                });

            // Compilation finished but _done not set yet.
            // Either: (a) domain-reload just happened, delayCall hasn't fired yet
            //      or (b) compile errors prevented [InitializeOnLoad] from running.
            // Distinguish using submission timestamp.
            string submittedStr = EditorPrefs.GetString(prefBase + "_submitted", "0");
            long submitted = long.TryParse(submittedStr, out long ts) ? ts : 0;
            long elapsed   = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - submitted;

            if (elapsed < CompileErrorTimeoutSeconds)
                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = "pending",
                    Message = "Compilation done, code execution pending (domain reload just completed). " +
                              "Try again in 2 seconds."
                });

            // Timed out — compile errors most likely.
            //
            // Harvest the errors BEFORE deleting the script. The temp file has to go (a broken
            // script in Assets/Editor keeps the whole project from compiling), but deleting it and
            // then saying "call console/get-errors" sent the caller after a file that no longer
            // exists: the console names Assets/Editor/MosaicBridge_RunBlock_<id>.cs, and by the
            // time they look, the bridge has removed it. Two round trips to learn something this
            // call already had in hand.
            string scriptPath = EditorPrefs.GetString(prefBase + "_scriptPath", "");
            string compileErrors = CollectCompileErrors(jobId);
            DeleteTempScript(jobId, scriptPath);

            // Two different failures wear the same timeout, and they have different fixes.
            //
            // Reporting both as "the script did not compile" sent people to console/get-errors for
            // errors that did not exist — after a poll had just said "compilation done, execution
            // pending", which is the opposite claim. The presence of compile errors for THIS job's
            // script is the discriminator, and this method already has it in hand.
            bool didNotCompile = !string.IsNullOrEmpty(compileErrors);

            return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
            {
                JobId   = jobId,
                Status  = "error",
                Error   = didNotCompile ? compileErrors : null,
                Message = didNotCompile
                    ? $"Job timed out after {elapsed}s — the script did not compile. The errors are "
                      + "in Error, below. Fix the code and resubmit. NOTE: the temp script has been "
                      + "deleted, so the file the console names no longer exists — the line numbers "
                      + "refer to the block you submitted, offset by the generated header."
                    : $"Job timed out after {elapsed}s — the script COMPILED, but the block never "
                      + "ran. No compile errors were logged against it. The generated class is "
                      + "[InitializeOnLoad] and schedules itself via delayCall, so this means the "
                      + "domain reload did not deliver that callback — typically another reload, a "
                      + "play-mode change or a second compile landing on top of it. Do NOT go "
                      + "looking for compile errors; there are none. Resubmit once the Editor is "
                      + "idle."
            });
        }

        /// <summary>
        /// Compile errors from this job's generated script, newest first, or "" if none were found.
        /// </summary>
        /// <remarks>
        /// Filtered to the job's own file so an unrelated pre-existing error in the project is not
        /// reported as the cause of this failure — the tool already warns not to submit a block
        /// into a project that does not compile, and blaming a stale error would make that warning
        /// harder to act on rather than easier.
        /// </remarks>
        private static string CollectCompileErrors(string jobId)
        {
            try
            {
                var entries = Mosaic.Bridge.Tools.ConsoleTools.ConsoleLogBuffer.GetEntries(
                    includeInfo: false, includeWarnings: false, includeErrors: true, maxResults: 50);
                var sb = new StringBuilder();
                foreach (var e in entries)
                {
                    string where = e.File ?? "";
                    string what  = e.Message ?? "";
                    if (where.IndexOf(jobId, StringComparison.Ordinal) < 0 &&
                        what.IndexOf(jobId, StringComparison.Ordinal) < 0)
                        continue;
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(what.Trim());
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                // Never let diagnostics turn a compile failure into a tool failure.
                return "";
            }
        }

        private static void DeleteTempScript(string jobId, string scriptPath)
        {
            EditorRunBlockTool.ClearJobPrefs(jobId);

            if (string.IsNullOrEmpty(scriptPath)) return;

            string full = Path.GetFullPath(scriptPath);
            if (!File.Exists(full)) return;

            try
            {
                AssetDatabase.DeleteAsset(scriptPath);
            }
            catch
            {
                // best-effort: if delete fails, leave the file; it's harmless after _done is cleared
            }
        }
    }

    // ── Params & Results ──────────────────────────────────────────────────────

    public sealed class RunBlockParams
    {
        [Required] public string   Code   { get; set; }
        public           string[]  Usings { get; set; }
    }

    public sealed class RunBlockPollParams
    {
        [Required] public string JobId { get; set; }
    }

    public sealed class RunBlockSubmitResult
    {
        public string JobId   { get; set; }
        public string Status  { get; set; }
        public string Message { get; set; }
    }

    public sealed class RunBlockPollResult
    {
        public string JobId   { get; set; }
        public string Status  { get; set; }
        public string Output  { get; set; }
        public string Error   { get; set; }
        public string Message { get; set; }
    }
}
