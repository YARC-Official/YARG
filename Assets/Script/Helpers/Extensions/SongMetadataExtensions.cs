using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Core.Song;

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
                byte[] file = await UniTask.RunOnThreadPool(songMetadata.RBData!.LoadImgFile);
                if (file == null)
                {
                    rawImage.texture = null;
                    rawImage.color = Color.clear;
                    return;
                }

                LoadRBConCover(file, rawImage, cancellationToken);
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

        private static void LoadRBConCover(byte[] file,
            RawImage rawImage, CancellationToken token)
        {
            for (int i = 32; i < file.Length; i += 2)
            {
                if (token.IsCancellationRequested)
                    return;

                (file[i + 1], file[i]) = (file[i], file[i + 1]);
            }

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

            rawImage.texture = texture;
            rawImage.color = Color.white;
            rawImage.uvRect = new Rect(0f, 0f, 1f, -1f);
        }

        public static void LoadAudio(this SongMetadata song, IAudioManager manager, float speed, params SongStem[] ignoreStems)
        {
            if (song.IniData != null)
            {
                LoadIniAudio(manager, song.Directory, speed, ignoreStems);
            }
            else
            {
                LoadRBCONAudio(song.RBData, manager, speed, ignoreStems);
            }
        }

        public static async UniTask<bool> LoadPreviewAudio(this SongMetadata song, IAudioManager manager, float speed)
        {
            if (song.IniData != null)
            {
                string directory = song.Directory;
                string previewBase = Path.Combine(directory, "preview");
                foreach (var ext in manager.SupportedFormats)
                {
                    string previewFile = previewBase + ext;
                    if (File.Exists(previewFile))
                    {
                        await UniTask.RunOnThreadPool(() => manager.LoadCustomAudioFile(previewFile, 1));
                        return true;
                    }
                }
                await UniTask.RunOnThreadPool(() => LoadIniAudio(manager, directory, speed, SongStem.Crowd));
            }
            else
                await UniTask.RunOnThreadPool(() => LoadRBCONAudio(song.RBData, manager, speed, SongStem.Crowd));
            return false;
        }

        private static void LoadIniAudio(IAudioManager manager, string directory, float speed, params SongStem[] ignoreStems)
        {
            var stems = AudioHelpers.GetSupportedStems(directory);
            foreach (var stem in ignoreStems)
                stems.Remove(stem);
            manager.LoadSong(stems, speed);
        }

        private static void LoadRBCONAudio(SongMetadata.IRBCONMetadata rbData, IAudioManager manager, float speed, params SongStem[] ignoreStems)
        {
            var rbmetadata = rbData.SharedMetadata;

            List<MoggStemMap> stemMaps = new();
            if (rbmetadata.DrumIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (rbmetadata.DrumIndices.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        stemMaps.Add(new(SongStem.Drums, rbmetadata.DrumIndices, rbmetadata.DrumStemValues));
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..3], rbmetadata.DrumStemValues[2..6]));
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..2], rbmetadata.DrumStemValues[2..4]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[2..4], rbmetadata.DrumStemValues[4..8]));
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..3], rbmetadata.DrumStemValues[2..6]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[3..5], rbmetadata.DrumStemValues[6..10]));
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..2], rbmetadata.DrumStemValues[0..4]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[2..4], rbmetadata.DrumStemValues[4..8]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[4..6], rbmetadata.DrumStemValues[8..12]));
                        break;
                }
            }

            if (rbmetadata.BassIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Bass))
                stemMaps.Add(new(SongStem.Bass, rbmetadata.BassIndices, rbmetadata.BassStemValues));

            if (rbmetadata.GuitarIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Guitar))
                stemMaps.Add(new(SongStem.Guitar, rbmetadata.GuitarIndices, rbmetadata.GuitarStemValues));

            if (rbmetadata.KeysIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Keys))
                stemMaps.Add(new(SongStem.Keys, rbmetadata.KeysIndices, rbmetadata.KeysStemValues));

            if (rbmetadata.VocalsIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Vocals))
                stemMaps.Add(new(SongStem.Vocals, rbmetadata.VocalsIndices, rbmetadata.VocalsStemValues));

            if (rbmetadata.TrackIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Song))
                stemMaps.Add(new(SongStem.Song, rbmetadata.TrackIndices, rbmetadata.TrackStemValues));

            if (rbmetadata.CrowdIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Crowd))
                stemMaps.Add(new(SongStem.Crowd, rbmetadata.CrowdIndices, rbmetadata.CrowdStemValues));

            manager.LoadMogg(rbData.GetMoggStream(), stemMaps, speed);
        }
    }
}