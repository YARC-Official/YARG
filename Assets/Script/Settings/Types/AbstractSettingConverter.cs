using System;
using Newtonsoft.Json;
using UnityEngine;

namespace YARG.Settings.Types
{
    public sealed class AbstractSettingConverter : JsonConverter<ISettingType>
    {
        public override ISettingType ReadJson(JsonReader reader, Type objectType, ISettingType existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
            {
                Debug.LogWarning($"No existing setting value was provided!");
                return null;
            }

            var value = serializer.Deserialize(reader, existingValue.DataType);
            existingValue.DataAsObject = value;

            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, ISettingType value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.DataAsObject, value.DataType);
        }
    }
}