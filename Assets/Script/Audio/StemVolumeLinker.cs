using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Settings;
using YARG.Settings.Types;

namespace YARG.Audio
{
    public sealed class StemVolumeLinker : IDisposable
    {
        private readonly StemMixer _mixer;
        private readonly SettingsManager.SettingContainer _settings;
        private readonly Dictionary<SongStem, Action<float>> _callbacks = new();

        public StemVolumeLinker(StemMixer mixer, SettingsManager.SettingContainer settings)
        {
            _mixer = mixer;
            _settings = settings;
            BindAllMixerStems();
        }

        private void BindAllMixerStems()
        {
            foreach (var stem in _mixer.Stems)
            {
                BindStem(stem);
            }
        }

        public void BindStem(SongStem stem)
        {
            if (_callbacks.ContainsKey(stem))
            {
                return;
            }

            var setting = GetStemVolumeSetting(stem);
            if (setting == null)
            {
                return;
            }

            // Create and register callback
            Action<float> callback = volume => _mixer.SetVolume(stem, volume);
            setting.OnChange += callback;
            _callbacks.Add(stem, callback);

            // Set initial volume
            _mixer.SetVolume(stem, setting.Value);
        }

        public void UnbindStem(SongStem stem)
        {
            if (_callbacks.Remove(stem, out var callback))
            {
                var setting = GetStemVolumeSetting(stem);
                if (setting != null)
                {
                    setting.OnChange -= callback;
                }
            }
        }

        private VolumeSetting? GetStemVolumeSetting(SongStem stem)
        {
            return stem switch
            {
                SongStem.Guitar => _settings.GuitarVolume,
                SongStem.Rhythm => _settings.RhythmVolume,
                SongStem.Bass   => _settings.BassVolume,
                SongStem.Keys   => _settings.KeysVolume,
                SongStem.Drums  => _settings.DrumsVolume,
                SongStem.Vocals => _settings.VocalsVolume,
                SongStem.Song   => _settings.SongVolume,
                SongStem.Crowd  => _settings.CrowdVolume,
                _               => null
            };
        }

        public void Dispose()
        {
            foreach (var (stem, callback) in _callbacks)
            {
                var setting = GetStemVolumeSetting(stem);
                if (setting != null)
                {
                    setting.OnChange -= callback;
                }
            }
            _callbacks.Clear();
        }
    }
}
