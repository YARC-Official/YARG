using System;
using Newtonsoft.Json;

namespace YARG.Input.Serialization
{
    public class ProfileBindingsConverter : JsonConverter<ProfileBindings>
    {
        public override bool CanRead => false;
        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, ProfileBindings value, JsonSerializer serializer)
        {
            var serialized = value.Serialize();
            serializer.Serialize(writer, serialized);
        }

        public override ProfileBindings ReadJson(JsonReader reader, Type objectType, ProfileBindings existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            throw new InvalidOperationException($"{nameof(ProfileBindings)} must be read as {nameof(SerializedProfileBindings)}.");
        }
    }
}