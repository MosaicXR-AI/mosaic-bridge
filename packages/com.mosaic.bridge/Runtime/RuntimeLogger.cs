using System;
using UnityEngine;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Lightweight logger for the runtime bridge. Logs to Unity's Debug.Log
    /// with a [Mosaic.Runtime] prefix. Mirrors the IMosaicLogger contract from
    /// the editor assembly without taking a dependency on it.
    /// </summary>
    public sealed class RuntimeLogger
    {
        public enum LogLevel { Trace = 0, Debug = 1, Info = 2, Warn = 3, Error = 4, None = 5 }

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

        public void Trace(string message)
        {
            if (IsEnabled(LogLevel.Trace))
                UnityEngine.Debug.Log($"[Mosaic.Runtime] TRACE: {message}");
        }

        public void Info(string message)
        {
            if (IsEnabled(LogLevel.Info))
                UnityEngine.Debug.Log($"[Mosaic.Runtime] {message}");
        }

        public void Warn(string message)
        {
            if (IsEnabled(LogLevel.Warn))
                UnityEngine.Debug.LogWarning($"[Mosaic.Runtime] {message}");
        }

        public void Error(string message, Exception exception = null)
        {
            if (IsEnabled(LogLevel.Error))
            {
                if (exception != null)
                    UnityEngine.Debug.LogError($"[Mosaic.Runtime] {message}\n{exception}");
                else
                    UnityEngine.Debug.LogError($"[Mosaic.Runtime] {message}");
            }
        }
    }
}
