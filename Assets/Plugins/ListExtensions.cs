using System.Collections.Generic;

namespace UnityEngine {
	public static class ListExtensions {
		/*
		 *	IList Utils
		 */

		// Thanks! https://stackoverflow.com/questions/273313/randomize-a-listt
		public static void Shuffle<T>(this IList<T> list) {
			int n = list.Count;
			while (n > 1) {
				n--;
				int k = Random.Range(0, n + 1);
				(list[k], list[n]) = (list[n], list[k]);
			}
		}

		public static void Shuffle<T>(this IList<T> list, System.Random random) {
			int n = list.Count;
			while (n > 1) {
				n--;
				int k = random.Next(0, n + 1);
				(list[k], list[n]) = (list[n], list[k]);
			}
		}

		public static T Pick<T>(this IList<T> list) {
			return list[Random.Range(0, list.Count)];
		}

		public static T Pick<T>(this IList<T> list, System.Random random) {
			return list[random.Next(0, list.Count)];
		}
	}
}