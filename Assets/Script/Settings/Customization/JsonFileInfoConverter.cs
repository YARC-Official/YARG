using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YARG.Settings.Customization
{
    public class JsonFileInfoConverter : JsonConverter<FileInfo>
    {
        public override void WriteJson(JsonWriter writer, FileInfo value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Name);
        }

        public override FileInfo ReadJson(JsonReader reader, Type objectType, FileInfo existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            string filename;
            if (reader.TokenType == JsonToken.String)
            {
                filename = reader.Value?.ToString();
            }
            else if (reader.TokenType == JsonToken.StartObject)
            {
                var legacyFileInfo = JObject.Load(reader);
                var filenameToken = legacyFileInfo.GetValue("OriginalPath", StringComparison.OrdinalIgnoreCase)
                    ?? legacyFileInfo.GetValue("FullName", StringComparison.OrdinalIgnoreCase)
                    ?? legacyFileInfo.GetValue("Name", StringComparison.OrdinalIgnoreCase);
                filename = filenameToken?.ToString();
            }
            else
            {
                throw new JsonSerializationException("Expected a filename string or legacy FileInfo object.");
            }

            return string.IsNullOrWhiteSpace(filename) ? null : new FileInfo(Path.GetFileName(filename));
        }
    }
}