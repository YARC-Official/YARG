using UnityEngine;

namespace YARG.Util {
	[CreateAssetMenu(fileName = "TestPlayInfo", menuName = "YARG/TestPlayInfo", order = 1)]
	public class TestPlayInfo : ScriptableObject {
		[HideInInspector]
		public bool TestPlayMode;
		[HideInInspector]
		public string TestPlaySongHash;

		public bool NoBotsMode;
	}
}