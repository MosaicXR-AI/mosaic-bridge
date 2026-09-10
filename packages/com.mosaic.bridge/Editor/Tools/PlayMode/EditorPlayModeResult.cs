namespace Mosaic.Bridge.Tools.PlayMode
{
    public sealed class EditorPlayModeResult
    {
        /// <summary>The action that was requested.</summary>
        public string RequestedAction { get; set; }

        /// <summary>True if the editor is currently in play mode.</summary>
        public bool IsPlaying { get; set; }

        /// <summary>True if the editor is currently paused.</summary>
        public bool IsPaused { get; set; }

        /// <summary>Human-readable play mode state: "Playing", "Paused", "Stopped".</summary>
        public string State { get; set; }

        /// <summary>Frames the player loop has actually run. If this does not move between
        /// two calls while State is "Playing", no game time is passing — see Pumping.</summary>
        public int FrameCount { get; set; }

        /// <summary>Seconds of game time since the scene loaded. The companion to FrameCount:
        /// a caller can tell at a glance whether the game is running or merely "Playing".</summary>
        public float TimeSinceLevelLoad { get; set; }

        /// <summary>True while this tool is driving the player loop for an unfocused Editor.</summary>
        public bool Pumping { get; set; }

        /// <summary>Seconds of pumping still to run, if any.</summary>
        public float PumpSecondsRemaining { get; set; }

        /// <summary>Says so when the Editor window is not focused, which is the normal state
        /// for a bridge-driven Editor and the reason pumping exists.</summary>
        public string Note { get; set; }
    }
}
