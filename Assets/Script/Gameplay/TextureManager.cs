using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
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
        private float[] fft = new float[FFT_SIZE * 2];
        private float[] wave = new float[FFT_SIZE * 2];

        private static int _soundTexId = Shader.PropertyToID("_Yarg_SoundTex");
        private static int _sourceIconId = Shader.PropertyToID("_Yarg_SourceIcon");
        private static int _albumCoverId = Shader.PropertyToID("_Yarg_AlbumCover");

        private const double MIN_DB = -100.0;
        private const double MAX_DB = -30.0;
        private const double DB_RANGE = MAX_DB - MIN_DB;
        private const int FFT_SIZE_LOG = 9 /* aka log2(512) */;
        private const int FFT_SIZE = 1 << FFT_SIZE_LOG;

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
                // first row is FFT data
                // second is waveform data
                _soundTexture = new Texture2D(FFT_SIZE, 2, TextureFormat.R8, false);
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

        private async void UpdateFFT()
        {
            if (_soundTexture == null)
            { 
                return; 
            }
            var pd = _soundTexture.GetPixelData<Byte>(0);

            await UniTask.RunOnThreadPool(() =>
            {
                GameManager.GetMixerFFTData(fft, FFT_SIZE_LOG, false);
                GameManager.GetMixerSampleData(wave);

                for (int i = 0; i < FFT_SIZE; ++i)
                {
                    var fft_value = fft[i];
                    // Avoid 0
                    double magnitude = fft_value + 1e-20;
                    // logarithmic scale
                    double db = 20.0 * Math.Log10(magnitude);
                    // clamp to range
                    db = Math.Max(MIN_DB, Math.Min(db, MAX_DB));
                    // normalize
                    double normalized = ((db - MIN_DB) / DB_RANGE) * 255;

                    // set spectrum data in the first row
                    pd[i] = (byte)Math.Round(normalized);
                    // waveform data in the second row
                    pd[FFT_SIZE + i] = (byte)(255.0f * (1.0f + wave[i]) / 2.0f);
                }
            });

            _soundTexture.Apply(false, false);
        }

        public void FixedUpdate()
        {
            UpdateFFT();
        }
    }
}
