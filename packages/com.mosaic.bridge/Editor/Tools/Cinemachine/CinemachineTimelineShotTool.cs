#if MOSAIC_HAS_CINEMACHINE && MOSAIC_HAS_TIMELINE
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Scenes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineTimelineShotTool
    {
        [MosaicTool("cinemachine/timeline-shot",
                    "Adds a shot clip to a CinemachineTrack (from timeline/add-track TrackType=" +
                    "Cinemachine) and wires its VirtualCamera ExposedReference to VCamName via " +
                    "PlayableDirector.SetReferenceValue — the cinematics course's core deliverable, a " +
                    "shot list. A CinemachineBrain must exist in the scene (cinemachine/create-brain) " +
                    "for cuts between shots to actually blend.",
                    isReadOnly: false)]
        public static ToolResult<CinemachineTimelineShotResult> Execute(CinemachineTimelineShotParams p)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.TimelineAssetPath);
            if (timeline == null)
                return ToolResult<CinemachineTimelineShotResult>.Fail(
                    $"TimelineAsset not found at '{p.TimelineAssetPath}'", ErrorCodes.NOT_FOUND);

            var tracks = timeline.GetOutputTracks().ToList();
            if (p.TrackIndex < 0 || p.TrackIndex >= tracks.Count)
                return ToolResult<CinemachineTimelineShotResult>.Fail(
                    $"TrackIndex {p.TrackIndex} is out of range (0..{tracks.Count - 1})", ErrorCodes.OUT_OF_RANGE);

            if (!(tracks[p.TrackIndex] is CinemachineTrack track))
                return ToolResult<CinemachineTimelineShotResult>.Fail(
                    $"Track {p.TrackIndex} is a {tracks[p.TrackIndex].GetType().Name}, not a CinemachineTrack " +
                    "— create one first with timeline/add-track TrackType=Cinemachine.", ErrorCodes.INVALID_PARAM);

            if (!GameObjectResolver.TryResolve(p.DirectorInstanceId, p.DirectorPath, out var directorGo, out var directorError))
                return ToolResult<CinemachineTimelineShotResult>.Fail(directorError, ErrorCodes.NOT_FOUND);
            var director = directorGo.GetComponent<PlayableDirector>();
            if (director == null)
                return ToolResult<CinemachineTimelineShotResult>.Fail(
                    $"No PlayableDirector component on '{directorGo.name}'", ErrorCodes.NOT_FOUND);

            var vcamGo = GameObject.Find(p.VCamName);
            if (vcamGo == null)
                return ToolResult<CinemachineTimelineShotResult>.Fail(
                    $"GameObject '{p.VCamName}' not found", ErrorCodes.NOT_FOUND);
            var vcam = vcamGo.GetComponent<CinemachineVirtualCameraBase>();
            if (vcam == null)
                return ToolResult<CinemachineTimelineShotResult>.Fail(
                    $"'{p.VCamName}' has no Cinemachine virtual camera component", ErrorCodes.INVALID_PARAM);

            var clip = track.CreateDefaultClip();
            clip.start = p.Start;
            clip.duration = p.Duration > 0 ? p.Duration : 2.0;
            var shot = (CinemachineShot)clip.asset;

            Undo.RecordObject(director, "Mosaic: Add Cinemachine Timeline Shot");
            director.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return ToolResult<CinemachineTimelineShotResult>.Ok(new CinemachineTimelineShotResult
            {
                TrackIndex = p.TrackIndex,
                VCamName = vcamGo.name,
                Start = clip.start,
                Duration = clip.duration,
                BrainFoundInScene = UnityEngine.Object.FindFirstObjectByType<CinemachineBrain>() != null,
            });
        }
    }
}
#endif
