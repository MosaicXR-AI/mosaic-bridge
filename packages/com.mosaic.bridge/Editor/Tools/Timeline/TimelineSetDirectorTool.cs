#if MOSAIC_HAS_TIMELINE
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineSetDirectorTool
    {
        [MosaicTool("timeline/set-director",
                    "Configures a PlayableDirector on a GameObject with the specified TimelineAsset",
                    isReadOnly: false)]
        public static ToolResult<TimelineSetDirectorResult> SetDirector(TimelineSetDirectorParams p)
        {
            // Resolve the target GameObject
            GameObject go = null;
            if (p.InstanceId != 0)
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId) as GameObject;
            if (go == null && !string.IsNullOrEmpty(p.Name))
                go = GameObject.Find(p.Name);
            if (go == null)
                return ToolResult<TimelineSetDirectorResult>.Fail(
                    "GameObject not found. Provide a valid InstanceId or Name.",
                    ErrorCodes.NOT_FOUND);

            // Load the timeline asset
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.TimelineAssetPath);
            if (timeline == null)
                return ToolResult<TimelineSetDirectorResult>.Fail(
                    $"TimelineAsset not found at '{p.TimelineAssetPath}'", ErrorCodes.NOT_FOUND);

            // Add or get PlayableDirector component
            var director = go.GetComponent<PlayableDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<PlayableDirector>(go);
            }
            else
            {
                Undo.RecordObject(director, "Mosaic: Set Director Timeline");
            }

            director.playableAsset = timeline;
            EditorUtility.SetDirty(director);

            return ToolResult<TimelineSetDirectorResult>.Ok(new TimelineSetDirectorResult
            {
                InstanceId = go.GetInstanceID(),
                GameObjectName = go.name,
                TimelineAssetPath = p.TimelineAssetPath
            });
        }
    }
}
#endif
