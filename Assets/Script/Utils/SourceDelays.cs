namespace YARG.Utils {
	public static class SourceDelays {
		public static float GetSourceDelay(string source) {
			// Hardcode a delay for certain games (such as RB1).
			// There may be a chance that I am reading the MIDI or something
			// incorrectly. This is the fix for now, and it seems to work for 
			// the most part.			
			return source switch {
				"rb1" or "rb1dlc" => 0.15f,
				_ => 0f
			};
		}
	}
}