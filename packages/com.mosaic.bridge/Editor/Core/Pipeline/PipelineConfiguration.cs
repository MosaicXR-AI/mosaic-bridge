using System;
using UnityEditor;

namespace Mosaic.Bridge.Core.Pipeline
{
    /// <summary>
    /// Reads pipeline configuration from EditorPrefs.
    /// All settings are per-user, survive domain reloads and Unity restarts.
    /// </summary>
    public sealed class PipelineConfiguration
    {
        private const string PrefixKey = "MosaicBridge.";

        /// <summary>Default execution mode when not specified per-call.</summary>
        public ExecutionMode DefaultMode
        {
            get
            {
                var s = EditorPrefs.GetString(PrefixKey + "DefaultExecutionMode", "direct");
                return ParseMode(s);
            }
            set => EditorPrefs.SetString(PrefixKey + "DefaultExecutionMode", value.ToString().ToLowerInvariant());
        }

        /// <summary>Comma-separated capture angle names (e.g., "front,right,top,perspective").</summary>
        public string CaptureAngles
        {
            get => EditorPrefs.GetString(PrefixKey + "CaptureAngles", "front,right,top,perspective");
            set => EditorPrefs.SetString(PrefixKey + "CaptureAngles", value);
        }

        /// <summary>Screenshot capture resolution in pixels (square). Default 512.</summary>
        public int CaptureResolution
        {
            get => EditorPrefs.GetInt(PrefixKey + "CaptureResolution", 512);
            set => EditorPrefs.SetInt(PrefixKey + "CaptureResolution", value);
        }

        /// <summary>Whether code review stage runs for script tools.</summary>
        public bool CodeReviewEnabled
        {
            get => EditorPrefs.GetBool(PrefixKey + "CodeReviewEnabled", true);
            set => EditorPrefs.SetBool(PrefixKey + "CodeReviewEnabled", value);
        }

        /// <summary>Whether to run Unity Test Runner after script compilation.</summary>
        public bool CodeReviewRunTests
        {
            get => EditorPrefs.GetBool(PrefixKey + "CodeReviewRunTests", false);
            set => EditorPrefs.SetBool(PrefixKey + "CodeReviewRunTests", value);
        }

        public static ExecutionMode ParseMode(string value)
        {
            if (string.IsNullOrEmpty(value)) return ExecutionMode.Direct;

            switch (value.ToLowerInvariant())
            {
                case "direct": return ExecutionMode.Direct;
                case "validated": return ExecutionMode.Validated;
                case "verified": return ExecutionMode.Verified;
                case "reviewed": return ExecutionMode.Reviewed;
                default: return ExecutionMode.Direct;
            }
        }
    }
}
