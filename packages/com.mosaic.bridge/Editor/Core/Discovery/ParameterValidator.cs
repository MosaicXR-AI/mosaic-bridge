using System;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Errors;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Discovery
{
    public static class ParameterValidator
    {
        /// <summary>
        /// Deserializes <paramref name="parametersJson"/> into <paramref name="targetType"/> and
        /// validates all [Required] properties. Returns Ok(null) immediately when targetType is null
        /// (method takes no parameters).
        /// </summary>
        public static ParameterValidationResult Bind(string parametersJson, Type targetType)
        {
            if (targetType == null)
                return ParameterValidationResult.Ok(null);

            if (string.IsNullOrWhiteSpace(parametersJson))
                return ParameterValidationResult.Fail(ErrorCodes.INVALID_PARAM, "parameters field is missing");

            object deserialized;
            try
            {
                deserialized = JsonConvert.DeserializeObject(parametersJson, targetType);
            }
            catch (JsonException ex)
            {
                return ParameterValidationResult.Fail(ErrorCodes.INVALID_PARAM, ex.Message);
            }

            // Validate [Required] properties
            foreach (var prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var required = prop.GetCustomAttribute<RequiredAttribute>();
                if (required == null)
                    continue;

                var value = prop.GetValue(deserialized);
                if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
                {
                    var msg = !string.IsNullOrEmpty(required.ErrorMessage)
                        ? required.ErrorMessage
                        : $"Required parameter '{prop.Name}' is missing or empty";
                    return ParameterValidationResult.Fail(ErrorCodes.INVALID_PARAM, msg);
                }
            }

            return ParameterValidationResult.Ok(deserialized);
        }

        /// <summary>Convenience overload for a known type.</summary>
        public static ParameterValidationResult Bind<T>(string parametersJson) =>
            Bind(parametersJson, typeof(T));
    }
}
