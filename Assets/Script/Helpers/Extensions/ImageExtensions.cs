using System;
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
            FixedArray<byte>? buffer = null;

            TextureFormat gfxFormat;
            switch (image.Format)
            {
                case ImageFormat.Grayscale:
                    // Unity does not directly support Grayscale formats
                    buffer = GrayscaleToRGBJob.Run(image, image.Length);
                    gfxFormat = TextureFormat.RGB24;
                    break;
                case ImageFormat.GrayScale_Alpha:
                    // Unity does not directly support Grayscale formats
                    buffer = GrayscaleAlphaToRGBAJob.Run(image, image.Width * image.Height);
                    gfxFormat = TextureFormat.RGBA32;
                    break;
                case ImageFormat.RGB:
                    gfxFormat = TextureFormat.RGB24;
                    break;
                case ImageFormat.RGBA:
                    gfxFormat = TextureFormat.RGBA32;
                    break;
                case ImageFormat.DXT1:
                    gfxFormat = TextureFormat.DXT1;
                    break;
                case ImageFormat.DXT5:
                    gfxFormat = TextureFormat.DXT5;
                    break;
                default:
                    throw new ArgumentException("Unsupported image format");
            }

            var texture = new Texture2D(image.Width, image.Height, gfxFormat, mips);
            if (mips)
            {
                unsafe
                {
                    var arr = buffer != null
                        ? NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(buffer.Ptr, buffer.Length, Unity.Collections.Allocator.None)
                        : NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>((void*) image.Data, image.Length, Unity.Collections.Allocator.None);
                    var handle = AtomicSafetyHandle.Create();
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, handle);
                    texture.SetPixelData(arr, 0);
                    AtomicSafetyHandle.Release(handle);
                }
            }
            else
            {
                if (buffer != null)
                {
                    texture.LoadRawTextureData(buffer.IntPtr, buffer.Length);
                }
                else
                {
                    texture.LoadRawTextureData(image.Data, image.Length);
                }
            }
            buffer?.Dispose();
            texture.Apply();
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

        
        private readonly unsafe struct TextureBuffer
        {
            [NativeDisableUnsafePtrRestriction]
            public readonly byte* OriginalTexture;
            [NativeDisableUnsafePtrRestriction]
            public readonly byte* ResultTexture;

            public TextureBuffer(byte* originalTexture, byte* resultTexture)
            {
                OriginalTexture = originalTexture;
                ResultTexture = resultTexture;
            }
        }

        [BurstCompile]
        private readonly unsafe struct GrayscaleToRGBJob : IJobParallelFor
        {
            private const int PIXELSIZE = 3;
            public static FixedArray<byte> Run(YARGImage image, int numPixels)
            {
                var buffer = FixedArray<byte>.Alloc(PIXELSIZE * numPixels);
                var texture = new TextureBuffer((byte*) image.Data, buffer.Ptr);

                new GrayscaleToRGBJob(texture)
                    .Schedule(image.Length, 64)
                    .Complete();
                return buffer;
            }

            private readonly TextureBuffer _buffer;

            private GrayscaleToRGBJob(TextureBuffer buffer)
            {
                _buffer = buffer;
            }

            public readonly void Execute(int index)
            {
                int resultIndex = PIXELSIZE * index;

                var value = _buffer.OriginalTexture[index];

                _buffer.ResultTexture[resultIndex] = value;
                _buffer.ResultTexture[resultIndex + 1] = value;
                _buffer.ResultTexture[resultIndex + 2] = value;
            }
        }

        [BurstCompile]
        private readonly unsafe struct GrayscaleAlphaToRGBAJob : IJobParallelFor
        {
            private const int PIXELSIZE = 4;
            public static FixedArray<byte> Run(YARGImage image, int numPixels)
            {
                var buffer = FixedArray<byte>.Alloc(PIXELSIZE * numPixels);
                var texture = new TextureBuffer((byte*) image.Data, buffer.Ptr);

                new GrayscaleAlphaToRGBAJob(texture)
                    .Schedule(image.Length, 64)
                    .Complete();
                return buffer;
            }

            private readonly TextureBuffer _buffer;

            private GrayscaleAlphaToRGBAJob(TextureBuffer buffer)
            {
                _buffer = buffer;
            }

            public readonly void Execute(int index)
            {
                int originalIndex = index << 1;
                int resultIndex = index << 2;

                byte value = _buffer.OriginalTexture[originalIndex];
                byte alpha = _buffer.OriginalTexture[originalIndex + 1];

                _buffer.ResultTexture[resultIndex] = value;
                _buffer.ResultTexture[resultIndex + 1] = value;
                _buffer.ResultTexture[resultIndex + 2] = value;
                _buffer.ResultTexture[resultIndex + 3] = alpha;
            }
        }
    }
}