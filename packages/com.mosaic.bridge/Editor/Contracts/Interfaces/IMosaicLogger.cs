using System;

namespace Mosaic.Bridge.Contracts.Interfaces
{
    /// <summary>
    /// Logging abstraction for all Mosaic Bridge components.
    /// Per the implementation patterns: NEVER use Debug.Log directly inside Mosaic.Bridge.Core
    /// or Mosaic.Bridge.Tools — always go through IMosaicLogger so allowlist redaction (NFR38) applies.
    /// </summary>
    public interface IMosaicLogger
    {
        /// <summary>The current minimum log level. Messages below this level are dropped.</summary>
        LogLevel MinimumLevel { get; set; }

        void Trace(string message, params (string Key, object Value)[] context);
        void Debug(string message, params (string Key, object Value)[] context);
        void Info(string message, params (string Key, object Value)[] context);
        void Warn(string message, params (string Key, object Value)[] context);
        void Error(string message, Exception exception = null, params (string Key, object Value)[] context);

        /// <summary>True if a message at the given level would be written.</summary>
        bool IsEnabled(LogLevel level);
    }

    /// <summary>
    /// Log severity levels in increasing order of importance.
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        None = 5
    }
}
