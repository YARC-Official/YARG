using System;
using System.IO;
using Newtonsoft.Json;

namespace YARG.Serialization
{
    public sealed class DirectoryInfoConverter : JsonConverter<DirectoryInfo>
    {
        public override DirectoryInfo ReadJson(JsonReader reader, Type objectType, DirectoryInfo existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var path = serializer.Deserialize<string>(reader);
            return new DirectoryInfo(path);
        }

        public override void WriteJson(JsonWriter writer, DirectoryInfo value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.FullName);
        }
    }
}