using TMPro;
using UnityEngine;

namespace YARG.Pools {
	public class LyricPool : Pool {
		public Transform AddLyric(string text, float x) {
			var poolable = Add("lyric", new Vector3(x, 0f, -0.68f));
			poolable.GetComponent<TextMeshPro>().text = text;

			return poolable.transform;
		}
	}
}