using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Song;

namespace YARG.Gameplay
{
    /// <summary>
    /// A point of integration between GamePlay state
    /// and venue materials
    /// </summary>
    public class TextureManager : GameplayBehaviour
    {
        private Texture2D _sourceIcon;
        private Texture2D _albumCover = null;
        private Texture2D _soundTexture = null;
        private float[] fft = new float[1024];
        private float[] wave = new float[1024];

        private static int _soundTexId = Shader.PropertyToID("_Yarg_SoundTex");
        private static int _sourceIconId = Shader.PropertyToID("_Yarg_SourceIcon");
        private static int _albumCoverId = Shader.PropertyToID("_Yarg_AlbumCover");


        private void Start()
        {
            _sourceIcon = SongSources.SourceToIcon(GameManager.Song.Source).texture;
        }

        protected Texture2D getAlbumArt()
        {
            if (_albumCover == null)
            {
                var image = GameManager.Song.LoadAlbumData();
                _albumCover = image.LoadTexture(false);
            }
            return _albumCover;
        }

        protected Texture2D getSoundTexture()
        {
            if (_soundTexture == null)
            {
                _soundTexture = new Texture2D(512, 2, TextureFormat.R8, false);
            }
            return _soundTexture;
        }

        public void processMaterial(Material m)
        {
            if (m.HasTexture(_sourceIconId))
            {
                m.SetTexture(_sourceIconId, _sourceIcon);
            }
            if (m.HasTexture(_soundTexId))
            {
                m.SetTexture(_soundTexId, getSoundTexture());
            }
            if (m.HasTexture(_albumCoverId))
            {
                m.SetTexture(_albumCoverId, getAlbumArt());
            }
        }

        public async void FixedUpdate()
        {
            if (_soundTexture != null)
            {
                var pd = _soundTexture.GetPixelData<Byte>(0);

                await UniTask.RunOnThreadPool(() =>
                {
                    GameManager.GetMixerFFTData(fft, 9 /* 512 */, false);
                    GameManager.GetMixerSampleData(wave);

                    double minDb = -100.0;
                    double maxDb = -30.0;
                    double range = maxDb - minDb;


                    for (int i = 0; i < 512; ++i)
                    {
                        var fft_value = fft[i];
                        // Avoid 0
                        double magnitude = fft_value + 1e-20;
                        // logarithmic scale
                        double db = 20.0 * Math.Log10(magnitude);
                        // clamp to range
                        db = Math.Max(minDb, Math.Min(db, maxDb));
                        // normalize
                        double normalized = ((db - minDb) / range) * 255;

                        // set spectrum data in the first row
                        pd[i] = (byte) Math.Round(normalized);
                        // waveform data in the second row
                        pd[512 + i] = (byte) (255.0f * (1.0f + wave[i]) / 2.0f);

                    }
                });

                _soundTexture.Apply(false, false);

            }
        }
    }
}
