using System.Collections.Concurrent;
using System.Linq;
using TMPro;
using UnityEngine;

namespace YARG.Server {
	public partial class Host : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI outputText;

		private ConcurrentQueue<string> threadPrint = new();

		private void Update() {
			// Print!
			while (threadPrint.Count > 0) {
				if (!threadPrint.TryDequeue(out var str)) {
					continue;
				}

				outputText.text += "\n" + str;

				// Deal with overflow
				var split = outputText.text.Split("\n").ToList();
				if (split.Count > 20) {
					split.RemoveAt(0);
					outputText.text = string.Join("\n", split);
				}
			}
		}

		private void Log(string str) {
			threadPrint.Enqueue(str);
		}
	}
}