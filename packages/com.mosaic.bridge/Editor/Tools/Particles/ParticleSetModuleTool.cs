using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleSetModuleTool
    {
        // O4 §4.8: particle/* previously only touched main/emission/shape/renderer — every
        // lifetime module (color/size/velocity/limitVelocity/rotation/noise/force over lifetime)
        // was absent, so fire fading or smoke growing had to be hard-coded into presets instead of
        // authored. Module structs are live property wrappers into the ParticleSystem (not plain
        // data) — assigning a member on a local copy applies immediately, no reassignment needed.
        [MosaicTool("particle/set-module",
                    "Configures a ParticleSystem lifetime module: colorOverLifetime (ColorKeyTimes/Colors, " +
                    "AlphaKeyTimes/Values — fire fading out), sizeOverLifetime (CurveScalar/Times/Values — " +
                    "smoke that grows), velocityOverLifetime/forceOverLifetime (X/Y/ZConstant or " +
                    "X/Y/ZCurveTimes/Values, Space), limitVelocity (LimitConstant or LimitCurveTimes/Values, " +
                    "Dampen), rotationOverLifetime (CurveScalar/Times/Values, RADIANS/sec — the Inspector " +
                    "displays degrees, but the underlying field is radians), noise " +
                    "(NoiseStrength/Frequency/ScrollSpeed/Damping/OctaveCount/Multiplier/Scale).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticleSetModuleResult> Execute(ParticleSetModuleParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<ParticleSetModuleResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var ps = ParticleToolHelpers.Resolve(p.InstanceId, p.Name);
            if (ps == null)
                return ToolResult<ParticleSetModuleResult>.Fail(
                    $"ParticleSystem not found (InstanceId={p.InstanceId}, Name='{p.Name}')", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(ps, "Mosaic: Set ParticleSystem Module");

            bool enabled;
            switch (p.Module?.ToLowerInvariant())
            {
                case "coloroverlifetime": enabled = ApplyColorOverLifetime(ps, p); break;
                case "sizeoverlifetime": enabled = ApplySizeOverLifetime(ps, p); break;
                case "velocityoverlifetime": if (!TryApplyVelocityOverLifetime(ps, p, out enabled, out var velErr))
                        return ToolResult<ParticleSetModuleResult>.Fail(velErr, ErrorCodes.INVALID_PARAM);
                    break;
                case "limitvelocity": enabled = ApplyLimitVelocity(ps, p); break;
                case "rotationoverlifetime": enabled = ApplyRotationOverLifetime(ps, p); break;
                case "noise": enabled = ApplyNoise(ps, p); break;
                case "forceoverlifetime": if (!TryApplyForceOverLifetime(ps, p, out enabled, out var forceErr))
                        return ToolResult<ParticleSetModuleResult>.Fail(forceErr, ErrorCodes.INVALID_PARAM);
                    break;
                default:
                    return ToolResult<ParticleSetModuleResult>.Fail(
                        $"Unknown Module '{p.Module}'. Valid: colorOverLifetime, sizeOverLifetime, " +
                        "velocityOverLifetime, limitVelocity, rotationOverLifetime, noise, forceOverLifetime",
                        ErrorCodes.INVALID_PARAM);
            }

            return ToolResult<ParticleSetModuleResult>.Ok(new ParticleSetModuleResult
            {
                InstanceId = UnityIds.Of(ps.gameObject),
                Name       = ps.gameObject.name,
                Module     = p.Module,
                Enabled    = enabled,
                Message    = $"{p.Module} module {(enabled ? "enabled and configured" : "disabled")} on '{ps.gameObject.name}'.",
            });
        }

        private static bool ApplyColorOverLifetime(ParticleSystem ps, ParticleSetModuleParams p)
        {
            var module = ps.colorOverLifetime;
            bool enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (enabled && p.ColorKeyTimes != null)
                module.color = new ParticleSystem.MinMaxGradient(BuildGradient(p));
            return enabled;
        }

        private static Gradient BuildGradient(ParticleSetModuleParams p)
        {
            var gradient = new Gradient();
            int colorCount = p.ColorKeyTimes?.Length ?? 0;
            var colorKeys = new GradientColorKey[colorCount];
            for (int i = 0; i < colorCount; i++)
                colorKeys[i] = new GradientColorKey(
                    new Color(p.ColorKeyColors[i * 3], p.ColorKeyColors[i * 3 + 1], p.ColorKeyColors[i * 3 + 2]),
                    p.ColorKeyTimes[i]);

            int alphaCount = p.AlphaKeyTimes?.Length ?? 0;
            var alphaKeys = new GradientAlphaKey[alphaCount > 0 ? alphaCount : 2];
            if (alphaCount > 0)
            {
                for (int i = 0; i < alphaCount; i++)
                    alphaKeys[i] = new GradientAlphaKey(p.AlphaKeyValues[i], p.AlphaKeyTimes[i]);
            }
            else
            {
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            }

            gradient.SetKeys(colorKeys.Length > 0 ? colorKeys : new[]
            {
                new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f)
            }, alphaKeys);
            return gradient;
        }

        private static bool ApplySizeOverLifetime(ParticleSystem ps, ParticleSetModuleParams p)
        {
            var module = ps.sizeOverLifetime;
            bool enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (enabled && p.CurveTimes != null)
                module.size = BuildMinMaxCurve(p.CurveScalar ?? 1f, p.CurveTimes, p.CurveValues);
            return enabled;
        }

        private static bool ApplyRotationOverLifetime(ParticleSystem ps, ParticleSetModuleParams p)
        {
            var module = ps.rotationOverLifetime;
            bool enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (enabled && p.CurveTimes != null)
                module.z = BuildMinMaxCurve(p.CurveScalar ?? 1f, p.CurveTimes, p.CurveValues);
            return enabled;
        }

        private static bool ApplyLimitVelocity(ParticleSystem ps, ParticleSetModuleParams p)
        {
            var module = ps.limitVelocityOverLifetime;
            bool enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (enabled)
            {
                if (p.LimitCurveTimes != null)
                    module.limit = BuildMinMaxCurve(p.LimitConstant ?? 1f, p.LimitCurveTimes, p.LimitCurveValues);
                else if (p.LimitConstant.HasValue)
                    module.limit = new ParticleSystem.MinMaxCurve(p.LimitConstant.Value);
                if (p.Dampen.HasValue) module.dampen = p.Dampen.Value;
            }
            return enabled;
        }

        private static bool ApplyNoise(ParticleSystem ps, ParticleSetModuleParams p)
        {
            var module = ps.noise;
            bool enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (enabled)
            {
                if (p.NoiseStrength.HasValue) module.strength = new ParticleSystem.MinMaxCurve(p.NoiseStrength.Value);
                if (p.NoiseFrequency.HasValue) module.frequency = p.NoiseFrequency.Value;
                if (p.NoiseScrollSpeed.HasValue) module.scrollSpeed = new ParticleSystem.MinMaxCurve(p.NoiseScrollSpeed.Value);
                if (p.NoiseDamping.HasValue) module.damping = p.NoiseDamping.Value;
                if (p.NoiseOctaveCount.HasValue) module.octaveCount = p.NoiseOctaveCount.Value;
                if (p.NoiseOctaveMultiplier.HasValue) module.octaveMultiplier = p.NoiseOctaveMultiplier.Value;
                if (p.NoiseOctaveScale.HasValue) module.octaveScale = p.NoiseOctaveScale.Value;
            }
            return enabled;
        }

        private static bool TryApplyVelocityOverLifetime(ParticleSystem ps, ParticleSetModuleParams p, out bool enabled, out string error)
        {
            error = null;
            var module = ps.velocityOverLifetime;
            enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (!enabled) return true;

            if (!TryParseSpace(p.Space, out var space, out error)) return false;
            if (!string.IsNullOrEmpty(p.Space)) module.space = space;

            if (p.XCurveTimes != null) module.x = BuildMinMaxCurve(p.XConstant ?? 1f, p.XCurveTimes, p.XCurveValues);
            else if (p.XConstant.HasValue) module.x = new ParticleSystem.MinMaxCurve(p.XConstant.Value);
            if (p.YCurveTimes != null) module.y = BuildMinMaxCurve(p.YConstant ?? 1f, p.YCurveTimes, p.YCurveValues);
            else if (p.YConstant.HasValue) module.y = new ParticleSystem.MinMaxCurve(p.YConstant.Value);
            if (p.ZCurveTimes != null) module.z = BuildMinMaxCurve(p.ZConstant ?? 1f, p.ZCurveTimes, p.ZCurveValues);
            else if (p.ZConstant.HasValue) module.z = new ParticleSystem.MinMaxCurve(p.ZConstant.Value);
            if (p.SpeedModifierCurveTimes != null)
                module.speedModifier = BuildMinMaxCurve(1f, p.SpeedModifierCurveTimes, p.SpeedModifierCurveValues);

            return true;
        }

        private static bool TryApplyForceOverLifetime(ParticleSystem ps, ParticleSetModuleParams p, out bool enabled, out string error)
        {
            error = null;
            var module = ps.forceOverLifetime;
            enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (!enabled) return true;

            if (!TryParseSpace(p.Space, out var space, out error)) return false;
            if (!string.IsNullOrEmpty(p.Space)) module.space = space;
            if (p.Randomized.HasValue) module.randomized = p.Randomized.Value;

            if (p.XCurveTimes != null) module.x = BuildMinMaxCurve(p.XConstant ?? 1f, p.XCurveTimes, p.XCurveValues);
            else if (p.XConstant.HasValue) module.x = new ParticleSystem.MinMaxCurve(p.XConstant.Value);
            if (p.YCurveTimes != null) module.y = BuildMinMaxCurve(p.YConstant ?? 1f, p.YCurveTimes, p.YCurveValues);
            else if (p.YConstant.HasValue) module.y = new ParticleSystem.MinMaxCurve(p.YConstant.Value);
            if (p.ZCurveTimes != null) module.z = BuildMinMaxCurve(p.ZConstant ?? 1f, p.ZCurveTimes, p.ZCurveValues);
            else if (p.ZConstant.HasValue) module.z = new ParticleSystem.MinMaxCurve(p.ZConstant.Value);

            return true;
        }

        private static bool TryParseSpace(string value, out ParticleSystemSimulationSpace space, out string error)
        {
            space = ParticleSystemSimulationSpace.Local;
            error = null;
            if (string.IsNullOrEmpty(value)) return true;
            if (!System.Enum.TryParse(value, ignoreCase: true, out space))
            {
                error = $"Unknown Space '{value}'. Valid: Local, World, Custom";
                return false;
            }
            return true;
        }

        private static ParticleSystem.MinMaxCurve BuildMinMaxCurve(float scalar, float[] times, float[] values)
        {
            if (times == null || values == null || times.Length != values.Length || times.Length == 0)
                return new ParticleSystem.MinMaxCurve(scalar);

            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++)
                keys[i] = new Keyframe(times[i], values[i]);
            return new ParticleSystem.MinMaxCurve(scalar, new AnimationCurve(keys));
        }
    }
}
