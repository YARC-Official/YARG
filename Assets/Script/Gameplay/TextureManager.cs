using System;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using YARG.Core.Parsing;
using YARG.Core.Venue;
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

        public Texture2D DefaultAlbumCover;

        private Texture2D _sourceIcon = null;
        private Texture2D _albumCover = null;
        private Texture2D _soundTexture = null;
        private Texture2D _gameStateTexture = null;
        private RenderTexture _videoTexture = null;
        private float[] _fft = new float[FFT_SIZE / 2];
        private float[] _wave = new float[FFT_TEXTURE_WIDTH];
        private float[] _prevFft = new float[FFT_SIZE / 2];
        private float[] _rawFft = new float[FFT_SIZE * 2];
        private float[] _rawWave = new float[FFT_SIZE];
        private readonly ushort[] _gameStateData = new ushort[GAME_STATE_TEX_WIDTH];

        private bool _videoTexFound = false;

        private UniTask           _updateTask = UniTask.CompletedTask;
        private NativeArray<byte> _pixelData;

        private static int _soundTexId = Shader.PropertyToID("_Yarg_SoundTex");
        private static int _gameStateTexId = Shader.PropertyToID("_Yarg_GameStateTex");
        private static int _sourceIconId = Shader.PropertyToID("_Yarg_SourceIcon");
        private static int _albumCoverId = Shader.PropertyToID("_Yarg_AlbumCover");
        private static int _videoTexId = Shader.PropertyToID("_Yarg_VideoTex");
        private static int _imageTexId = Shader.PropertyToID("_Yarg_ImageTex");
        private static int _backgroundTexId = Shader.PropertyToID("_Yarg_BackgroundTex");

        private const double MIN_DB = -100.0;
        private const double MAX_DB = -30.0;
        private const double DB_RANGE = MAX_DB - MIN_DB;
        private const int FFT_SIZE_LOG = 11 /* aka log2(2048) */;
        private const int FFT_SIZE = 1 << FFT_SIZE_LOG;
        private const int FFT_TEXTURE_WIDTH = 512;
        // IMPORTANT: the game state texture is APPEND-ONLY. When adding new
        // fields, always append them after the existing ones - never reorder
        // or remove entries. Shaders access the texels by index through
        // Assets/Art/Shaders/gamestate.hlsl, so appending keeps existing
        // shaders working unchanged.
        // Current layout:
        //   0: song length (seconds)
        //   1: song position (seconds)
        //   2: fail meter value (0.0-1.0)
        //   3: song progress, normalized (0.0-1.0)
        //   4: countdown time (seconds until song starts, 0 once playing)
        //   5: paused (0 or 1)
        //   6: practice mode (0 or 1)
        //   7: playback speed
        //   8: beat phase, audio timing (0.0-1.0)
        //   9: measure phase, audio timing (0.0-1.0)
        //  10: star power active, any player (0 or 1)
        //  11: star power charge, highest player (0.0-1.0)
        //  12: crowd intensity (0.0-1.0)
        //  13: band accuracy, average note hit % (0.0-1.0)
        //  14: band combo multiplier, average player (>= 1)
        //  15: stars earned incl. progress into next star (0.0-6.0)
        private const int GAME_STATE_TEX_WIDTH = 16;
        private const int VIDEO_TEX_WIDTH = 256;
        private const int VIDEO_TEX_HEIGHT = 144;

        // TODO: Get the number of active channels from the mixer instead of assuming
        //  Note that this won't _break_ if there are more channels, it will just make
        //  wave shader output look weird on songs that have multichannel audio (or mono, for that matter)
        private const int AUDIO_CHANNELS = 2;
        // You would expect this to be 1 / AUDIO_CHANNELS, but we need a little bump for some as yet
        // to be understood reason
        private const float PER_CHANNEL_MULTIPLIER = 0.6f;

        protected override void GameplayAwake()
        {
            _ = GetSoundTexture();
            _ = GetGameStateTexture();
        }

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
                using var image = GameManager.Song.LoadAlbumData();
                if (image == null)
                {
                    return DefaultAlbumCover;
                }
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
                Shader.SetGlobalTexture(_soundTexId, _soundTexture);
            }
            return _soundTexture;
        }

        protected Texture2D GetGameStateTexture()
        {
            if (_gameStateTexture == null)
            {
                // Single f16 channel (RHalf = 16-bit float)
                // x: song length (seconds)
                // y: song position (seconds)
                // z: fail meter value (0.0-1.0)
                _gameStateTexture = new Texture2D(GAME_STATE_TEX_WIDTH, 1, TextureFormat.RHalf, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };
                Shader.SetGlobalTexture(_gameStateTexId, _gameStateTexture);
            }
            return _gameStateTexture;
        }

        public RenderTexture GetVideoTexture(int? width, int? height)
        {
            if (_videoTexture == null)
            {
                _videoTexture = new RenderTexture(VIDEO_TEX_WIDTH, VIDEO_TEX_HEIGHT, 0);
            }
            if (width is { } checkedWidth && checkedWidth > _videoTexture.width)
            {
                _videoTexture.width = checkedWidth;
            }
            if (height is { } checkedHeight && checkedHeight > _videoTexture.height)
            {
                _videoTexture.height = checkedHeight;
            }
            return _videoTexture;
        }

        public bool VideoTexFound() => _videoTexFound;

        public void CreateVideoTexture()
        {
            if (_videoTexture != null && !_videoTexture.IsCreated())
            {
                _videoTexture.Create();
            }
        }

        public void ProcessMaterial(Material m, BackgroundType? songBackgroundType)
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
            if (m.HasTexture(_videoTexId) && songBackgroundType is BackgroundType.Video)
            {
                var matTex = m.GetTexture(_videoTexId);

                if (matTex != null)
                {
                    m.SetTexture(_videoTexId, GetVideoTexture(matTex.width, matTex.height));
                    _videoTexFound = true;
                }
            }
            if (m.HasTexture(_imageTexId) && songBackgroundType is BackgroundType.Image)
            {
                var matTex = m.GetTexture(_imageTexId);

                if (matTex != null)
                {
                    m.SetTexture(_imageTexId, GetVideoTexture(matTex.width, matTex.height));
                    _videoTexFound = true;
                }
            }
            if (m.HasTexture(_backgroundTexId) && songBackgroundType is BackgroundType.Image or BackgroundType.Video)
            {
                var matTex = m.GetTexture(_backgroundTexId);

                if (matTex != null)
                {
                    m.SetTexture(_backgroundTexId, GetVideoTexture(matTex.width, matTex.height));
                    _videoTexFound = true;
                }
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

        public void Update()
        {
            UpdateGameState();

            if (_soundTexture != null && _updateTask.Status.IsCompleted())
            {
                if (_pixelData.IsCreated)
                {
                    _soundTexture.Apply(false, false);
                }

                _pixelData = _soundTexture.GetPixelData<Byte>(0);
                _updateTask =
                    UniTask.RunOnThreadPool(static state => ((TextureManager) state).UpdateFFT_Threaded(), this);
            }
        }

        private void UpdateFFT_Threaded()
        {
            UpdateFFT(_pixelData);
        }

        private void UpdateGameState()
        {
            var tex = GetGameStateTexture();

            double songLength = GameManager.SongLength;
            double songTime = GameManager.SongTime;

            _gameStateData[0] = ToF16((float) songLength);
            _gameStateData[1] = ToF16((float) songTime);

            var failMeter = GameManager.EngineManager?.Happiness ?? 1f;
            _gameStateData[2] = ToF16(math.clamp(failMeter, 0f, 1f));

            _gameStateData[3] = ToF16(songLength > 0 ? math.clamp((float) (songTime / songLength), 0f, 1f) : 0f);
            _gameStateData[4] = ToF16((float) Math.Max(0.0, -songTime));
            _gameStateData[5] = ToF16(GameManager.Paused ? 1f : 0f);
            _gameStateData[6] = ToF16(GameManager.IsPractice ? 1f : 0f);
            _gameStateData[7] = ToF16(GameManager.SongSpeed);

            // New fields go here, appended after the existing ones

            var beats = GameManager.BeatEventHandler?.Audio;
            _gameStateData[8] = ToF16(beats?.QuarterNote == null ? 0f : (float) beats.QuarterNote.CurrentPercentage);
            _gameStateData[9] = ToF16(beats?.Measure == null ? 0f : (float) beats.Measure.CurrentPercentage);

            UpdateEngineState();

            tex.SetPixelData(_gameStateData, 0);
            tex.Apply(false, false);
        }

        private void UpdateEngineState()
        {
            _gameStateData[10] = ToF16(0f);
            _gameStateData[11] = ToF16(0f);
            _gameStateData[12] = ToF16(0f);
            _gameStateData[13] = ToF16(0f);

            var engineManager = GameManager.EngineManager;
            if (engineManager == null)
            {
                return;
            }

            float spCharge = 0f;
            float accuracySum = 0f;
            float multiplierSum = 0f;
            int playerCount = 0;

            foreach (var engine in engineManager.Engines)
            {
                var baseEngine = engine.BaseEngine;
                var stats = baseEngine.BaseStats;

                if (stats.IsStarPowerActive)
                {
                    _gameStateData[10] = ToF16(1f);
                }

                spCharge = MathF.Max(spCharge, (float) baseEngine.GetStarPowerBarAmount());

                accuracySum += stats.Percent;
                multiplierSum += stats.ScoreMultiplier;
                playerCount++;
            }

            _gameStateData[11] = ToF16(math.clamp(spCharge, 0f, 1f));
            _gameStateData[13] = ToF16(playerCount > 0 ? math.clamp(accuracySum / playerCount, 0f, 1f) : 1f);
            _gameStateData[14] = ToF16(playerCount > 0 ? multiplierSum / playerCount : 1f);

            _gameStateData[12] = ToF16(GetCrowdIntensity());
            _gameStateData[15] = ToF16(math.clamp(engineManager.Stars, 0f, 6f));
        }

        private float GetCrowdIntensity()
        {
            return GameManager.CrowdEventHandler?.CrowdState switch
            {
                CrowdState.Intense => 1f,
                CrowdState.Normal  => 0.66f,
                CrowdState.Mellow  => 0.33f,
                _                  => 0f,
            };
        }

        private static ushort ToF16(float value) => (ushort) math.f32tof16(value);

        protected override void GameplayDestroy()
        {
            if (_videoTexture != null)
            {
                _videoTexture.Release();
                _videoTexture.DiscardContents();
                _videoTexture = null;
            }
            // Dispose FFT stuff, but only after the FFT update has completed
            _ = Destroy_Async();
        }

        private async UniTaskVoid Destroy_Async()
        {
            try
            {
                await _updateTask;
            }
            finally
            {
                if (_pixelData.IsCreated)
                {
                    _pixelData.Dispose();
                }
            }

            Destroy(_soundTexture);
            _soundTexture = null;

            Destroy(_gameStateTexture);
            _gameStateTexture = null;
        }
    }
}
