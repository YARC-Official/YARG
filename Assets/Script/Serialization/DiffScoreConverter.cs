using System;
using System.Globalization;
using Newtonsoft.Json;
using YARG.Data;

namespace YARG.Serialization {
	public sealed class DiffScoreConverter : JsonConverter<DiffScore> {
		public override DiffScore ReadJson(JsonReader reader, Type objectType, DiffScore existingValue, bool hasExistingValue, JsonSerializer serializer) {
			var str = serializer.Deserialize<string>(reader);
			char diff = str[0];
			int score = int.Parse(str[1..], CultureInfo.InvariantCulture);

			return new DiffScore {
				difficulty = DifficultyExtensions.FromChar(diff),
				score = score
			};
		}

		public override void WriteJson(JsonWriter writer, DiffScore value, JsonSerializer serializer) {
			serializer.Serialize(writer, value.difficulty.ToChar() + value.score.ToString());
		}
	}
}