using System.Collections.Generic;
using YARG.Input;

namespace YARG {
	public static class PlayerManager {
		public class Player {
			public string name;
			public InputStrategy inputStrategy;
		}

		public static int nextPlayerIndex = 1;
		public static List<Player> players = new();
	}
}