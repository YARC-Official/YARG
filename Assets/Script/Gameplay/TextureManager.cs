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
        public float FFTSmoothingFactor = 0.8f;
        [Range(0.0f, 0.95f)]
        public float WaveSmoothingFactor = 0.5f;

        private Texture2D _sourceIcon = null;
        private Texture2D _albumCover = null;
        private Texture2D _soundTexture = null;
        private float[] _fft = new float[FFT_SIZE / 2];
        private float[] _wave = new float[FFT_TEXTURE_WIDTH];
        private float[] _prevFft = new float[FFT_SIZE / 2];
        private float[] _rawFft = new float[FFT_SIZE * 2];
        private float[] _rawWave = new float[FFT_SIZE];

        private static int _soundTexId = Shader.PropertyToID("_Yarg_SoundTex");
        private static int _sourceIconId = Shader.PropertyToID("_Yarg_SourceIcon");
        private static int _albumCoverId = Shader.PropertyToID("_Yarg_AlbumCover");

        private const double MIN_DB = -100.0;
        private const double MAX_DB = -30.0;
        private const double DB_RANGE = MAX_DB - MIN_DB;
        private const int FFT_SIZE_LOG = 11 /* aka log2(2048) */;
        private const int FFT_SIZE = 1 << FFT_SIZE_LOG;
        private const int FFT_TEXTURE_WIDTH = 512;

        // TODO: Get the number of active channels from the mixer instead of assuming
        //  Note that this won't _break_ if there are more channels, it will just make
        //  wave shader output look weird on songs that have multichannel audio (or mono, for that matter)
        private const int AUDIO_CHANNELS = 2;
        // You would expect this to be 1 / AUDIO_CHANNELS, but we need a little bump for some as yet
        // to be understood reason
        private const float PER_CHANNEL_MULTIPLIER = 0.6f;

        private Texture2D GetSourceIcon()
        {
            if (_sourceIcon == null)
            {
                _sourceIcon = SongSources.SourceToIcon(GameManager.Song.Source).texture;
            }
            return _sourceIcon;
        }

        protected Texture2D GetAlbumArt()
        {
            if (_albumCover == null)
            {
                var image = GameManager.Song.LoadAlbumData();
                _albumCover = image.LoadTexture(false);
            }
            return _albumCover;
        }

        protected Texture2D GetSoundTexture()
        {
            if (_soundTexture == null)
            {
                // first row is FFT data
                // second is waveform data
                // divide by 4 to get 512 texture bins
                _soundTexture = new Texture2D(FFT_TEXTURE_WIDTH, 2, TextureFormat.R8, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };
            }
            return _soundTexture;
        }

        public void ProcessMaterial(Material m)
        {
            if (m.HasTexture(_sourceIconId))
            {
                m.SetTexture(_sourceIconId, GetSourceIcon());
            }
            if (m.HasTexture(_soundTexId))
            {
                m.SetTexture(_soundTexId, GetSoundTexture());
            }
            if (m.HasTexture(_albumCoverId))
            {
                m.SetTexture(_albumCoverId, GetAlbumArt());
            }
        }

        private void UpdateFFT(NativeArray<byte> pixelData)
        {
            GameManager.GetMixerFFTData(_rawFft, FFT_SIZE_LOG, true);
            GameManager.GetMixerSampleData(_rawWave);

            // Massage complex FFT data into real magnitudes
            // We go by twos because the real and complex components are interleaved
            for (int i = 0; i < _fft.Length * 2; i += 2)
            {
                _rawFft[i] *= 0.5f;
                _rawFft[i + 1] *= 0.5f;

                // This is an inaccurate way of calculating a hypotenuse, but it doesn't seem to matter for this purpose
                var magnitude = MathF.Sqrt(_rawFft[i] * _rawFft[i] + _rawFft[i + 1] * _rawFft[i + 1]);
                _fft[i / 2] = _prevFft[i / 2] * FFTSmoothingFactor + magnitude * (1.0f - FFTSmoothingFactor);
            }

            // TODO: Understand why the frequency rolloff seems to be different between BASS and Chrome/Firefox

            for (int i = 0; i < FFT_TEXTURE_WIDTH; ++i)
            {
                // Save the old data
                _prevFft[i] = _fft[i];
                // Avoid 0
                double magnitude = _fft[i] + 1e-20;
                // logarithmic scale
                double db = 20.0 * Math.Log10(magnitude);
                // clamp to range
                db = Math.Max(MIN_DB, Math.Min(db, MAX_DB));
                // normalize
                double normalized = ((db - MIN_DB) / DB_RANGE) * 255;

                // Process the wave data
                _wave[i] = (_rawWave[i * AUDIO_CHANNELS] + _rawWave[(i * AUDIO_CHANNELS) + 1]) * PER_CHANNEL_MULTIPLIER;

                // set spectrum data in the first row
                pixelData[i] = (byte)Math.Round(normalized);
                // waveform data in the second row
                pixelData[FFT_TEXTURE_WIDTH + i] = (byte)Math.Max(0, Math.Min(255, 128 * (_wave[i] + 1)));
            }
        }

        public async void Update()
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
