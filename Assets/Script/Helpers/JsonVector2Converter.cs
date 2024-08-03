using System;
using Newtonsoft.Json;
using UnityEngine;

namespace YARG.Helpers
{
    public class JsonVector2Converter : JsonConverter<Vector2>
    {
        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.x);
            writer.WriteValue(value.y);
            writer.WriteEndArray();
        }

        public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartArray)
            {
                return Vector2.zero;
            }

            var x = reader.ReadAsDouble();
            if (x is null)
            {
                return Vector2.zero;
            }

            var y = reader.ReadAsDouble();
            if (y is null)
            {
                return Vector2.zero;
            }

            reader.Read();
            if (reader.TokenType != JsonToken.EndArray)
            {
                return Vector2.zero;
            }

            return new Vector2((float) x.Value, (float) y.Value);
        }
    }
}