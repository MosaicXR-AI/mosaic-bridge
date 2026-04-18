using System;
using Mosaic.Bridge.Contracts.Interfaces;

namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// Decorator around <see cref="IMosaicLogger"/> that runs <see cref="LogRedactor.Redact"/>
    /// on all messages and context values before forwarding to the inner logger.
    /// Story 8.4 — Allowlist-Based Log Redaction.
    /// </summary>
    public sealed class RedactingLogger : IMosaicLogger
    {
        private readonly IMosaicLogger _inner;

        public RedactingLogger(IMosaicLogger inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public LogLevel MinimumLevel
        {
            get => _inner.MinimumLevel;
            set => _inner.MinimumLevel = value;
        }

        public bool IsEnabled(LogLevel level) => _inner.IsEnabled(level);

        public void Trace(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Trace)) return;
            _inner.Trace(LogRedactor.Redact(message), RedactContext(context));
        }

        public void Debug(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Debug)) return;
            _inner.Debug(LogRedactor.Redact(message), RedactContext(context));
        }

        public void Info(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Info)) return;
            _inner.Info(LogRedactor.Redact(message), RedactContext(context));
        }

        public void Warn(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Warn)) return;
            _inner.Warn(LogRedactor.Redact(message), RedactContext(context));
        }

        public void Error(string message, Exception exception = null, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Error)) return;
            _inner.Error(LogRedactor.Redact(message), exception, RedactContext(context));
        }

        private static (string Key, object Value)[] RedactContext((string Key, object Value)[] context)
        {
            if (context == null || context.Length == 0)
                return context;

            var redacted = new (string Key, object Value)[context.Length];
            for (int i = 0; i < context.Length; i++)
            {
                var (key, value) = context[i];
                if (LogRedactor.IsSafeField(key))
                {
                    redacted[i] = (key, value);
                }
                else
                {
                    redacted[i] = (key, value is string s ? (object)LogRedactor.Redact(s) : value);
                }
            }
            return redacted;
        }
    }
}
