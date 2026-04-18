using System;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Pipeline.Validation
{
    /// <summary>
    /// Validates that position values on gameobject tools stay within Unity's reliable precision range.
    /// - Y position outside [-500, 5000]: Warning
    /// - Any position component > 100,000: Error (rejected — floating-point precision breaks down)
    /// </summary>
    public sealed class TransformRangeRule : IValidationRule
    {
        private const float YMin = -500f;
        private const float YMax = 5000f;
        private const float AbsoluteMax = 100000f;

        public string Category => "gameobject";

        public ValidationResult Validate(ExecutionContext context)
        {
            var parameters = context.Parameters;
            if (parameters == null)
                return ValidationResult.Pass();

            var posToken = parameters["position"];
            if (posToken == null)
                return ValidationResult.Pass();

            // Extract x, y, z from either array [x,y,z] or object {x,y,z}
            float? x = null, y = null, z = null;

            if (posToken is JArray arr && arr.Count >= 3)
            {
                try { x = arr[0].Value<float>(); y = arr[1].Value<float>(); z = arr[2].Value<float>(); }
                catch { return ValidationResult.Pass(); }
            }
            else if (posToken is JObject obj)
            {
                try
                {
                    if (obj["x"] != null) x = obj["x"].Value<float>();
                    if (obj["y"] != null) y = obj["y"].Value<float>();
                    if (obj["z"] != null) z = obj["z"].Value<float>();
                }
                catch { return ValidationResult.Pass(); }
            }
            else
            {
                return ValidationResult.Pass();
            }

            // Check absolute precision limit on all components
            var components = new[] { ("x", x), ("y", y), ("z", z) };
            foreach (var (axis, val) in components)
            {
                if (val.HasValue && Math.Abs(val.Value) > AbsoluteMax)
                {
                    return ValidationResult.Reject(
                        $"Position component exceeds Unity's reliable precision range ({axis}={val.Value}).");
                }
            }

            // Check Y-position typical range
            if (y.HasValue && (y.Value < YMin || y.Value > YMax))
            {
                return ValidationResult.Warn(
                    $"Y position {y.Value} is outside typical range [{YMin}, {YMax}].");
            }

            return ValidationResult.Pass();
        }
    }
}
