using YARG.Data;

namespace YARG {
	public static class Constants {
		public static readonly YargVersion VERSION_TAG = YargVersion.Parse("v0.9.1");

		public const float HIT_MARGIN = 0.095f;
		public const float STRUM_LENIENCY = 0.065f;
		public const bool ANCHORING = true;
		public const bool INFINITE_FRONTEND = false;
		public const bool ANCHOR_CHORD_HOPO = true;
		public const bool ANTI_GHOSTING = true;
		public const int EXTRA_ALLOWED_GHOSTS = 0;
	}
}