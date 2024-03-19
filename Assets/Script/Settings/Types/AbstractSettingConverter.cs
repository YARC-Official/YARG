using System;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Settings.Types
{
    public sealed class AbstractSettingConverter : JsonConverter<ISettingType>
    {
        public override ISettingType ReadJson(JsonReader reader, Type objectType, ISettingType existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
            {
                YargLogger.LogWarning("No existing setting value was provided!");
                return null;
            }

            // Make sure the whole settings file doesn't get reset if it fails to read
            try
            {
                var value = serializer.Deserialize(reader, existingValue.ValueType);
                existingValue.ValueAsObject = value;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to read setting value. See error below for more details.");
                return existingValue;
            }

            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, ISettingType value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ValueAsObject, value.ValueType);
        }
    }
}