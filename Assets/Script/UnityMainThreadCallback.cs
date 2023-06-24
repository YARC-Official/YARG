using System;
using System.Collections.Generic;
using UnityEngine;

namespace YARG {
	public class UnityMainThreadCallback : MonoBehaviour {

		private static readonly Queue<Action> CallbackQueue = new();

		private void Update() {
			lock (CallbackQueue) {
				while (CallbackQueue.Count > 0) {
					CallbackQueue.Dequeue().Invoke();
				}
			}
		}

		public static void QueueEvent(Action action) {
			lock (CallbackQueue) {
				CallbackQueue.Enqueue(action);
			}
		}
	}
}