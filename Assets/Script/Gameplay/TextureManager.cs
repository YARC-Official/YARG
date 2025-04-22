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
        private float[]   _rawFft       = new float[FFT_SIZE * 2];
        private float[]   _rawWave      = new float[FFT_SIZE];

        private static int _soundTexId = Shader.PropertyToID("_Yarg_SoundTex");
        private static int _sourceIconId = Shader.PropertyToID("_Yarg_SourceIcon");
        private static int _albumCoverId = Shader.PropertyToID("_Yarg_AlbumCover");

        private const double MIN_DB = -100.0;
        private const double MAX_DB = -30.0;
        private const double DB_RANGE = MAX_DB - MIN_DB;
        private const int FFT_SIZE_LOG = 11 /* aka log2(2048) */;
        private const int FFT_SIZE = 1 << FFT_SIZE_LOG;
        private const int FFT_TEXTURE_WIDTH = 512;

        // This isn't used because it produces much too small magnitudes
        private const double MAGNITUDE_SCALE = 1.0 / (FFT_SIZE_LOG - 2);

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
                // divide by 4 to get 512 texture bins
                _soundTexture = new Texture2D(FFT_TEXTURE_WIDTH, 2, TextureFormat.R8, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };
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
            GameManager.GetMixerFFTData(_rawFft, FFT_SIZE_LOG, true);
            GameManager.GetMixerSampleData(_rawWave);

            // Massage complex FFT data into real magnitudes
            // We go by twos because the real and complex components are interleaved
            // TODO: Integrate this into the loop below rather than doing it separately
            for (int i = 0; i < FFT_SIZE; i = i + 2)
            {
                // Just stop if we exceed the output buffer
                if (i / 2 >= _fft.Length)
                {
                    break;
                }

                _rawFft[i] /= 2;
                _rawFft[i + 1] /= 2;

                // This is a bad way to calculate a hypotenuse, but whatever
                var magnitude = Math.Sqrt(_rawFft[i] * _rawFft[i] + _rawFft[i + 1] * _rawFft[i + 1]); // * MAGNITUDE_SCALE;
                _fft[i / 2] = (float) _prevFft[i / 2] * fftSmoothingFactor + (float) magnitude * (1.0f - fftSmoothingFactor);
            }

            // This is getting stupid now
            for (int i = 0; i < _wave.Length; i++)
            {
                // How do we tell how many channels there actually are?
                // I guess we'll just assume it's stereo for now
                // Normally one would halve the value of each channel and add them together, but we're peaking
                // at only 0.35, so...

                // The multiplier is just an empirical fudge factor, I need to understand why it is necessary :(
                _wave[i] = _rawWave[i*2] * 0.6f;
                _wave[i] += _rawWave[(i*2)+1] * 0.6f;
            }

            // TODO: Understand why the frequency rolloff seems to be different between BASS and Chrome

            for (int i = 0; i < FFT_TEXTURE_WIDTH; ++i)
            {
                // var fft_value = _fft[i] * (1.0f - fftSmoothingFactor) + _prevFft[i] * fftSmoothingFactor;
                _prevFft[i] = _fft[i];
                // Avoid 0
                double magnitude = _fft[i] + 1e-20;
                // logarithmic scale
                double db = 20.0 * Math.Log10(magnitude);
                // clamp to range
                db = Math.Max(MIN_DB, Math.Min(db, MAX_DB));
                // normalize
                double normalized = ((db - MIN_DB) / DB_RANGE) * 255;

                // set spectrum data in the first row
                pixelData[i] = (byte) Math.Round(normalized);
                // waveform data in the second row
                // I can't find any wave smoothing in the FF or ShaderToy code, so this is commented out
                // var wave = _wave[i] * (1.0f - waveSmoothingFactor) + _prevWave[i] * waveSmoothingFactor;
                _prevWave[i] = _wave[i];
                // pixelData[FFT_TEXTURE_WIDTH + i] = (byte) (255.0f * (1.0f + wave) / 2.0f);
                pixelData[FFT_TEXTURE_WIDTH + i] = (byte) Math.Max(0, Math.Min(255, 128 * (_wave[i] + 1)));
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
