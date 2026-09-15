#if MOSAIC_HAS_TIMELINE
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Scenes;
using Mosaic.Bridge.Tools.UI;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineSignalTool
    {
        [MosaicTool("timeline/signal",
                    "create-asset: creates a SignalAsset at AssetPath. add-emitter: adds a " +
                    "SignalEmitter marker at Time on TrackIndex (or the timeline's own global marker " +
                    "track when omitted), emitting SignalAssetPath. add-receiver: adds a " +
                    "SignalReceiver to ReceiverInstanceId/Path and registers a PERSISTENT reaction " +
                    "(same contract as ui/add_listener: TargetComponentType/MethodName/*Arg) for " +
                    "SignalAssetPath — the sanctioned path for 'fire gameplay event at t=3.2s', since " +
                    "an unwired Signal track is otherwise inert.",
                    isReadOnly: false)]
        public static ToolResult<TimelineSignalResult> Execute(TimelineSignalParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create-asset":  return CreateAsset(p);
                case "add-emitter":   return AddEmitter(p);
                case "add-receiver":  return AddReceiver(p);
                default:
                    return ToolResult<TimelineSignalResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: create-asset, add-emitter, add-receiver",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TimelineSignalResult> CreateAsset(TimelineSignalParams p)
        {
            if (string.IsNullOrEmpty(p.AssetPath))
                return ToolResult<TimelineSignalResult>.Fail("AssetPath is required", ErrorCodes.INVALID_PARAM);

            var signal = ScriptableObject.CreateInstance<SignalAsset>();
            var dir = System.IO.Path.GetDirectoryName(p.AssetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(signal, p.AssetPath);
            AssetDatabase.SaveAssets();

            return ToolResult<TimelineSignalResult>.Ok(new TimelineSignalResult
            {
                Action = "create-asset", AssetPath = p.AssetPath,
            });
        }

        private static ToolResult<TimelineSignalResult> AddEmitter(TimelineSignalParams p)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.TimelineAssetPath);
            if (timeline == null)
                return ToolResult<TimelineSignalResult>.Fail(
                    $"TimelineAsset not found at '{p.TimelineAssetPath}'", ErrorCodes.NOT_FOUND);

            var signalAsset = AssetDatabase.LoadAssetAtPath<SignalAsset>(p.SignalAssetPath);
            if (signalAsset == null)
                return ToolResult<TimelineSignalResult>.Fail(
                    $"SignalAsset not found at '{p.SignalAssetPath}'", ErrorCodes.NOT_FOUND);

            TrackAsset track;
            int resultTrackIndex;
            if (p.TrackIndex.HasValue)
            {
                var tracks = timeline.GetOutputTracks().ToList();
                if (p.TrackIndex.Value < 0 || p.TrackIndex.Value >= tracks.Count)
                    return ToolResult<TimelineSignalResult>.Fail(
                        $"TrackIndex {p.TrackIndex.Value} is out of range (0..{tracks.Count - 1})",
                        ErrorCodes.OUT_OF_RANGE);
                track = tracks[p.TrackIndex.Value];
                resultTrackIndex = p.TrackIndex.Value;
            }
            else
            {
                if (timeline.markerTrack == null)
                    timeline.CreateMarkerTrack();
                track = timeline.markerTrack;
                resultTrackIndex = -1;
            }

            var emitter = track.CreateMarker<SignalEmitter>(p.Time);
            emitter.asset = signalAsset;
            emitter.retroactive = p.Retroactive ?? false;
            emitter.emitOnce = p.EmitOnce ?? false;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return ToolResult<TimelineSignalResult>.Ok(new TimelineSignalResult
            {
                Action = "add-emitter",
                TrackIndex = resultTrackIndex,
                Time = emitter.time,
                Retroactive = emitter.retroactive,
                EmitOnce = emitter.emitOnce,
            });
        }

        private static ToolResult<TimelineSignalResult> AddReceiver(TimelineSignalParams p)
        {
            if (!GameObjectResolver.TryResolve(p.ReceiverInstanceId, p.ReceiverPath, out var receiverGo, out var receiverError))
                return ToolResult<TimelineSignalResult>.Fail(receiverError, ErrorCodes.NOT_FOUND);

            var signalAsset = AssetDatabase.LoadAssetAtPath<SignalAsset>(p.SignalAssetPath);
            if (signalAsset == null)
                return ToolResult<TimelineSignalResult>.Fail(
                    $"SignalAsset not found at '{p.SignalAssetPath}'", ErrorCodes.NOT_FOUND);

            if (!GameObjectResolver.TryResolve(p.TargetInstanceId, p.TargetPath, out var targetGo, out var targetError))
                return ToolResult<TimelineSignalResult>.Fail(targetError, ErrorCodes.NOT_FOUND);

            var targetComponentType = UIToolHelpers.ResolveComponentType(p.TargetComponentType);
            if (targetComponentType == null)
                return ToolResult<TimelineSignalResult>.Fail(
                    $"Component type '{p.TargetComponentType}' not found", ErrorCodes.NOT_FOUND);
            var targetComponent = targetGo.GetComponent(targetComponentType);
            if (targetComponent == null)
                return ToolResult<TimelineSignalResult>.Fail(
                    $"Component '{p.TargetComponentType}' not found on '{targetGo.name}'", ErrorCodes.NOT_FOUND);

            if (!UIEventListenerHelpers.TryResolveMethod(targetComponent, p.MethodName, out var method, out var argType, out var methodError))
                return ToolResult<TimelineSignalResult>.Fail(methodError, ErrorCodes.NOT_FOUND);

            if (!UIEventListenerHelpers.TryParseCallState(p.CallState, out var callState))
                return ToolResult<TimelineSignalResult>.Fail(
                    $"Invalid CallState '{p.CallState}'. Valid: RuntimeOnly, EditorAndRuntime, Off", ErrorCodes.INVALID_PARAM);

            var receiver = receiverGo.GetComponent<SignalReceiver>();
            if (receiver == null)
                receiver = receiverGo.AddComponent<SignalReceiver>();

            if (receiver.GetReaction(signalAsset) != null)
                return ToolResult<TimelineSignalResult>.Fail(
                    $"'{receiverGo.name}' already has a reaction registered for '{p.SignalAssetPath}' " +
                    "— remove it first (this tool does not yet edit an existing reaction).",
                    ErrorCodes.CONFLICT);

            var reaction = new UnityEvent();
            if (!UIEventListenerHelpers.TryAddPersistentListener(
                    reaction, targetComponent, method, argType,
                    p.FloatArg, p.IntArg, p.BoolArg, p.StringArg, p.ObjectArg,
                    callState, out var index, out var addError))
                return ToolResult<TimelineSignalResult>.Fail(addError, ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(receiver, "Mosaic: Add Signal Reaction");
            receiver.AddReaction(signalAsset, reaction);
            EditorUtility.SetDirty(receiver);

            return ToolResult<TimelineSignalResult>.Ok(new TimelineSignalResult
            {
                Action = "add-receiver",
                ReceiverName = receiverGo.name,
                ListenerIndex = index,
                CallState = callState.ToString(),
            });
        }
    }
}
#endif
