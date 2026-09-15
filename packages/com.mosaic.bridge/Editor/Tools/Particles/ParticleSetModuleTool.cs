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
                    "(NoiseStrength/Frequency/ScrollSpeed/Damping/OctaveCount/Multiplier/Scale), " +
                    "collision (CollisionType/Mode, Dampen/Bounce/LifetimeLoss, Min/MaxKillSpeed, " +
                    "CollidesWithLayers, SendCollisionMessages, RadiusScale), subEmitters (SubEmitterName " +
                    "+ SubEmitterType + SubEmitterProperties + SubEmitterEmitProbability — the sub-emitter " +
                    "must be a child ParticleSystem), trails (TrailRatio, TrailLifetimeConstant/CurveTimes/" +
                    "Values, TrailMinVertexDistance, TrailWorldSpace, TrailDieWithParticles, " +
                    "TrailSizeAffectsWidth), lights (LightPrefabPath, LightRatio, LightUseRandomDistribution/" +
                    "ParticleColor, LightSizeAffectsRange, LightAlphaAffectsIntensity, LightMaxLights), " +
                    "textureSheetAnimation (TilesX/Y, TsaAnimation, Fps, CycleCount, TsaStartFrameConstant).",
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
                case "collision": if (!TryApplyCollision(ps, p, out enabled, out var collisionErr))
                        return ToolResult<ParticleSetModuleResult>.Fail(collisionErr, ErrorCodes.INVALID_PARAM);
                    break;
                case "subemitters": if (!TryApplySubEmitters(ps, p, out enabled, out var subErr, out var subErrCode))
                        return ToolResult<ParticleSetModuleResult>.Fail(subErr, subErrCode);
                    break;
                case "trails": enabled = ApplyTrails(ps, p); break;
                case "lights": if (!TryApplyLights(ps, p, out enabled, out var lightsErr))
                        return ToolResult<ParticleSetModuleResult>.Fail(lightsErr, ErrorCodes.NOT_FOUND);
                    break;
                case "texturesheetanimation": if (!TryApplyTextureSheetAnimation(ps, p, out enabled, out var tsaErr))
                        return ToolResult<ParticleSetModuleResult>.Fail(tsaErr, ErrorCodes.INVALID_PARAM);
                    break;
                default:
                    return ToolResult<ParticleSetModuleResult>.Fail(
                        $"Unknown Module '{p.Module}'. Valid: colorOverLifetime, sizeOverLifetime, " +
                        "velocityOverLifetime, limitVelocity, rotationOverLifetime, noise, forceOverLifetime, " +
                        "collision, subEmitters, trails, lights, textureSheetAnimation",
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

        private static bool TryApplyCollision(ParticleSystem ps, ParticleSetModuleParams p, out bool enabled, out string error)
        {
            error = null;
            var module = ps.collision;
            enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (!enabled) return true;

            if (!string.IsNullOrEmpty(p.CollisionType))
            {
                if (!System.Enum.TryParse<ParticleSystemCollisionType>(p.CollisionType, ignoreCase: true, out var type))
                {
                    error = $"Unknown CollisionType '{p.CollisionType}'. Valid: Planes, World";
                    return false;
                }
                module.type = type;
            }
            if (!string.IsNullOrEmpty(p.CollisionMode))
            {
                if (!System.Enum.TryParse<ParticleSystemCollisionMode>(p.CollisionMode, ignoreCase: true, out var mode))
                {
                    error = $"Unknown CollisionMode '{p.CollisionMode}'. Valid: Collision3D, Collision2D";
                    return false;
                }
                module.mode = mode;
            }
            if (p.Dampen.HasValue) module.dampen = p.Dampen.Value;
            if (p.Bounce.HasValue) module.bounce = p.Bounce.Value;
            if (p.LifetimeLoss.HasValue) module.lifetimeLoss = p.LifetimeLoss.Value;
            if (p.MinKillSpeed.HasValue) module.minKillSpeed = p.MinKillSpeed.Value;
            if (p.MaxKillSpeed.HasValue) module.maxKillSpeed = p.MaxKillSpeed.Value;
            if (p.SendCollisionMessages.HasValue) module.sendCollisionMessages = p.SendCollisionMessages.Value;
            if (p.RadiusScale.HasValue) module.radiusScale = p.RadiusScale.Value;
            if (!string.IsNullOrEmpty(p.CollidesWithLayers))
                module.collidesWith = LayerMask.GetMask(System.Array.ConvertAll(p.CollidesWithLayers.Split(','), s => s.Trim()));

            return true;
        }

        private static bool TryApplySubEmitters(ParticleSystem ps, ParticleSetModuleParams p, out bool enabled, out string error, out string errorCode)
        {
            error = null;
            errorCode = ErrorCodes.INVALID_PARAM;
            var module = ps.subEmitters;
            enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (!enabled) return true;

            if (string.IsNullOrEmpty(p.SubEmitterName)) return true;

            var subGo = GameObject.Find(p.SubEmitterName);
            if (subGo == null)
            {
                error = $"No GameObject named '{p.SubEmitterName}' found";
                errorCode = ErrorCodes.NOT_FOUND;
                return false;
            }
            var subPs = subGo.GetComponent<ParticleSystem>();
            if (subPs == null)
            {
                error = $"No ParticleSystem component on '{p.SubEmitterName}'";
                errorCode = ErrorCodes.NOT_FOUND;
                return false;
            }

            if (!System.Enum.TryParse<ParticleSystemSubEmitterType>(p.SubEmitterType ?? "Birth", ignoreCase: true, out var type))
            {
                error = $"Unknown SubEmitterType '{p.SubEmitterType}'. Valid: Birth, Collision, Death, Trigger, Manual";
                return false;
            }

            var properties = ParticleSystemSubEmitterProperties.InheritNothing;
            if (!string.IsNullOrEmpty(p.SubEmitterProperties))
            {
                foreach (var token in p.SubEmitterProperties.Split(','))
                {
                    if (!System.Enum.TryParse<ParticleSystemSubEmitterProperties>(token.Trim(), ignoreCase: true, out var prop))
                    {
                        error = $"Unknown SubEmitterProperties value '{token.Trim()}'. Valid: InheritNothing, " +
                                "InheritEverything, InheritColor, InheritSize, InheritRotation, InheritLifetime, InheritDuration";
                        return false;
                    }
                    properties |= prop;
                }
            }

            module.AddSubEmitter(subPs, type, properties, p.SubEmitterEmitProbability ?? 1f);
            return true;
        }

        private static bool ApplyTrails(ParticleSystem ps, ParticleSetModuleParams p)
        {
            var module = ps.trails;
            bool enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (enabled)
            {
                if (p.TrailRatio.HasValue) module.ratio = p.TrailRatio.Value;
                if (p.TrailMinVertexDistance.HasValue) module.minVertexDistance = p.TrailMinVertexDistance.Value;
                if (p.TrailWorldSpace.HasValue) module.worldSpace = p.TrailWorldSpace.Value;
                if (p.TrailDieWithParticles.HasValue) module.dieWithParticles = p.TrailDieWithParticles.Value;
                if (p.TrailSizeAffectsWidth.HasValue) module.sizeAffectsWidth = p.TrailSizeAffectsWidth.Value;
                if (p.TrailLifetimeCurveTimes != null)
                    module.lifetime = BuildMinMaxCurve(p.TrailLifetimeConstant ?? 1f, p.TrailLifetimeCurveTimes, p.TrailLifetimeCurveValues);
                else if (p.TrailLifetimeConstant.HasValue)
                    module.lifetime = new ParticleSystem.MinMaxCurve(p.TrailLifetimeConstant.Value);
            }
            return enabled;
        }

        private static bool TryApplyLights(ParticleSystem ps, ParticleSetModuleParams p, out bool enabled, out string error)
        {
            error = null;
            var module = ps.lights;
            enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (!enabled) return true;

            if (!string.IsNullOrEmpty(p.LightPrefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.LightPrefabPath);
                if (prefab == null) { error = $"Prefab not found at '{p.LightPrefabPath}'"; return false; }
                var light = prefab.GetComponent<Light>();
                if (light == null) { error = $"Prefab at '{p.LightPrefabPath}' has no Light component"; return false; }
                module.light = light;
            }
            if (p.LightRatio.HasValue) module.ratio = p.LightRatio.Value;
            if (p.LightUseRandomDistribution.HasValue) module.useRandomDistribution = p.LightUseRandomDistribution.Value;
            if (p.LightUseParticleColor.HasValue) module.useParticleColor = p.LightUseParticleColor.Value;
            if (p.LightSizeAffectsRange.HasValue) module.sizeAffectsRange = p.LightSizeAffectsRange.Value;
            if (p.LightAlphaAffectsIntensity.HasValue) module.alphaAffectsIntensity = p.LightAlphaAffectsIntensity.Value;
            if (p.LightMaxLights.HasValue) module.maxLights = p.LightMaxLights.Value;

            return true;
        }

        private static bool TryApplyTextureSheetAnimation(ParticleSystem ps, ParticleSetModuleParams p, out bool enabled, out string error)
        {
            error = null;
            var module = ps.textureSheetAnimation;
            enabled = p.Enabled ?? true;
            module.enabled = enabled;
            if (!enabled) return true;

            if (p.TilesX.HasValue) module.numTilesX = p.TilesX.Value;
            if (p.TilesY.HasValue) module.numTilesY = p.TilesY.Value;
            if (!string.IsNullOrEmpty(p.TsaAnimation))
            {
                if (!System.Enum.TryParse<ParticleSystemAnimationType>(p.TsaAnimation, ignoreCase: true, out var animation))
                {
                    error = $"Unknown TsaAnimation '{p.TsaAnimation}'. Valid: WholeSheet, SingleRow";
                    return false;
                }
                module.animation = animation;
            }
            if (p.Fps.HasValue) module.fps = p.Fps.Value;
            if (p.CycleCount.HasValue) module.cycleCount = p.CycleCount.Value;
            if (p.TsaStartFrameConstant.HasValue) module.startFrame = new ParticleSystem.MinMaxCurve(p.TsaStartFrameConstant.Value);

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
