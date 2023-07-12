using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Song;
using DeviceType = ManagedBass.DeviceType;

namespace YARG.Audio.BASS
{
    public class BassAudioManager : MonoBehaviour, IAudioManager
    {
        public AudioOptions Options { get; set; } = new();

        public IList<string> SupportedFormats { get; private set; }

        public bool IsAudioLoaded { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsFadingOut { get; private set; }

        public double MasterVolume { get; private set; }
        public double SfxVolume { get; private set; }

        public double CurrentPositionD => GetPosition();
        public double AudioLengthD { get; private set; }

        public float CurrentPositionF => (float) GetPosition();
        public float AudioLengthF { get; private set; }

        public event Action SongEnd
        {
            add
            {
                if (_mixer is null)
                {
                    throw new InvalidOperationException("No song is currently loaded!");
                }

                _mixer.SongEnd += value;
            }
            remove
            {
                if (_mixer is null)
                {
                    throw new InvalidOperationException("No song is currently loaded!");
                }

                _mixer.SongEnd -= value;
            }
        }

        private double[] _stemVolumes;
        private ISampleChannel[] _sfxSamples;

        private int _opusHandle;

        private IStemMixer _mixer;

        private void Awake()
        {
            SupportedFormats = new[]
            {
                ".ogg", ".mogg", ".wav", ".mp3", ".aiff", ".opus",
            };

            _stemVolumes = new double[AudioHelpers.SupportedStems.Count];

            _sfxSamples = new ISampleChannel[AudioHelpers.SfxPaths.Count];

            _opusHandle = 0;
        }

        public void Initialize()
        {
            Debug.Log("Initializing BASS...");
            string bassPath = GetBassDirectory();
            string opusLibDirectory = Path.Combine(bassPath, "bassopus");

            _opusHandle = Bass.PluginLoad(opusLibDirectory);
            Bass.Configure(Configuration.IncludeDefaultDevice, true);

            Bass.UpdatePeriod = 5;
            Bass.DeviceBufferLength = 10;
            Bass.PlaybackBufferLength = 75;
            Bass.DeviceNonStop = true;

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
                Debug.LogError("Failed to initialize BASS");
                Debug.LogError($"Bass Error: {Bass.LastError}");
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
        }

        public void Unload()
        {
            Debug.Log("Unloading BASS plugins");

            UnloadSong();

            Bass.PluginFree(_opusHandle);
            _opusHandle = 0;

            // Free SFX samples
            foreach (var sample in _sfxSamples)
            {
                sample?.Dispose();
            }

            Bass.Free();
        }

        public IList<IMicDevice> GetAllInputDevices()
        {
            var mics = new List<IMicDevice>();

            var typeWhitelist = new List<DeviceType>
            {
                DeviceType.Headset,
                DeviceType.Digital,
                DeviceType.Line,
                DeviceType.Headphones,
                DeviceType.Microphone,
            };

            for (int deviceIndex = 0; Bass.RecordGetDeviceInfo(deviceIndex, out var info); deviceIndex++)
            {
                if (!info.IsEnabled)
                {
                    continue;
                }

                //Debug.Log($"Device {deviceIndex}: Name: {info.Name}. Type: {info.Type}. IsLoopback: {info.IsLoopback}.");

                // Check if type is in whitelist
                // The "Default" device is also excluded here since we want the user to explicitly pick which microphone to use
                if (!typeWhitelist.Contains(info.Type) || info.Name == "Default")
                {
                    continue;
                }

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
                    Debug.LogError($"SFX path does not exist! {sfxPath}");
                    continue;
                }

                var sfxSample = AudioHelpers.GetSfxFromName(sfxFile);

                var sfx = new BassSampleChannel(this, sfxPath, 8, sfxSample);
                if (sfx.Load() != 0)
                {
                    Debug.LogError($"Failed to load SFX! {sfxPath}");
                    Debug.LogError($"Bass Error: {Bass.LastError}");
                    continue;
                }

                _sfxSamples[(int) sfxSample] = sfx;
                Debug.Log($"Loaded {sfxFile}");
            }

            Debug.Log("Finished loading SFX");
        }

        public void LoadSong(IDictionary<SongStem, string> stems, float speed)
        {
            Debug.Log("Loading song");
            UnloadSong();

            _mixer = new BassStemMixer(this);
            if (!_mixer.Create())
            {
                throw new Exception($"Failed to create mixer: {Bass.LastError}");
            }

            foreach (var stem in stems)
            {
                var stemChannel = new BassStemChannel(this, stem.Value, stems.Count > 1 ? stem.Key : SongStem.Song);
                if (stemChannel.Load(speed) != 0)
                {
                    Debug.LogError($"Failed to load stem! {stem.Value}");
                    Debug.LogError($"Bass Error: {Bass.LastError}");
                    continue;
                }

                if (_mixer.AddChannel(stemChannel) != 0)
                {
                    Debug.LogError($"Failed to add stem to mixer!");
                    Debug.LogError($"Bass Error: {Bass.LastError}");
                }
            }

            Debug.Log($"Loaded {_mixer.StemsLoaded} stems");

            // Setup audio length
            AudioLengthD = _mixer.LeadChannel.LengthD;
            AudioLengthF = (float) AudioLengthD;

            IsAudioLoaded = true;
        }

