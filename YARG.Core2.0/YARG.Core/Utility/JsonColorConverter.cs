using System;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json;

namespace YARG.Core.Utility
{
    public class JsonColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            int argb = value.ToArgb();

            byte a = (byte) ((argb >> 24) & 0xFF);

            // Convert from ARGB to RGBA
            argb <<= 8;
            argb |= a;

            writer.WriteValue(argb.ToString("X8"));
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return Color.White;
            }

            var value = reader.Value.ToString();

            if (value.Length == 6)
            {
                value += "FF";
            } else if(value.Length != 8)
            {
                return Color.White;
            }

            try
            {
                int rgba = int.Parse(value, NumberStyles.AllowHexSpecifier);

                var a = (byte) (rgba & 0xFF);

                // Convert from RGBA to ARGB
                rgba >>= 8;
                rgba |= a << 24;

                return Color.FromArgb(rgba);
            }
            catch
            {
                return Color.White;
            }

        }

        public override bool CanRead => true;
    }
}