using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationClipTool
    {
        private const string ValidActions = "create, info, set-curve, add-event";

        [MosaicTool("animation/clip",
                    "Manages AnimationClip assets: create, inspect, set curves, add events",
                    isReadOnly: false)]
        public static ToolResult<AnimationClipResult> Execute(AnimationClipParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":    return Create(p);
                case "info":      return Info(p);
                case "set-curve": return SetCurve(p);
                case "add-event": return AddEvent(p);
                default:
                    return ToolResult<AnimationClipResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: {ValidActions}",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AnimationClipResult> Create(AnimationClipParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationClipResult>.Fail(
                    "Path is required for 'create' action", ErrorCodes.INVALID_PARAM);

            AnimationToolHelpers.EnsureDirectoryExists(p.Path);

            var clip = new AnimationClip();
            clip.name = !string.IsNullOrEmpty(p.ClipName)
                ? p.ClipName
                : System.IO.Path.GetFileNameWithoutExtension(p.Path);

            clip.frameRate = p.FrameRate > 0 ? p.FrameRate : 60f;

            // Set loop via AnimationClipSettings
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = p.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, p.Path);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<AnimationClipResult>.Ok(new AnimationClipResult
            {
                Action    = "create",
                Path      = p.Path,
                Guid      = guid,
                ClipName  = clip.name,
                FrameRate = clip.frameRate,
                IsLooping = p.Loop
            });
        }

        private static ToolResult<AnimationClipResult> Info(AnimationClipParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationClipResult>.Fail(
                    "Path is required for 'info' action", ErrorCodes.INVALID_PARAM);

            var clip = AnimationToolHelpers.LoadClip(p.Path);
            if (clip == null)
                return ToolResult<AnimationClipResult>.Fail(
                    $"AnimationClip not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curves = bindings.Select(b =>
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                return new AnimationCurveInfo
                {
                    Path          = b.path,
                    PropertyName  = b.propertyName,
                    Type          = b.type.Name,
                    KeyframeCount = curve != null ? curve.keys.Length : 0
                };
            }).ToArray();

            var events = AnimationUtility.GetAnimationEvents(clip);
            var eventInfos = events.Select(e => new AnimationEventInfo
            {
                Time            = e.time,
                FunctionName    = e.functionName,
                StringParameter = e.stringParameter,
                FloatParameter  = e.floatParameter,
                IntParameter    = e.intParameter
            }).ToArray();

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<AnimationClipResult>.Ok(new AnimationClipResult
            {
                Action     = "info",
                Path       = p.Path,
                Guid       = guid,
                ClipName   = clip.name,
                Length     = clip.length,
                FrameRate  = clip.frameRate,
                IsLooping  = settings.loopTime,
                CurveCount = curves.Length,
                EventCount = eventInfos.Length,
                Curves     = curves,
                Events     = eventInfos
            });
        }

        private static ToolResult<AnimationClipResult> SetCurve(AnimationClipParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationClipResult>.Fail(
                    "Path is required for 'set-curve' action", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.PropertyName))
                return ToolResult<AnimationClipResult>.Fail(
                    "PropertyName is required for 'set-curve' action", ErrorCodes.INVALID_PARAM);

            if (p.KeyframeTimes == null || p.KeyframeValues == null)
                return ToolResult<AnimationClipResult>.Fail(
                    "KeyframeTimes and KeyframeValues arrays are required for 'set-curve' action",
                    ErrorCodes.INVALID_PARAM);

            if (p.KeyframeTimes.Length != p.KeyframeValues.Length)
                return ToolResult<AnimationClipResult>.Fail(
                    "KeyframeTimes and KeyframeValues must have the same length",
                    ErrorCodes.INVALID_PARAM);

            var clip = AnimationToolHelpers.LoadClip(p.Path);
            if (clip == null)
                return ToolResult<AnimationClipResult>.Fail(
                    $"AnimationClip not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            // Resolve component type
            Type componentType = ResolveComponentType(p.ComponentType);
            if (componentType == null)
                return ToolResult<AnimationClipResult>.Fail(
                    $"Component type '{p.ComponentType}' not found. Use full type name (e.g. 'Transform', 'SpriteRenderer')",
                    ErrorCodes.INVALID_PARAM);

            var binding = new EditorCurveBinding
            {
                path         = p.PropertyPath ?? "",
                type         = componentType,
                propertyName = p.PropertyName
            };

            var keyframes = new Keyframe[p.KeyframeTimes.Length];
            for (int i = 0; i < keyframes.Length; i++)
                keyframes[i] = new Keyframe(p.KeyframeTimes[i], p.KeyframeValues[i]);

            var curve = new AnimationCurve(keyframes);

            Undo.RecordObject(clip, "Mosaic: Set Animation Curve");
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<AnimationClipResult>.Ok(new AnimationClipResult
            {
                Action    = "set-curve",
                Path      = p.Path,
                Guid      = guid,
                ClipName  = clip.name
            });
        }

        private static ToolResult<AnimationClipResult> AddEvent(AnimationClipParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationClipResult>.Fail(
                    "Path is required for 'add-event' action", ErrorCodes.INVALID_PARAM);

            if (!p.EventTime.HasValue)
                return ToolResult<AnimationClipResult>.Fail(
                    "EventTime is required for 'add-event' action", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.EventFunction))
                return ToolResult<AnimationClipResult>.Fail(
                    "EventFunction is required for 'add-event' action", ErrorCodes.INVALID_PARAM);

            var clip = AnimationToolHelpers.LoadClip(p.Path);
            if (clip == null)
                return ToolResult<AnimationClipResult>.Fail(
                    $"AnimationClip not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(clip, "Mosaic: Add Animation Event");

            var existing = AnimationUtility.GetAnimationEvents(clip);
            var eventList = new List<AnimationEvent>(existing);

            var evt = new AnimationEvent
            {
                time            = p.EventTime.Value,
                functionName    = p.EventFunction,
                stringParameter = p.EventStringParam ?? "",
                floatParameter  = p.EventFloatParam ?? 0f,
                intParameter    = p.EventIntParam ?? 0
            };
            eventList.Add(evt);

            AnimationUtility.SetAnimationEvents(clip, eventList.ToArray());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<AnimationClipResult>.Ok(new AnimationClipResult
            {
                Action     = "add-event",
                Path       = p.Path,
                Guid       = guid,
                ClipName   = clip.name,
                EventCount = eventList.Count
            });
        }

        private static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeof(Transform);

            // Try common Unity types first
            var directType = Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (directType != null) return directType;

            directType = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (directType != null) return directType;

            // Try full name
            directType = Type.GetType(typeName);
            if (directType != null) return directType;

            // Search all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var found = asm.GetType(typeName) ?? asm.GetType($"UnityEngine.{typeName}");
                if (found != null) return found;
            }

            return null;
        }
    }
}
