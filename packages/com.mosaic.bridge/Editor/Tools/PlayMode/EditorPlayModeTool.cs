using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.PlayMode
{
    /// <summary>
    /// Play mode, including the part Unity does not do for an Editor nobody is looking at.
    /// </summary>
    /// <remarks>
    /// An unfocused Unity Editor does not advance the player loop. Play mode entered over the
    /// bridge sits at frame 1 with timeSinceLevelLoad 0.00 indefinitely: no physics, no NavMesh,
    /// no particles, no animation. A twelve-hour autonomous build lost more time to this than to
    /// anything else, because a patrol agent, a particle system and a HUD all looked broken while
    /// every one of them was configured correctly, and several non-bugs were "fixed" before the
    /// cause was found. PlayerSettings.runInBackground does not help: it governs built players.
    ///
    /// EditorApplication.update keeps firing while unfocused, so the loop can be driven from
    /// there, one QueuePlayerLoopUpdate per tick. PumpSeconds starts that and returns straight
    /// away, because the pump needs the editor tick this call would otherwise be blocking.
    /// FrameCount and TimeSinceLevelLoad are on every result so a caller can see whether time is
    /// passing rather than inferring it.
    /// </remarks>
    public static class EditorPlayModeTool
    {
        private static readonly string[] ValidActions = { "play", "pause", "stop", "step", "status" };

        private static double _pumpUntil;
        private static bool _hooked;

        [MosaicTool("editor/play-mode",
                    "Controls Unity play mode: play, pause, stop, step, or status. " +
                    "An UNFOCUSED Editor does not advance the player loop, so play mode alone sits at " +
                    "frame 1 forever and correct scenes look broken. Pass PumpSeconds to drive the loop " +
                    "for that many seconds of game time; the call returns immediately, then poll with " +
                    "action 'status' and watch FrameCount and TimeSinceLevelLoad move. " +
                    "Play-mode transitions are asynchronous, so State reflects the pre-transition value " +
                    "immediately after 'play'; call 'status' to see the settled state.",
                    isReadOnly: false)]
        public static ToolResult<EditorPlayModeResult> Execute(EditorPlayModeParams p)
        {
            var action = p.Action?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(action) ||
                System.Array.IndexOf(ValidActions, action) < 0)
            {
                return ToolResult<EditorPlayModeResult>.Fail(
                    $"Invalid action '{p.Action}'. Valid actions: play, pause, stop, step, status",
                    ErrorCodes.INVALID_PARAM);
            }

            switch (action)
            {
                case "play":
                    EditorApplication.isPlaying = true;
                    break;

                case "stop":
                    EditorApplication.isPlaying = false;
                    StopPump();
                    break;

                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    break;

                case "step":
                    EditorApplication.Step();
                    break;

                case "status":
                    break;
            }

            if (p.PumpSeconds > 0f)
            {
                if (action == "stop")
                {
                    return ToolResult<EditorPlayModeResult>.Fail(
                        "PumpSeconds cannot be combined with 'stop': there would be nothing to drive.",
                        ErrorCodes.INVALID_PARAM);
                }
                StartPump(p.PumpSeconds);
            }

            return ToolResult<EditorPlayModeResult>.Ok(Snapshot(action));
        }

        private static EditorPlayModeResult Snapshot(string action)
        {
            var pumping = _pumpUntil > EditorApplication.timeSinceStartup;
            return new EditorPlayModeResult
            {
                RequestedAction      = action,
                IsPlaying            = EditorApplication.isPlaying,
                IsPaused             = EditorApplication.isPaused,
                State                = GetStateString(),
                FrameCount           = Time.frameCount,
                TimeSinceLevelLoad   = Time.timeSinceLevelLoad,
                Pumping              = pumping,
                PumpSecondsRemaining = pumping ? (float)(_pumpUntil - EditorApplication.timeSinceStartup) : 0f,
                Note                 = EditorApplicationIsFocused()
                    ? null
                    : "This Editor is not the focused window, so it does not advance the player loop on its own. " +
                      "Pass PumpSeconds to drive it."
            };
        }

        /// <summary>Whether a human is looking at this Editor. There is no direct API; an
        /// unfocused Editor is the normal state for a bridge-driven one, so this only ever
        /// softens the note above.</summary>
        private static bool EditorApplicationIsFocused()
        {
            return UnityEditorInternal.InternalEditorUtility.isApplicationActive;
        }

        private static void StartPump(float seconds)
        {
            _pumpUntil = EditorApplication.timeSinceStartup + Mathf.Clamp(seconds, 0f, 600f);
            if (_hooked) return;
            EditorApplication.update += Pump;
            _hooked = true;
        }

        private static void StopPump()
        {
            _pumpUntil = 0;
            if (!_hooked) return;
            EditorApplication.update -= Pump;
            _hooked = false;
        }

        private static void Pump()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup >= _pumpUntil)
            {
                StopPump();
                return;
            }
            // The whole fix, one line: an unfocused Editor still ticks update, and this is
            // what turns a tick into a frame of the game.
            EditorApplication.QueuePlayerLoopUpdate();
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
