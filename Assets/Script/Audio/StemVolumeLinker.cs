using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Settings;

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

            var setting = _settings.GetVolumeSetting(stem);
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

        public void Dispose()
        {
            foreach (var (stem, callback) in _callbacks)
            {
                var setting = _settings.GetVolumeSetting(stem);
                if (setting != null)
                {
                    setting.OnChange -= callback;
                }
            }
            _callbacks.Clear();
        }
    }
}
