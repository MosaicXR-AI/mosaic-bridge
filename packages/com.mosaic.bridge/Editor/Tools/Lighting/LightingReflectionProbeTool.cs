using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingReflectionProbeTool
    {
        [MosaicTool("lighting/reflection-probe",
                    "create/set: Mode (Baked/Realtime/Custom), Resolution, Size/Center, " +
                    "BoxProjection, Hdr, Intensity, Importance, clip planes, ShadowDistance, " +
                    "BlendDistance. bake: synchronously bakes one probe to BakePath (defaults to " +
                    "'Assets/{Name}.exr'). bake-all: bakes every ReflectionProbe in the scene. " +
                    "Metallic/glass surfaces need this to look right in a capture.",
                    isReadOnly: false)]
        public static ToolResult<LightingReflectionProbeResult> Execute(LightingReflectionProbeParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":   return Create(p);
                case "set":      return Set(p);
                case "bake":     return Bake(p);
                case "bake-all": return BakeAll(p);
                default:
                    return ToolResult<LightingReflectionProbeResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: create, set, bake, bake-all", ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<LightingReflectionProbeResult> Create(LightingReflectionProbeParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<LightingReflectionProbeResult>.Fail("Name is required for create", ErrorCodes.INVALID_PARAM);

            var go = new GameObject(p.Name);
            var probe = go.AddComponent<ReflectionProbe>();

            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            if (!ApplyFields(p, probe, out var error))
            {
                Object.DestroyImmediate(go);
                return ToolResult<LightingReflectionProbeResult>.Fail(error, ErrorCodes.INVALID_PARAM);
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Reflection Probe");

            return ToolResult<LightingReflectionProbeResult>.Ok(ToResult("create", go, probe));
        }

        private static ToolResult<LightingReflectionProbeResult> Set(LightingReflectionProbeParams p)
        {
            if (!TryResolveProbe(p.Name, out var probe, out var error))
                return ToolResult<LightingReflectionProbeResult>.Fail(error, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(probe, "Mosaic: Set Reflection Probe");
            if (!ApplyFields(p, probe, out var applyError))
                return ToolResult<LightingReflectionProbeResult>.Fail(applyError, ErrorCodes.INVALID_PARAM);
            EditorUtility.SetDirty(probe);

            return ToolResult<LightingReflectionProbeResult>.Ok(ToResult("set", probe.gameObject, probe));
        }

        private static ToolResult<LightingReflectionProbeResult> Bake(LightingReflectionProbeParams p)
        {
            if (!TryResolveProbe(p.Name, out var probe, out var error))
                return ToolResult<LightingReflectionProbeResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var path = string.IsNullOrEmpty(p.BakePath) ? $"Assets/{probe.gameObject.name}.exr" : p.BakePath;
            var success = Lightmapping.BakeReflectionProbe(probe, path);

            var result = ToResult("bake", probe.gameObject, probe);
            result.BakeSuccess = success;
            result.BakePath = path;
            return ToolResult<LightingReflectionProbeResult>.Ok(result);
        }

        private static ToolResult<LightingReflectionProbeResult> BakeAll(LightingReflectionProbeParams p)
        {
            var probes = UnityIds.FindAll<ReflectionProbe>();
            var baked = probes.Select(probe =>
            {
                var path = $"Assets/{probe.gameObject.name}.exr";
                var success = Lightmapping.BakeReflectionProbe(probe, path);
                return new LightingReflectionProbeBakedInfo
                {
                    Name = probe.gameObject.name, Success = success, BakePath = path,
                };
            }).ToArray();

            return ToolResult<LightingReflectionProbeResult>.Ok(new LightingReflectionProbeResult
            {
                Action = "bake-all", BakedProbes = baked,
            });
        }

        private static bool ApplyFields(LightingReflectionProbeParams p, ReflectionProbe probe, out string error)
        {
            error = null;

            if (!string.IsNullOrEmpty(p.Mode))
            {
                if (!TryParseMode(p.Mode, out var mode))
                {
                    error = $"Unknown Mode '{p.Mode}'. Valid: Baked, Realtime, Custom";
                    return false;
                }
                probe.mode = mode;
            }

            if (p.Resolution.HasValue) probe.resolution = p.Resolution.Value;

            if (p.Size != null)
            {
                if (p.Size.Length != 3) { error = "Size requires exactly [x, y, z]"; return false; }
                probe.size = new Vector3(p.Size[0], p.Size[1], p.Size[2]);
            }

            if (p.Center != null)
            {
                if (p.Center.Length != 3) { error = "Center requires exactly [x, y, z]"; return false; }
                probe.center = new Vector3(p.Center[0], p.Center[1], p.Center[2]);
            }

            if (p.BoxProjection.HasValue) probe.boxProjection = p.BoxProjection.Value;
            if (p.Hdr.HasValue) probe.hdr = p.Hdr.Value;
            if (p.Intensity.HasValue) probe.intensity = p.Intensity.Value;
            if (p.Importance.HasValue) probe.importance = p.Importance.Value;
            if (p.NearClipPlane.HasValue) probe.nearClipPlane = p.NearClipPlane.Value;
            if (p.FarClipPlane.HasValue) probe.farClipPlane = p.FarClipPlane.Value;
            if (p.ShadowDistance.HasValue) probe.shadowDistance = p.ShadowDistance.Value;
            if (p.BlendDistance.HasValue) probe.blendDistance = p.BlendDistance.Value;

            return true;
        }

        private static LightingReflectionProbeResult ToResult(string action, GameObject go, ReflectionProbe probe) =>
            new LightingReflectionProbeResult
            {
                Action = action, InstanceId = UnityIds.Of(go), Name = go.name,
                Mode = probe.mode.ToString(), Resolution = probe.resolution, Intensity = probe.intensity,
            };

        private static bool TryResolveProbe(string name, out ReflectionProbe probe, out string error)
        {
            probe = null;
            error = null;
            var go = GameObject.Find(name);
            if (go == null)
            {
                error = $"GameObject '{name}' not found";
                return false;
            }
            probe = go.GetComponent<ReflectionProbe>();
            if (probe == null)
            {
                error = $"GameObject '{name}' does not have a ReflectionProbe component";
                return false;
            }
            return true;
        }

        private static bool TryParseMode(string value, out ReflectionProbeMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "baked":    result = ReflectionProbeMode.Baked;    return true;
                case "realtime": result = ReflectionProbeMode.Realtime; return true;
                case "custom":   result = ReflectionProbeMode.Custom;   return true;
                default:         result = ReflectionProbeMode.Baked;    return false;
            }
        }
    }
}
