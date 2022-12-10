using System.Collections.Generic;
using YARG.Input;

namespace YARG {
	public static class PlayerManager {
		public class Player {
			public InputStrategy inputStrategy;
		}

		public static List<Player> players = new();
	}
}