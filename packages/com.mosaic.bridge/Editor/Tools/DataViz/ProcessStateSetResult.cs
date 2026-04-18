namespace Mosaic.Bridge.Tools.DataViz
{
    public sealed class ProcessStateSetResult
    {
        /// <summary>Name of the target GameObject.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Resolved (lower-case) state that was applied.</summary>
        public string State { get; set; }

        /// <summary>Previous state (if any) recorded for the GameObject.</summary>
        public string PreviousState { get; set; }

        /// <summary>Number of renderers/objects affected by the operation.</summary>
        public int AffectedCount { get; set; }

        /// <summary>Resolved (lower-case) display mode.</summary>
        public string DisplayMode { get; set; }
    }
}
