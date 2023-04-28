using YARG.Data;

namespace YARG {
	public static class Constants {
		public static readonly YargVersion VERSION_TAG = YargVersion.Parse("v9-Acc-Test");

		public const float HIT_MARGIN = 0.075f;
		public const float HIT_MARGIN_PERFECT = 0.36f;
		public const float HIT_MARGIN_GREAT = 0.54f;
		public const float HIT_MARGIN_GOOD = 0.78f;
		public const float STRUM_LENIENCY = 0.035f;
		public const bool ANCHORING = true;
		public const bool INFINITE_FRONTEND = false;
		public const bool ANCHOR_CHORD_HOPO = true;
		public const int EXTRA_ALLOWED_GHOSTS = 0;
	}
}