using System;
using Mosaic.Bridge.Contracts.Interfaces;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Multiplexes log calls to multiple <see cref="IMosaicLogger"/> instances.
    /// Used to fan out to both the Unity console logger and the file logger.
    /// </summary>
    public sealed class CompositeLogger : IMosaicLogger
    {
        private readonly IMosaicLogger[] _loggers;

        public CompositeLogger(params IMosaicLogger[] loggers)
        {
            _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
        }

        public LogLevel MinimumLevel
        {
            get
            {
                // Return the lowest (most verbose) level among all loggers
                var min = LogLevel.None;
                foreach (var logger in _loggers)
                {
                    if (logger.MinimumLevel < min)
                        min = logger.MinimumLevel;
                }
                return min;
            }
            set
            {
                foreach (var logger in _loggers)
                    logger.MinimumLevel = value;
            }
        }

        public bool IsEnabled(LogLevel level)
        {
            foreach (var logger in _loggers)
            {
                if (logger.IsEnabled(level))
                    return true;
            }
            return false;
        }

        public void Trace(string message, params (string Key, object Value)[] context)
        {
            foreach (var logger in _loggers)
                logger.Trace(message, context);
        }

        public void Debug(string message, params (string Key, object Value)[] context)
        {
            foreach (var logger in _loggers)
                logger.Debug(message, context);
        }

        public void Info(string message, params (string Key, object Value)[] context)
        {
            foreach (var logger in _loggers)
                logger.Info(message, context);
        }

        public void Warn(string message, params (string Key, object Value)[] context)
        {
            foreach (var logger in _loggers)
                logger.Warn(message, context);
        }

        public void Error(string message, Exception exception = null, params (string Key, object Value)[] context)
        {
            foreach (var logger in _loggers)
                logger.Error(message, exception, context);
        }
    }
}
