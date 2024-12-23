using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Audio
{
    public enum AudioFxMode
    {
        Off,
        MultitrackOnly,
        On
    }

    public static class GlobalAudioHandler
    {
        public const int WHAMMY_FFT_DEFAULT = 512;
        public const int WHAMMY_OVERSAMPLE_DEFAULT = 8;
        public static readonly int MAX_THREADS = Environment.ProcessorCount switch
        {
            >= 16 => 16,
            >= 6 => Environment.ProcessorCount / 2,
            _ => 2
        };

        internal static readonly Dictionary<SongStem, StemSettings> StemSettings;

        static GlobalAudioHandler()
        {
            var vocals = new StemSettings();
            var drums = new StemSettings();

            StemSettings = new()
            {
                { SongStem.Song,    new StemSettings() },
                { SongStem.Guitar,  new StemSettings() },
                { SongStem.Bass,    new StemSettings() },
                { SongStem.Rhythm,  new StemSettings() },
                { SongStem.Keys,    new StemSettings() },
                { SongStem.Vocals,  vocals },
                { SongStem.Vocals1, vocals },
                { SongStem.Vocals2, vocals },
                { SongStem.Drums,   drums },
                { SongStem.Drums1,  drums },
                { SongStem.Drums2,  drums },
                { SongStem.Drums3,  drums },
                { SongStem.Drums4,  drums },
                { SongStem.Crowd,   new StemSettings() },
                { SongStem.Sfx,     new StemSettings() },
                { SongStem.DrumSfx, new StemSettings() },
            };
        }

        internal static bool LogMixerStatus;

        public static bool UseWhammyFx;
        public static bool IsChipmunkSpeedup;

        /// <summary>
        /// The number of semitones to bend the pitch by. Must be at least 1;
        /// </summary>
        public static float WhammyPitchShiftAmount = 1f;

        // Not implemented, as changing the FFT size causes BASS_FX to crash
        // /// <summary>
        // /// The size of the whammy FFT buffer. Must be a power of 2, up to 8192.
        // /// </summary>
        // /// <remarks>
        // /// Changes to this value will not be applied until the next song plays.
        // /// </remarks>
        // public int WhammyFFTSize
        // {
        //     get => (int)Math.Pow(2, _whammyFFTSize);
        //     set => _whammyFFTSize = (int)Math.Log(value, 2);
        // }
        // private int _whammyFFTSize = WHAMMY_FFT_DEFAULT;

        /// <summary>
        /// The oversampling factor of the whammy SFX. Must be at least 4.
        /// </summary>
        /// <remarks>
        /// Changes to this value will not be applied until the next song plays.
        /// </remarks>
        public static int WhammyOversampleFactor = WHAMMY_OVERSAMPLE_DEFAULT;

        public static double GetTrueVolume(SongStem stem)
        {
            return StemSettings[stem].TrueVolume;
        }

        public static double GetVolumeSetting(SongStem stem)
        {
            return StemSettings[stem].VolumeSetting;
        }

        public static void SetVolumeSetting(SongStem stem, double volume)
        {
            StemSettings[stem].VolumeSetting = volume;
        }

        public static bool GetReverbSetting(SongStem stem)
        {
            return StemSettings[stem].Reverb;
        }

        public static void SetReverbSetting(SongStem stem, bool reverb)
        {
            StemSettings[stem].Reverb = reverb;
        }

        public static float GetWhammyPitchSetting(SongStem stem)
        {
            return StemSettings[stem].WhammyPitch;
        }

        public static void SetWhammyPitchSetting(SongStem stem, float percent)
        {
            StemSettings[stem].WhammyPitch = percent;
        }

        private static object _instanceLock = new();
        private static AudioManager? _instance;

        public static void Initialize<TAudioManager>()
            where TAudioManager : AudioManager, new()
        {
            // Two locks to allow other things to happen
            lock (_instanceLock)
            {
                if (_instance == null || _instance is not TAudioManager)
                {
                    _instance?.Dispose();
                    _instance = new TAudioManager();
                }
            }
        }

        public static void Close()
        {
            lock (_instanceLock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        private class NotInitializedException : Exception
        {
            public NotInitializedException()
                : base("Audio manager not initialized") { }
        }

        public static ReadOnlySpan<string> SupportedFormats
        {
            get
            {
                lock ( _instanceLock)
                {
                    if (_instance == null)
                    {
                        throw new NotInitializedException();
                    }
                    return _instance.SupportedFormats;
                }
            }
        }

        public static int PlaybackLatency
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        throw new NotInitializedException();
                    }
                    return _instance.PlaybackLatency;
                }
            }
        }

        public static int MinimumBufferLength
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        throw new NotInitializedException();
                    }
                    return _instance.MinimumBufferLength;
                }
            }
        }

        public static int MaximumBufferLength
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        throw new NotInitializedException();
                    }
                    return _instance.MaximumBufferLength;
                }
            }
        }

        public static float GlobalSpeed
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        throw new NotInitializedException();
                    }
                    return _instance.GlobalSpeed;
                }
            }
            set
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        throw new NotInitializedException();
                    }
                    _instance.GlobalSpeed = value;
                }
            }
        }

        public static void PlaySoundEffect(SfxSample sample)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                _instance.SfxSamples[(int) sample]?.Play();
            }
        }

        public static void PlayDrumSoundEffect(DrumSfxSample sample, double volume)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                _instance.DrumSfxSamples[(int) sample]?.Play(volume);
            }
        }

        public static StemMixer? LoadCustomFile(string name, Stream stream, float speed, double volume, SongStem stem = SongStem.Song)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                return _instance.LoadCustomFile(name, stream, speed, volume, stem);
            }
        }

        public static StemMixer? LoadCustomFile(string file, float speed, double volume, SongStem stem = SongStem.Song)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                return _instance.LoadCustomFile(file, speed, volume, stem);
            }
        }

        public static StemMixer? CreateMixer(string name, float speed, double mixerVolume, bool clampStemVolume)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                return _instance.CreateMixer(name, speed, mixerVolume, clampStemVolume);
            }
        }

        public static StemMixer? CreateMixer(string name, Stream stream, float speed, double mixerVolume, bool clampStemVolume)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                return _instance.CreateMixer(name, stream, speed, mixerVolume, clampStemVolume);
            }
        }

        public static MicDevice? GetInputDevice(string name)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                return _instance.GetInputDevice(name);
            }
        }

        public static List<(int id, string name)> GetAllInputDevices()
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                return _instance.GetAllInputDevices();
            }
        }

        public static MicDevice? CreateDevice(int deviceId, string name)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                return _instance.CreateDevice(deviceId, name);
            }
        }

        public static void SetMasterVolume(double volume)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                _instance.SetMasterVolume(volume);
            }
        }

        public static void TogglePlaybackBuffer(bool enable)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                _instance.ToggleBuffer(enable);
            }
        }

        public static void SetBufferLength(int length)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    throw new NotInitializedException();
                }
                _instance.SetBufferLength(length);
            }
        }
    }
}
