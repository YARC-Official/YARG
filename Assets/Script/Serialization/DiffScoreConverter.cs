using System;
using System.Globalization;
using Newtonsoft.Json;
using YARG.Data;

namespace YARG.Serialization {
	public sealed class DiffScoreConverter : JsonConverter<DiffScore> {
		public override DiffScore ReadJson(JsonReader reader, Type objectType, DiffScore existingValue, bool hasExistingValue, JsonSerializer serializer) {
			var str = serializer.Deserialize<string>(reader);
			var spl = str.Split('s');
			char diff = spl[0][0];
			int score = int.Parse(spl[0][1..], CultureInfo.InvariantCulture);
			int stars = int.Parse(spl[1]);

			return new DiffScore {
				difficulty = DifficultyExtensions.FromChar(diff),
				score = score,
				stars = stars
			};
		}

		public override void WriteJson(JsonWriter writer, DiffScore value, JsonSerializer serializer) {
			serializer.Serialize(writer, $"{value.difficulty.ToChar()}{value.score}s{value.stars}");
		}
	}
}