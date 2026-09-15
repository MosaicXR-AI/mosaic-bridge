#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineSignalParams
    {
        /// <summary>Action: create-asset, add-emitter, add-receiver.</summary>
        [Required] public string Action { get; set; }

        // -- create-asset --

        /// <summary>Asset path for the new SignalAsset (e.g. "Assets/Signals/GameOver.signal").</summary>
        public string AssetPath { get; set; }

        // -- add-emitter --

        public string TimelineAssetPath { get; set; }

        /// <summary>Track to host the marker. Omit to use (creating if needed) the TimelineAsset's
        /// own global marker track.</summary>
        public int? TrackIndex { get; set; }

        /// <summary>Existing SignalAsset to emit (from create-asset).</summary>
        public string SignalAssetPath { get; set; }

        public double Time { get; set; }
        public bool? Retroactive { get; set; }
        public bool? EmitOnce { get; set; }

        // -- add-receiver --

        /// <summary>GameObject to add (or reuse) a SignalReceiver component on.</summary>
        public int? ReceiverInstanceId { get; set; }
        public string ReceiverPath { get; set; }

        /// <summary>The GameObject/Component carrying the method the reaction calls.</summary>
        public int? TargetInstanceId { get; set; }
        public string TargetPath { get; set; }

        /// <summary>Component type on the target carrying the method.</summary>
        public string TargetComponentType { get; set; }

        /// <summary>Public method name, 0 or 1 parameters (float, int, bool, string, or an
        /// Object-derived type) — same contract as ui/add_listener.</summary>
        public string MethodName { get; set; }

        public float? FloatArg { get; set; }
        public int? IntArg { get; set; }
        public bool? BoolArg { get; set; }
        public string StringArg { get; set; }
        public string ObjectArg { get; set; }

        /// <summary>"RuntimeOnly" (default), "EditorAndRuntime", or "Off".</summary>
        public string CallState { get; set; }
    }
}
#endif
