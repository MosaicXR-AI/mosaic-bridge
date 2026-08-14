using System;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            // Unwrap stringified JSON arrays/objects. Claude (and some other MCP clients) occasionally
            // serialize array parameters as string values — e.g. position: "[0, 8, -12]" instead of
            // position: [0, 8, -12]. The schema is correct, it's an LLM quirk. Without this step,
            // JSON.NET errors out trying to deserialize "[0, 8, -12]" to float[]. We detect string
            // values that start with '[' or '{' and re-parse them as JSON.
            try
            {
                parametersJson = UnwrapStringifiedJson(parametersJson);
            }
            catch
            {
                // Unwrap is best-effort — if it fails, fall through to the normal deserializer
                // which will produce a clearer error for the user.
            }

            object deserialized;
            try
            {
                // MissingMemberHandling.Error, because the generated schema already promises
                // `additionalProperties: false` and the default binder quietly ignored anything
                // extra. A caller that misspells a parameter, or invents one, got silence and a
                // successful-looking result computed from defaults — the parameter simply had no
                // effect. Silence is the worst possible answer here: the call looks like it did
                // what was asked.
                deserialized = JsonConvert.DeserializeObject(parametersJson, targetType,
                    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            }
            catch (JsonSerializationException ex) when (ex.Message.StartsWith("Could not find member", StringComparison.Ordinal))
            {
                return ParameterValidationResult.Fail(ErrorCodes.INVALID_PARAM,
                    UnknownParameterMessage(ex, targetType));
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

        /// <summary>
        /// Name the offending parameter AND the accepted ones. "Could not find member 'x' on ..."
        /// tells the caller what is wrong but not what to write instead, and the whole reason this
        /// error exists is that the caller believed a name that does not exist.
        /// </summary>
        private static string UnknownParameterMessage(JsonSerializationException ex, Type targetType)
        {
            string bad = null;
            var open = ex.Message.IndexOf('\'');
            if (open >= 0)
            {
                var close = ex.Message.IndexOf('\'', open + 1);
                if (close > open) bad = ex.Message.Substring(open + 1, close - open - 1);
            }

            var names = new System.Collections.Generic.List<string>();
            foreach (var prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var jp = prop.GetCustomAttribute<JsonPropertyAttribute>();
                var name = jp?.PropertyName ?? char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
                names.Add(prop.GetCustomAttribute<RequiredAttribute>() != null ? name + " (required)" : name);
            }
            names.Sort(StringComparer.Ordinal);

            return $"Unknown parameter '{bad ?? "?"}'. This tool accepts: {string.Join(", ", names)}. " +
                   "Unknown parameters are rejected rather than ignored, because a silently dropped " +
                   "parameter produces a result that looks correct and is not.";
        }

        /// <summary>Convenience overload for a known type.</summary>
        public static ParameterValidationResult Bind<T>(string parametersJson) =>
            Bind(parametersJson, typeof(T));

        /// <summary>
        /// Walks a parsed JSON tree and replaces any string value that looks like a JSON array
        /// or object (starts with '[' or '{') with its parsed form. Handles the common case
        /// where an LLM client stringifies an array parameter:
        ///   {"position": "[0, 8, -12]"}   →   {"position": [0, 8, -12]}
        ///   {"rotation": "[30.0, 0, 0]"}  →   {"rotation": [30.0, 0, 0]}
        /// Leaves non-JSON strings (and all other value types) unchanged.
        /// </summary>
        internal static string UnwrapStringifiedJson(string parametersJson)
        {
            var token = JToken.Parse(parametersJson);
            var unwrapped = UnwrapToken(token);
            return unwrapped.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static JToken UnwrapToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    var newObj = new JObject();
                    foreach (var property in obj.Properties())
                    {
                        newObj[property.Name] = UnwrapToken(property.Value);
                    }
                    return newObj;

                case JTokenType.Array:
                    var arr = (JArray)token;
                    var newArr = new JArray();
                    foreach (var item in arr)
                    {
                        newArr.Add(UnwrapToken(item));
                    }
                    return newArr;

                case JTokenType.String:
                    var s = token.Value<string>();
                    if (!string.IsNullOrEmpty(s))
                    {
                        // Heuristic: strings that start with '[' or '{' are likely stringified JSON.
                        // Only attempt to parse — if it fails, keep the string as-is (it's just a string
                        // that happens to start with a bracket, e.g. a literal string `[optional]`).
                        var trimmed = s.TrimStart();
                        if (trimmed.Length > 0 && (trimmed[0] == '[' || trimmed[0] == '{'))
                        {
                            try
                            {
                                var parsed = JToken.Parse(s);
                                // Recurse — the parsed content may itself contain stringified JSON
                                return UnwrapToken(parsed);
                            }
                            catch (JsonException)
                            {
                                // Not valid JSON — leave the original string intact.
                            }
                        }
                    }
                    return token;

                default:
                    return token;
            }
        }
    }
}
