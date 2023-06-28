using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace YARG.Util
{
    public static class TextureLoader
    {
        public static async UniTask<Texture2D> Load(string path, CancellationToken cancellationToken = default)
        {
            using var uwr = CreateRequest(path);
            uwr.downloadHandler = new DownloadHandlerTexture();

            try
            {
                await uwr.SendWebRequest().WithCancellation(cancellationToken);
                return DownloadHandlerTexture.GetContent(uwr);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public static async UniTask<Texture2D> LoadWithMips(string path, CancellationToken cancellationToken = default)
        {
            var original = await Load(path, cancellationToken);
            var mipmapped = new Texture2D(original.width, original.height, original.format, true);

            mipmapped.SetPixelData(original.GetRawTextureData<byte>(), 0);
            mipmapped.Apply(true, true);

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            return mipmapped;
        }

        private static UnityWebRequest CreateRequest(string path)
        {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			return new UnityWebRequest(new Uri(path));
#else
            return new UnityWebRequest(path);
#endif
        }
    }
}