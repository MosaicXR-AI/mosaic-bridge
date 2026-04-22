using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.PlayMode
{
    public static class EditorPlayModeTool
    {
        private static readonly string[] ValidActions = { "play", "pause", "stop", "step" };

        [MosaicTool("editor/play-mode",
                    "Controls Unity play mode: play, pause, stop, or step one frame. " +
                    "IMPORTANT: Play-mode transitions are asynchronous — the response State reflects the " +
                    "pre-transition value (e.g., 'Stopped' immediately after calling 'play'). " +
                    "Call editor/execute-code with 'UnityEditor.EditorApplication.isPlaying' after a " +
                    "short delay to verify the new state.",
                    isReadOnly: false)]
        public static ToolResult<EditorPlayModeResult> Execute(EditorPlayModeParams p)
        {
            var action = p.Action?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(action) ||
                System.Array.IndexOf(ValidActions, action) < 0)
            {
                return ToolResult<EditorPlayModeResult>.Fail(
                    $"Invalid action '{p.Action}'. Valid actions: play, pause, stop, step",
                    ErrorCodes.INVALID_PARAM);
            }

            switch (action)
            {
                case "play":
                    EditorApplication.isPlaying = true;
                    break;

                case "stop":
                    EditorApplication.isPlaying = false;
                    break;

                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    break;

                case "step":
                    EditorApplication.Step();
                    break;
            }

            return ToolResult<EditorPlayModeResult>.Ok(new EditorPlayModeResult
            {
                RequestedAction = action,
                IsPlaying       = EditorApplication.isPlaying,
                IsPaused        = EditorApplication.isPaused,
                State           = GetStateString()
            });
        }

        private static string GetStateString()
        {
            if (!EditorApplication.isPlaying)
                return "Stopped";
            if (EditorApplication.isPaused)
                return "Paused";
            return "Playing";
        }
    }
}
