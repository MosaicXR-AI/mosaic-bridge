using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using UnityEditor;
using UnityEditor.Compilation;

namespace Mosaic.Bridge.Tools.Testing
{
    /// <summary>
    /// Whether Unity has finished compiling, and what it said.
    /// </summary>
    /// <remarks>
    /// There was no way to learn that a script had compiled. Menu items silently execute the
    /// PREVIOUSLY compiled assembly, so an edit that has not finished building runs the old code
    /// and reports success. The only workarounds were tailing Editor.log for compiler output, or
    /// polling the ScriptAssemblies timestamp — and Editor.log is shared by every Editor on the
    /// machine, so a second open project fills it with null padding and destroys that channel
    /// mid-session. Across roughly fifty script iterations in one build, at about forty seconds
    /// of guessing each, this was the second largest time sink.
    ///
    /// This route answers directly: is a compile running, did the assemblies move, and what
    /// errors did the last compile produce. Poll it after editor/refresh until Settled is true.
    /// </remarks>
    public static class EditorCompileStatusTool
    {
        [MosaicTool("editor/compile-status",
            "Reports whether Unity is compiling, whether the assemblies have been rebuilt, and the " +
            "errors and warnings from the last compilation. Poll this after editing scripts or calling " +
            "editor/refresh until Settled is true, THEN run anything that depends on the new code: a " +
            "menu item invoked while a compile is pending silently runs the previously compiled assembly.",
            isReadOnly: true)]
        public static ToolResult<EditorCompileStatusResult> Execute()
        {
            var dll = Path.Combine(Directory.GetCurrentDirectory(), "Library", "ScriptAssemblies", "Assembly-CSharp-Editor.dll");
            var stamp = File.Exists(dll) ? File.GetLastWriteTimeUtc(dll) : (DateTime?)null;
            var busy = EditorApplication.isCompiling || EditorApplication.isUpdating;
            var errors = CompileWatcher.Errors;

            return ToolResult<EditorCompileStatusResult>.Ok(new EditorCompileStatusResult
            {
                IsCompiling         = EditorApplication.isCompiling,
                IsUpdatingAssets    = EditorApplication.isUpdating,
                Settled             = !busy,
                ErrorCount          = errors.Count,
                WarningCount        = CompileWatcher.Warnings.Count,
                Errors              = errors.Take(50).ToArray(),
                LastCompileFinished = CompileWatcher.FinishedUtc?.ToString("o"),
                EditorAssemblyWritten = stamp?.ToString("o"),
                AutoRefresh         = EditorPrefs.GetBool("kAutoRefresh", true),
                Note                = busy
                    ? "Still working. Poll again; do not run anything that depends on the new code yet."
                    : errors.Count > 0
                        ? "Compilation finished WITH errors. The assemblies in use are the last ones that built cleanly."
                        : null
            });
        }
    }

    public sealed class EditorCompileStatusResult
    {
        /// <summary>True while Unity is compiling scripts.</summary>
        public bool IsCompiling { get; set; }

        /// <summary>True while the asset database is importing, which precedes compilation.</summary>
        public bool IsUpdatingAssets { get; set; }

        /// <summary>True when neither is running: it is safe to invoke the new code.</summary>
        public bool Settled { get; set; }

        /// <summary>Errors from the most recent compilation.</summary>
        public string[] Errors { get; set; }

        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        /// <summary>When the last compilation finished, ISO 8601 UTC.</summary>
        public string LastCompileFinished { get; set; }

        /// <summary>When Library/ScriptAssemblies/Assembly-CSharp-Editor.dll was last written.
        /// If this has not moved since an edit, the edit has not been built.</summary>
        public string EditorAssemblyWritten { get; set; }

        /// <summary>False means Unity will not compile until something forces it, so waiting
        /// for Settled would wait for ever.</summary>
        public bool AutoRefresh { get; set; }

        public string Note { get; set; }
    }

    /// <summary>Records what each compilation said, because the console does not keep it in a
    /// form a tool can read and Editor.log cannot be trusted with two Editors open.</summary>
    [InitializeOnLoad]
    internal static class CompileWatcher
    {
        internal static readonly List<string> Errors = new List<string>();
        internal static readonly List<string> Warnings = new List<string>();
        internal static DateTime? FinishedUtc;

        static CompileWatcher()
        {
            CompilationPipeline.compilationStarted += OnStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished += OnFinished;
        }

        private static void OnStarted(object _)
        {
            Errors.Clear();
            Warnings.Clear();
        }

        private static void OnAssemblyFinished(string assembly, CompilerMessage[] messages)
        {
            foreach (var m in messages)
            {
                var line = $"{m.file}({m.line},{m.column}): {m.message}";
                if (m.type == CompilerMessageType.Error) Errors.Add(line);
                else Warnings.Add(line);
            }
        }

        private static void OnFinished(object _)
        {
            FinishedUtc = DateTime.UtcNow;
        }
    }
}
