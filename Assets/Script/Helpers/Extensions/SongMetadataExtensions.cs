using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using YARG.Core.Song;
using YARG.Serialization;

namespace YARG.Helpers.Extensions
{
    public static class SongMetadataExtensions
    {
        public static async UniTask SetRawImageToAlbumCover(this SongMetadata songMetadata,
            RawImage rawImage, CancellationToken cancellationToken)
        {
            if (songMetadata.IniData != null)
                await LoadSongIniCover(songMetadata.Directory, rawImage, cancellationToken);
            else
            {
                byte[] file = null;
                await UniTask.RunOnThreadPool(() => file = songMetadata.RBData.LoadImgFile());

                if (file != null)
                    await LoadRbConCover(file, rawImage, cancellationToken);
                else
                {
                    rawImage.texture = null;
                    rawImage.color = Color.clear;
                }
            }
        }

        private static async UniTask LoadSongIniCover(string directory,
            RawImage rawImage, CancellationToken cancellationToken)
        {
            string[] possiblePaths =
            {
                "album.png", "album.jpg", "album.jpeg",
            };

            // Load album art from one of the paths
            Texture2D texture = null;
            foreach (string path in possiblePaths)
            {
                string fullPath = Path.Combine(directory, path);

                if (File.Exists(fullPath))
                {
                    texture = await TextureHelper.Load(fullPath, cancellationToken);
                    break;
                }
            }

            if (texture != null)
            {
                // Set album cover
                rawImage.texture = texture;
                rawImage.color = Color.white;
                rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
            else
            {
                rawImage.texture = null;
                rawImage.color = Color.clear;
            }
        }

        private static async UniTask LoadRbConCover(byte[] file,
            RawImage rawImage, CancellationToken cancellationToken)
        {
            XboxImageSettings settings = null;

            // The overload with cancellation support requires an async function.
            // ReSharper disable once MethodSupportsCancellation
            await UniTask.RunOnThreadPool(() => settings = XboxImageTextureGenerator.GetTexture(file, cancellationToken));

            if (settings == null)
                return;

            // Choose the right format
            bool isDXT1 = settings.bitsPerPixel == 0x04 && settings.format == 0x08;
            var format = isDXT1 ? GraphicsFormat.RGBA_DXT1_SRGB : GraphicsFormat.RGBA_DXT5_SRGB;

            // Load the texture
            var texture = new Texture2D(settings.width, settings.height, format, TextureCreationFlags.None);
            unsafe
            {
                fixed (byte* data = file)
                {
                    texture.LoadRawTextureData((IntPtr) (data + 32), file.Length - 32);
                }
            }
            texture.Apply();

            // Set it
            rawImage.texture = texture;
            rawImage.color = Color.white;
            rawImage.uvRect = new Rect(0f, 0f, 1f, -1f);
        }
    }
}