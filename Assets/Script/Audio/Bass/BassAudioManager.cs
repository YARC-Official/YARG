using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Core.Audio;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YARG.Audio.BASS
{
    public class BassAudioManager : MonoBehaviour, IAudioManager
    {
        public AudioOptions Options { get; set; } = new();

        public IList<string> SupportedFormats { get; private set; } = new[]
        {
            ".ogg", ".mogg", ".wav", ".mp3", ".aiff", ".opus",
        };

        public bool IsAudioLoaded { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsFadingOut { get; private set; }

        public double MasterVolume { get; private set; }
        public double SfxVolume { get; private set; }

        public double PlaybackBufferLength { get; private set; }

        public double CurrentPositionD => GetPosition();
        public double AudioLengthD { get; private set; }

        public float CurrentPositionF => (float) GetPosition();
        public float AudioLengthF { get; private set; }

        private bool _isInitialized = false;

        public event Action SongEnd;

        private double[] _stemVolumes = new double[AudioHelpers.SupportedStems.Count];
        private ISampleChannel[] _sfxSamples = new ISampleChannel[AudioHelpers.SfxPaths.Count];

        private int _opusHandle = 0;

        private BassStemMixer _mixer = null;

        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogError("BASS is already initialized! An error has occurred somewhere and Unity must be restarted.");
                return;
            }

            Debug.Log("Initializing BASS...");
            string bassPath = GetBassDirectory();
            string opusLibDirectory = Path.Combine(bassPath, "bassopus");

            _opusHandle = Bass.PluginLoad(opusLibDirectory);
            if (_opusHandle == 0)
                Debug.LogError($"Failed to load .opus plugin: {Bass.LastError}");

            Bass.Configure(Configuration.IncludeDefaultDevice, true);

            Bass.UpdatePeriod = 5;
            Bass.DeviceBufferLength = 10;
            Bass.PlaybackBufferLength = BassHelpers.PLAYBACK_BUFFER_LENGTH;
            Bass.DeviceNonStop = true;

            PlaybackBufferLength = Bass.PlaybackBufferLength / 1000.0;

            // Affects Windows only. Forces device names to be in UTF-8 on Windows rather than ANSI.
            Bass.Configure(Configuration.UnicodeDeviceInformation, true);
            Bass.Configure(Configuration.TruePlayPosition, 0);
            Bass.Configure(Configuration.UpdateThreads, 2);
            Bass.Configure(Configuration.FloatDSP, true);

            // Undocumented BASS_CONFIG_MP3_OLDGAPS config.
            Bass.Configure((Configuration) 68, 1);

            // Disable undocumented BASS_CONFIG_DEV_TIMEOUT config. Prevents pausing audio output if a device times out.
            Bass.Configure((Configuration) 70, false);

            int deviceCount = Bass.DeviceCount;
            Debug.Log($"Devices found: {deviceCount}");

            if (!Bass.Init(-1, 44100, DeviceInitFlags.Default | DeviceInitFlags.Latency, IntPtr.Zero))
            {
                var error = Bass.LastError;
                if (error == Errors.Already)
                    Debug.LogError("BASS is already initialized! An error has occurred somewhere and Unity must be restarted.");
                else
                    Debug.LogError($"Failed to initialize BASS: {error}");
                return;
            }

            LoadSfx();

            Debug.Log($"BASS Successfully Initialized");
            Debug.Log($"BASS: {Bass.Version}");
            Debug.Log($"BASS.FX: {BassFx.Version}");
            Debug.Log($"BASS.Mix: {BassMix.Version}");

            Debug.Log($"Update Period: {Bass.UpdatePeriod}");
            Debug.Log($"Device Buffer Length: {Bass.DeviceBufferLength}");
            Debug.Log($"Playback Buffer Length: {Bass.PlaybackBufferLength}");

            Debug.Log($"Current Device: {Bass.GetDeviceInfo(Bass.CurrentDevice).Name}");

            _isInitialized = true;
        }

        public void Unload()
        {
            Debug.Log("Unloading BASS plugins");

            UnloadSong();

            Bass.PluginFree(0);

            // Free SFX samples
            foreach (var sample in _sfxSamples)
            {
                sample?.Dispose();
            }

            Bass.Free();
        }

#if UNITY_EDITOR
        // For respecting the editor's mute button
        private bool previousMute = false;
        private void Update()
        {
            bool muted = EditorUtility.audioMasterMute;
            if (muted == previousMute)
                return;

            UpdateVolumeSetting(SongStem.Master, muted ? 0 : Settings.SettingsManager.Settings.MasterMusicVolume.Value);
            previousMute = muted;
        }
#endif

        public IList<IMicDevice> GetAllInputDevices()
        {
            var mics = new List<IMicDevice>();

            // Ignored for now since it causes issues on Linux, BASS must not report device info correctly there
            // TODO: allow configuring this at runtime?
            // Also put into a static variable instead of instantiating every time
            // var typeWhitelist = new List<DeviceType>()
            // {
            //     DeviceType.Headset,
            //     DeviceType.Digital,
            //     DeviceType.Line,
            //     DeviceType.Headphones,
            //     DeviceType.Microphone,
            // };

            for (int deviceIndex = 0; Bass.RecordGetDeviceInfo(deviceIndex, out var info); deviceIndex++)
            {
                // Ignore disabled/claimed devices
                if (!info.IsEnabled || info.IsInitialized) continue;

                // Ignore loopback devices, they're potentially confusing and can cause feedback loops
                if (info.IsLoopback) continue;

                // Check if type is in whitelist
                // The "Default" device is also excluded here since we want the user to explicitly pick which microphone to use
                // if (!typeWhitelist.Contains(info.Type) || info.Name == "Default") continue;
                if (info.Name == "Default") continue;

                mics.Add(new BassMicDevice(deviceIndex, info));
            }

            return mics;
        }

        public void LoadSfx()
        {
            Debug.Log("Loading SFX");

            _sfxSamples = new ISampleChannel[AudioHelpers.SfxPaths.Count];

            string sfxFolder = Path.Combine(Application.streamingAssetsPath, "sfx");

            foreach (string sfxFile in AudioHelpers.SfxPaths)
            {
                string sfxPath = Path.Combine(sfxFolder, sfxFile);

                foreach (string format in SupportedFormats)
                {
                    if (!File.Exists($"{sfxPath}{format}")) continue;

                    // Append extension to path (e.g sfx/boop becomes sfx/boop.ogg)
                    sfxPath += format;
                    break;
                }

                if (!File.Exists(sfxPath))
                {
                    Debug.LogWarning($"SFX Sample {sfxFile} does not exist!");
                    continue;
                }

                var sfxSample = AudioHelpers.GetSfxFromName(sfxFile);

                var sfx = new BassSampleChannel(this, sfxPath, 8, sfxSample);
                if (sfx.Load() != 0)
                {
                    Debug.LogError($"Failed to load SFX {sfxPath}: {Bass.LastError}");
                    continue;
                }

                _sfxSamples[(int) sfxSample] = sfx;
                Debug.Log($"Loaded {sfxFile}");
            }

            Debug.Log("Finished loading SFX");
        }

        public void LoadSong(List<AudioChannel> channels, float speed)
        {
            EditorDebug.Log("Loading song");
            UnloadSong();

            if (channels.Count == 0)
            {
                throw new Exception("No stems were provided!");
            }

            _mixer = new BassStemMixer();
            if (!_mixer.Create())
            {
                throw new Exception($"Failed to create mixer: {Bass.LastError}");
            }

            foreach (var channel in channels)
            {
                var stem = channels.Count > 1 ? channel.Stem : SongStem.Song;
                var stemChannel = BassStemChannel.CreateChannel(this, channel.Stream, stem, speed, channel.Indices);

                if (stemChannel == null)
                {
                    Debug.LogError($"Failed to load stem {stem}: {Bass.LastError}");
                    continue;
                }

                int result = channel.Indices != null
                    ? _mixer.AddChannel(stemChannel, channel.Indices, channel.Panning!)
                    : _mixer.AddChannel(stemChannel);

                if (result != 0)
                {
                    Debug.LogError($"Failed to add stem {stem} to mixer: {Bass.LastError}");
                    continue;
                }
            }

            EditorDebug.Log($"Loaded {_mixer.StemsLoaded} stems");

            // Setup audio length
            AudioLengthD = _mixer.LeadChannel.LengthD;
            AudioLengthF = (float) AudioLengthD;

            // Listen for song end
            _mixer.SongEnd += OnSongEnd;

            IsAudioLoaded = true;
        }

        public void LoadCustomAudioFile(Stream audiostream, float speed)
        {
            EditorDebug.Log("Loading custom audio file");
            UnloadSong();

            _mixer = new BassStemMixer();
            if (!_mixer.Create())
            {
                throw new Exception($"Failed to create mixer: {Bass.LastError}");
            }

            var stemChannel = BassStemChannel.CreateChannel(this, audiostream, SongStem.Song, speed, null);
            if (stemChannel == null)
            {
                throw new Exception($"Failed to load custom file: {Bass.LastError}");
            }

            if (_mixer.GetChannels(SongStem.Song).Length > 0)
            {
                throw new Exception("Custom File already loaded!");
            }

            if (_mixer.AddChannel(stemChannel) != 0)
            {
                throw new Exception($"Failed to add stem to mixer: {Bass.LastError}");
            }

            EditorDebug.Log($"Loaded {_mixer.StemsLoaded} stems");

            // Setup audio length
            AudioLengthD = _mixer.LeadChannel.LengthD;
            AudioLengthF = (float) AudioLengthD;

            // Listen for song end
            _mixer.SongEnd += OnSongEnd;

            IsAudioLoaded = true;
        }

        public void UnloadSong()
        {
            Options.UseMinimumStemVolume = false;
            IsPlaying = false;
            IsAudioLoaded = false;

            // Free mixer (and all channels in it)
            if (_mixer is not null)
            {
                _mixer.SongEnd -= OnSongEnd;
                _mixer.Dispose();
                _mixer = null;
            }
        }

        public void Play() => Play(false);

        private void Play(bool fadeIn)
        {
            // Don't try to play if there's no audio loaded or if it's already playing
            if (!IsAudioLoaded || IsPlaying)
            {
                return;
            }

            _mixer.SetPlayVolume(this, fadeIn);

            if (_mixer.Play() != 0)
            {
                Debug.LogError($"Play error: {Bass.LastError}");
            }

            IsPlaying = _mixer.IsPlaying;
        }

        public void Pause()
        {
            if (!IsAudioLoaded || !IsPlaying)
            {
                return;
            }

            if (_mixer.Pause() != 0)
            {
                Debug.LogError($"Pause error: {Bass.LastError}");
            }

            IsPlaying = _mixer.IsPlaying;
        }

        public void FadeIn(float maxVolume)
        {
            Play(true);
            if (IsPlaying && _mixer != null)
                _mixer.FadeIn(maxVolume);
        }

        public async UniTask FadeOut(CancellationToken token = default)
        {
            if (IsFadingOut)
            {
                Debug.LogWarning("Already fading out song!");
                return;
            }

            if (IsPlaying)
            {
                IsFadingOut = true;
                await _mixer.FadeOut(token);
                IsFadingOut = false;
            }
        }

        public void PlaySoundEffect(SfxSample sample)
        {
            var sfx = _sfxSamples[(int) sample];

            sfx?.Play();
        }

        public void SetStemVolume(SongStem stem, double volume)
        {
            if (_mixer == null)
                return;

            var stemChannels = _mixer.GetChannels(stem);
            for (int i = 0; i < stemChannels.Length; ++i)
                stemChannels[i].SetVolume(this, volume);
        }

        public void SetAllStemsVolume(double volume)
        {
            if (_mixer == null)
            {
                return;
            }

            foreach (var channel in _mixer.Channels)
                channel.SetVolume(this, volume);
        }

        public void UpdateVolumeSetting(SongStem stem, double volume)
        {
            switch (stem)
            {
                case SongStem.Master:
#if UNITY_EDITOR
                    if (EditorUtility.audioMasterMute)
                        volume = 0;
#endif
                    MasterVolume = volume;
                    Bass.GlobalStreamVolume = (int) (10_000 * MasterVolume);
                    Bass.GlobalSampleVolume = (int) (10_000 * MasterVolume);
                    break;
                case SongStem.Sfx:
                    SfxVolume = volume;
                    break;
                default:
                    _stemVolumes[(int) stem] = volume * BassHelpers.SONG_VOLUME_MULTIPLIER;
                    break;
            }
        }

        public double GetVolumeSetting(SongStem stem)
        {
            return stem switch
            {
                SongStem.Master => MasterVolume,
                SongStem.Sfx    => SfxVolume,
                _               => _stemVolumes[(int) stem]
            };
        }

        public void ApplyReverb(SongStem stem, bool reverb)
        {
            if (_mixer == null) return;

            foreach (var channel in _mixer.GetChannels(stem))
                channel.SetReverb(this, reverb);
        }

        public void SetSpeed(float speed)
        {
            _mixer?.SetSpeed(speed);
        }

        public void SetWhammyPitch(SongStem stem, float percent)
        {
            if (_mixer == null || !AudioHelpers.PitchBendAllowedStems.Contains(stem)) return;

            foreach (var channel in _mixer.GetChannels(stem))
                channel.SetWhammyPitch(this, percent);
        }

        public double GetPosition(bool bufferCompensation = true)
        {
            if (_mixer is null) return -1;

            return _mixer.GetPosition(this, bufferCompensation);
        }

        public void SetPosition(double position, bool bufferCompensation = true)
            => _mixer?.SetPosition(this, position, bufferCompensation);

        public int GetData(float[] buffer)
        {
            if (_mixer == null)
            {
                return -1;
            }

            return _mixer.GetData(buffer);
        }

        public bool HasStem(SongStem stem)
        {
            return _mixer != null && _mixer.Channels.Any(channel => channel.Stem == stem);
        }

        private void OnSongEnd()
        {
            Pause();
            SongEnd?.Invoke();
        }

        private void OnDestroy()
        {
            if (!_isInitialized)
            {
                return;
            }

            Unload();
            _isInitialized = false;
        }

        private static string GetBassDirectory()
        {
            string pluginDirectory = Path.Combine(Application.dataPath, "Plugins");

            // Locate windows directory
            // Checks if running on 64 bit and sets the path accordingly
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
#if UNITY_64
			pluginDirectory = Path.Combine(pluginDirectory, "x86_64");
#else
			pluginDirectory = Path.Combine(pluginDirectory, "x86");
#endif
#endif

            // Unity Editor directory, Assets/Plugins/Bass/
#if UNITY_EDITOR
            pluginDirectory = Path.Combine(pluginDirectory, "BassNative");
#endif

            // Editor paths differ to standalone paths, as the project contains platform specific folders
#if UNITY_EDITOR_WIN
            pluginDirectory = Path.Combine(pluginDirectory, "Windows/x86_64");
#elif UNITY_EDITOR_OSX
			pluginDirectory = Path.Combine(pluginDirectory, "Mac");
#elif UNITY_EDITOR_LINUX
			pluginDirectory = Path.Combine(pluginDirectory, "Linux/x86_64");
#endif

            return pluginDirectory;
        }
    }
}