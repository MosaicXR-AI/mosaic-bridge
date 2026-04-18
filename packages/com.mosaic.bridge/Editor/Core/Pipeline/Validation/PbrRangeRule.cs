using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Pipeline.Validation
{
    /// <summary>
    /// Validates that PBR material properties stay within the physically correct [0, 1] range.
    /// Rejects any tool call that sets roughness, metalness, smoothness, or occlusion outside [0.0, 1.0].
    /// </summary>
    public sealed class PbrRangeRule : IValidationRule
    {
        /// <summary>
        /// PBR property names that must be in [0, 1]. Covers common Unity Standard / URP / HDRP names.
        /// </summary>
        private static readonly string[] ZeroOneProperties =
        {
            "roughness",
            "metallic",
            "metalness",
            "smoothness",
            "occlusion",
            "opacity",
            "alpha",
            "clearCoat",
            "clearCoatRoughness",
            "reflectance"
        };

        public string Category => "material";

        public ValidationResult Validate(ExecutionContext context)
        {
            var parameters = context.Parameters;
            if (parameters == null)
                return ValidationResult.Pass();

            // Check top-level parameters
            foreach (var propName in ZeroOneProperties)
            {
                var result = CheckProperty(parameters, propName);
                if (result != null)
                    return result;
            }

            // Also check inside a nested "properties" or "values" object
            foreach (var containerName in new[] { "properties", "values" })
            {
                var container = parameters[containerName] as JObject;
                if (container == null)
                    continue;

                foreach (var propName in ZeroOneProperties)
                {
                    var result = CheckProperty(container, propName);
                    if (result != null)
                        return result;
                }
            }

            return ValidationResult.Pass();
        }

        private static ValidationResult CheckProperty(JObject obj, string propName)
        {
            var token = obj[propName];
            if (token == null || token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                return null;

            float value;
            try
            {
                value = token.Value<float>();
            }
            catch
            {
                return null;
            }

            if (value < 0f || value > 1f)
            {
                return ValidationResult.Reject(
                    $"PBR property '{propName}' value {value} is outside the valid [0, 1] range.");
            }

            return null;
        }
    }
}
