using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.Shared
{
    /// <summary>
    /// Reads a string parameter that may arrive as a JSON object or array, and yields its text.
    /// </summary>
    /// <remarks>
    /// File content is opaque text to us, but not to the transport. A caller writing an .asmdef,
    /// a .json or any structured file naturally sends the structure — and the deserializer, told
    /// the target is a <c>string</c>, refused it:
    ///
    ///   INVALID_PARAM: Unexpected character encountered while parsing value: {.
    ///                  Path 'content', line 1, position 60.
    ///
    /// So every content beginning with <c>{</c> was rejected, which is exactly the set of files
    /// worth writing structurally. The error also names a JSON parse failure, which reads as
    /// "your JSON is malformed" when the JSON was fine and the TYPE was the problem — several
    /// hours were spent on that reading.
    ///
    /// There is nothing to disambiguate: an object arriving where file text is expected can only
    /// mean "write this JSON". Serialized indented, because these files are read by people and
    /// diffed in review.
    /// </remarks>
    public sealed class RawTextConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
                                        JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return null;
                case JsonToken.String:
                    return (string)reader.Value;
                case JsonToken.StartObject:
                case JsonToken.StartArray:
                    return JToken.Load(reader).ToString(Formatting.Indented);
                default:
                    // Numbers and booleans: a caller writing "42" into a file meant the text.
                    return JToken.Load(reader).ToString();
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => throw new NotSupportedException("RawTextConverter is read-only.");
    }
}
