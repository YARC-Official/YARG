using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace YARG.Util {
	public static class TextureLoader {
		public static async UniTask<Texture2D> Load(string path, CancellationToken cancellationToken = default) {
			using var uwr = CreateRequest(path);

			try {
				await uwr.SendWebRequest().WithCancellation(cancellationToken);
				return DownloadHandlerTexture.GetContent(uwr);
			} catch (OperationCanceledException) {
				return null;
			}
		}

		private static UnityWebRequest CreateRequest(string path) {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			return UnityWebRequestTexture.GetTexture(new System.Uri(path));
#else
			return UnityWebRequestTexture.GetTexture(path);
#endif
		}
	}
}