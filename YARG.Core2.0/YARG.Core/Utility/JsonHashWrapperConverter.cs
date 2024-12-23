using System;
using System.Globalization;
using Newtonsoft.Json;
using YARG.Core.Song;

namespace YARG.Core.Utility
{
    public class JsonHashWrapperConverter : JsonConverter<HashWrapper>
    {
        public override void WriteJson(JsonWriter writer, HashWrapper value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override HashWrapper ReadJson(JsonReader reader, Type objectType, HashWrapper existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.Value != null ? HashWrapper.FromString(reader.Value.ToString().AsSpan()) : default;
        }
    }
}