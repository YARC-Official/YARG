using System;
using Newtonsoft.Json;
using YARG.Data;

namespace YARG.Serialization {
	public sealed class DiffPercentConverter : JsonConverter<DiffPercent> {
		public override DiffPercent ReadJson(JsonReader reader, Type objectType, DiffPercent existingValue, bool hasExistingValue, JsonSerializer serializer) {
			var str = serializer.Deserialize<string>(reader);
			char diff = str[0];
			float percent = float.Parse(str[1..]);

			return new DiffPercent {
				difficulty = DifficultyExtensions.FromChar(diff),
				percent = percent
			};
		}

		public override void WriteJson(JsonWriter writer, DiffPercent value, JsonSerializer serializer) {
			serializer.Serialize(writer, value.difficulty.ToChar() + value.percent.ToString());
		}
	}
}