        public void LoadMogg(byte[] moggArray, List<(SongStem, int[], float[])> stemMaps, float speed)
        {
            Debug.Log("Loading mogg song");
            UnloadSong();

            // Initialize stream
            // Last flag is new BASS_SAMPLE_NOREORDER flag, which is not in the BassFlags enum,
            // as it was made as part of an update to fix <= 8 channel oggs.
            // https://www.un4seen.com/forum/?topic=20148.msg140872#msg140872
            const BassFlags flags = BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile | (BassFlags) 64;

            int start = BitConverter.ToInt32(moggArray, 4);
            int moggStreamHandle = Bass.CreateStream(moggArray, start, moggArray.Length - start, flags);
            if (moggStreamHandle == 0)
            {
                Debug.LogError($"Failed to load mogg file or position: {Bass.LastError}");
                return;
            }

            // Initialize mixer
            BassMoggStemMixer mixer = new(this, moggStreamHandle);
            if (!mixer.Create())
            {
                throw new Exception($"Failed to create mixer: {Bass.LastError}");
            }

            // Split stream into multiple channels

            var channelMap = new int[2];
            channelMap[1] = -1;

            for (var i = 0; i < stemMaps.Count; i++)
            {
                for (int j = 0; j < stemMaps[i].Item2.Length; ++j)
                {
                    channelMap[0] = stemMaps[i].Item2[j];
                    int splitHandle = BassMix.CreateSplitStream(moggStreamHandle, BassFlags.Decode | BassFlags.SplitPosition, channelMap);
                    if (splitHandle == 0)
                    {
                        throw new Exception($"Failed to create MOGG stream handle: {Bass.LastError}");
                    }

                    var channel = new BassMoggStemChannel(this, stemMaps[i].Item1, splitHandle, stemMaps[i].Item3[2 * j], stemMaps[i].Item3[2 * j + 1]);
                    if (channel.Load(speed) < 0)
                    {
                        throw new Exception($"Failed to load MOGG stem channel: {Bass.LastError}");
                    }

                    int code = mixer.AddChannel(channel);
                    if (code != 0)
                    {
                        throw new Exception($"Failed to add MOGG stem channel to mixer: {Bass.LastError}");
                    }
                }
            }

            Debug.Log($"Loaded {mixer.StemsLoaded} stems");

            // Setup audio length
            AudioLengthD = mixer.LeadChannel.LengthD;
            AudioLengthF = (float) AudioLengthD;

            IsAudioLoaded = true;
            _mixer = mixer;
        }

        public void LoadCustomAudioFile(string audioPath, float speed)
        {
            Debug.Log("Loading custom audio file");
            UnloadSong();

            _mixer = new BassStemMixer(this);
            if (!_mixer.Create())
            {
                throw new Exception($"Failed to create mixer: {Bass.LastError}");
            }

            var stemChannel = new BassStemChannel(this, audioPath, SongStem.Song);
            if (stemChannel.Load(speed) != 0)
            {
                Debug.LogError($"Failed to load stem! {audioPath}");
                Debug.LogError($"Bass Error: {Bass.LastError}");
                return;
            }

            if (_mixer.AddChannel(stemChannel) != 0)
            {
                Debug.LogError("Failed to add stem to mixer!");
                Debug.LogError($"Bass Error: {Bass.LastError}");
            }

            Debug.Log($"Loaded {_mixer.StemsLoaded} stems");

            // Setup audio length
            AudioLengthD = _mixer.LeadChannel.LengthD;
            AudioLengthF = (float) AudioLengthD;

            IsAudioLoaded = true;
        }

        public void UnloadSong()
        {
            IsPlaying = false;
            IsAudioLoaded = false;

            // Free mixer (and all channels in it)
            _mixer?.Dispose();
            _mixer = null;
        }

        public void Play() => Play(false);

        private void Play(bool fadeIn)
        {
            // Don't try to play if there's no audio loaded or if it's already playing
            if (!IsAudioLoaded || IsPlaying)
            {
                return;
            }

            _mixer.SetPlayVolume(fadeIn);

            if (_mixer.Play() != 0)
            {
                Debug.Log($"Play error: {Bass.LastError}");
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
                Debug.Log($"Pause error: {Bass.LastError}");
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
                stemChannels[i].SetVolume(volume);
        }

        public void SetAllStemsVolume(double volume)
        {
            if (_mixer == null)
            {
                return;
            }

            foreach (var (_, stem) in _mixer.Channels)
                for (int i = 0; i < stem.Count; ++i)
                    stem[i].SetVolume(volume);
        }

        public void UpdateVolumeSetting(SongStem stem, double volume)
        {
            switch (stem)
            {
                case SongStem.Master:
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
                channel.SetReverb(reverb);
        }

        public void SetWhammyPitch(SongStem stem, float percent)
        {
            if (_mixer == null || !AudioHelpers.PitchBendAllowedStems.Contains(stem)) return;

            foreach (var channel in _mixer.GetChannels(stem))
                channel.SetWhammyPitch(percent);
        }

        public double GetPosition(bool desyncCompensation = true)
        {
            if (_mixer is null) return -1;

            return _mixer.GetPosition(desyncCompensation);
        }

        public void SetPosition(double position, bool desyncCompensation = true)
            => _mixer?.SetPosition(position, desyncCompensation);

        private void OnApplicationQuit()
        {
            Unload();
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