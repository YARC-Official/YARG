using System;
using Cysharp.Threading.Tasks;
using Unity.Collections;
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

        // Smoothing factor (adjust to taste)
        [Range(0.0f, 0.95f)]
        public float fftSmoothingFactor = 0.8f;
        [Range(0.0f, 0.95f)]
        public float waveSmoothingFactor = 0.5f;

        private Texture2D _sourceIcon;
        private Texture2D _albumCover = null;
        private Texture2D _soundTexture = null;
        private float[] _fft = new float[FFT_SIZE / 2];
        private float[] _wave = new float[FFT_TEXTURE_WIDTH];
        private float[] _prevFft = new float[FFT_SIZE / 2];
        private float[] _prevWave = new float[FFT_TEXTURE_WIDTH];

        private static int _soundTexId = Shader.PropertyToID("_Yarg_SoundTex");
        private static int _sourceIconId = Shader.PropertyToID("_Yarg_SourceIcon");
        private static int _albumCoverId = Shader.PropertyToID("_Yarg_AlbumCover");

        private const double MIN_DB = -100.0;
        private const double MAX_DB = -30.0;
        private const double DB_RANGE = MAX_DB - MIN_DB;
        private const int FFT_SIZE_LOG = 10 /* aka log2(1024) */;
        private const int FFT_SIZE = 1 << FFT_SIZE_LOG;
        private const int FFT_TEXTURE_WIDTH = 512;

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
                _soundTexture = new Texture2D(FFT_TEXTURE_WIDTH, 2, TextureFormat.R8, false);
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

        private void UpdateFFT(NativeArray<byte> pixelData)
        {
            GameManager.GetMixerFFTData(_fft, FFT_SIZE_LOG, false);
            GameManager.GetMixerSampleData(_wave);

            for (int i = 0; i < FFT_TEXTURE_WIDTH; ++i)
            {
                var fft_value = _fft[i] * (1.0f - fftSmoothingFactor) + _prevFft[i] * fftSmoothingFactor;
                _prevFft[i] = _fft[i];
                // Avoid 0
                double magnitude = fft_value + 1e-20;
                // logarithmic scale
                double db = 20.0 * Math.Log10(magnitude);
                // clamp to range
                db = Math.Max(MIN_DB, Math.Min(db, MAX_DB));
                // normalize
                double normalized = ((db - MIN_DB) / DB_RANGE) * 255;

                // set spectrum data in the first row
                pixelData[i] = (byte) Math.Round(normalized);
                // waveform data in the second row
                var wave = _wave[i] * (1.0f - waveSmoothingFactor) + _prevWave[i] * waveSmoothingFactor;
                _prevWave[i] = _wave[i];
                pixelData[FFT_TEXTURE_WIDTH + i] = (byte) (255.0f * (1.0f + wave) / 2.0f);
            }
        }

        public async void FixedUpdate()
        {
            if (_soundTexture != null)
            {
                var pixelData = _soundTexture.GetPixelData<Byte>(0);
                await UniTask.RunOnThreadPool(() =>
                {
                    UpdateFFT(pixelData);
                });
                _soundTexture.Apply(false, false);
            }
        }
    }
}
