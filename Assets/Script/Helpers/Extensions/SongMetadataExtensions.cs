using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Song;

namespace YARG.Helpers.Extensions
{
    public static class SongEntryExtensions
    {
        public static async UniTask LoadAlbumCover(this RawImage rawImage,
            SongEntry SongEntry, CancellationToken cancellationToken)
        {
            var file = await UniTask.RunOnThreadPool(SongEntry.LoadAlbumData);
            if (file != null && !cancellationToken.IsCancellationRequested)
            {
                if (SongEntry.SubType >= EntryType.ExCON)
                {
                    rawImage.texture = LoadRBConCoverTexture(file);
                    rawImage.uvRect = new Rect(0f, 0f, 1f, -1f);
                }
                else if (LoadSongIniCoverTexture(file, out var texture))
                {
                    rawImage.texture = texture;
                    rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                }
                rawImage.color = Color.white;
            }
            else
            {
                rawImage.texture = null;
                rawImage.color = Color.clear;
            }
        }

        private static bool LoadSongIniCoverTexture(byte[] file, out Texture2D texture)
        {
            // Width & height get overwritten
            texture = new Texture2D(2, 2);
            return texture.LoadImage(file);
        }

        private static Texture2D LoadRBConCoverTexture(byte[] file)
        {
            byte bitsPerPixel = file[1];
            int format = BinaryPrimitives.ReadInt32LittleEndian(new(file, 2, 4));
            int width = BinaryPrimitives.ReadInt16LittleEndian(new(file, 7, 2));
            int height = BinaryPrimitives.ReadInt16LittleEndian(new(file, 9, 2));

            bool isDXT1 = bitsPerPixel == 0x04 && format == 0x08;
            var gfxFormat = isDXT1 ? GraphicsFormat.RGBA_DXT1_SRGB : GraphicsFormat.RGBA_DXT5_SRGB;

            var texture = new Texture2D(width, height, gfxFormat, TextureCreationFlags.None);
            unsafe
            {
                fixed (byte* data = file)
                {
                    texture.LoadRawTextureData((IntPtr) (data + 32), file.Length - 32);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}