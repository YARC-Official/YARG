using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Helpers.Extensions
{
    public static class ImageExtensions
    {
        public static Texture2D LoadTexture(this YARGImage image, bool mips)
        {
            var gfxFormat = image.Format switch
            {
                ImageFormat.RGB or ImageFormat.Grayscale => TextureFormat.RGB24,
                ImageFormat.RGBA or ImageFormat.GrayScale_Alpha => TextureFormat.RGBA32,
                ImageFormat.DXT1 => TextureFormat.DXT1,
                ImageFormat.DXT5 => TextureFormat.DXT5,
                _ => throw new ArgumentException("Unsupported image format"),
            };

            var texture = new Texture2D(image.Width, image.Height, gfxFormat, mips);
            unsafe
            {
                var data = texture.GetPixelData<byte>(0);
                var ptr = (byte*)data.GetUnsafeReadOnlyPtr();
                switch (image.Format)
                {
                    case ImageFormat.Grayscale:
                        // Unity does not directly support Grayscale formats
                        new GrayscaleToRGBJob(image.Data, ptr)
                            .Schedule(image.Width * image.Height, 128)
                            .Complete();
                        break;
                    case ImageFormat.GrayScale_Alpha:
                        // Unity does not directly support Grayscale formats
                        new GrayscaleAlphaToRGBAJob(image.Data, ptr)
                            .Schedule(image.Width * image.Height, 128)
                            .Complete();
                        break;
                    case ImageFormat.DXT1:
                    case ImageFormat.DXT5:
                        for (int i = 0; i < data.Length; i += 2)
                        {
                            ptr[i] = image.Data[i + 1];
                            ptr[i + 1] = image.Data[i];
                        }
                        break;
                    default:
                        Unsafe.CopyBlock(ptr, image.Data, (uint) data.Length);
                        break;

                }
            }
            texture.Apply(true, true);
            return texture;
        }

        private static SongEntry _current = null;
        public static async void LoadAlbumCover(this RawImage rawImage, SongEntry songEntry, CancellationToken cancellationToken)
        {
            _current = songEntry;
            using var image = await UniTask.RunOnThreadPool(songEntry.LoadAlbumData);
            // Everything that happens after this conditional should only happen *once*
            // There's no reason to create and destroy texture objects if they're just gonna be overriden
            if (_current != songEntry)
            {
                return;
            }

            lock (rawImage)
            {
                // Dispose of the old texture (prevent memory leaks)
                UnityEngine.Object.Destroy(rawImage.texture);

                if (image != null && !cancellationToken.IsCancellationRequested)
                {
                    rawImage.texture = image.LoadTexture(false);
                    rawImage.uvRect = new Rect(0f, 0f, 1f, -1f);
                    rawImage.color = Color.white;
                }
                else
                {
                    rawImage.texture = null;
                    rawImage.color = Color.clear;
                }
            }
        }

        [BurstCompile]
        private readonly unsafe struct GrayscaleToRGBJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly byte* _originalTexture;
            [NativeDisableUnsafePtrRestriction]
            private readonly byte* _resultTexture;

            public GrayscaleToRGBJob(byte* originalTexture, byte* resultTexture)
            {
                _originalTexture = originalTexture;
                _resultTexture = resultTexture;
            }

            public readonly void Execute(int index)
            {
                const int PIXELSIZE = 3;
                int resultIndex = PIXELSIZE * index;

                var value = _originalTexture[index];

                _resultTexture[resultIndex] = value;
                _resultTexture[resultIndex + 1] = value;
                _resultTexture[resultIndex + 2] = value;
            }
        }

        [BurstCompile]
        private readonly unsafe struct GrayscaleAlphaToRGBAJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly byte* _originalTexture;
            [NativeDisableUnsafePtrRestriction]
            private readonly byte* _resultTexture;

            public GrayscaleAlphaToRGBAJob(byte* originalTexture, byte* resultTexture)
            {
                _originalTexture = originalTexture;
                _resultTexture = resultTexture;
            }

            public readonly void Execute(int index)
            {
                int originalIndex = index << 1;
                int resultIndex = index << 2;

                byte value = _originalTexture[originalIndex];
                byte alpha = _originalTexture[originalIndex + 1];

                _resultTexture[resultIndex] = value;
                _resultTexture[resultIndex + 1] = value;
                _resultTexture[resultIndex + 2] = value;
                _resultTexture[resultIndex + 3] = alpha;
            }
        }
    }
}