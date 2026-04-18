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
    }
}
