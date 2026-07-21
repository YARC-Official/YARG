using System;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Helpers.Extensions;

namespace YARG.Helpers
{
    public class JsonUnityColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ToSystemColor());
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var systemColor = serializer.Deserialize<System.Drawing.Color>(reader);
            return systemColor.ToUnityColor();
        }
    }
}
