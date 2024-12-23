using System;

namespace YARG.Core.Audio
{
    public class StemSettings
    {
        public static bool ApplySettings = true;

        private Action<double>? _onVolumeChange;
        private Action<bool>? _onReverbChange;
        private Action<float>? _onWhammyPitchChange;
        private double _volume;
        private bool _reverb;
        private float _whammyPitch;

        public StemSettings()
        {
            _volume = 1;
        }

        public event Action<double> OnVolumeChange
        {
            add { _onVolumeChange += value; }
            remove { _onVolumeChange -= value; }
        }

        public event Action<bool> OnReverbChange
        {
            add { _onReverbChange += value; }
            remove { _onReverbChange -= value; }
        }

        public event Action<float> OnWhammyPitchChange
        {
            add { _onWhammyPitchChange += value; }
            remove { _onWhammyPitchChange -= value; }
        }

        public double VolumeSetting
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 1);
                _onVolumeChange?.Invoke(TrueVolume);
            }
        }

        public double TrueVolume => (ApplySettings ? _volume : 1);

        public bool Reverb
        {
            get => _reverb;
            set
            {
                if (value != _reverb)
                {
                    _reverb = value;
                    _onReverbChange?.Invoke(value);
                }
            }
        }

        public float WhammyPitch
        {
            get => _whammyPitch;
            set
            {
                value = Math.Clamp(value, 0, 1);
                if (value != _whammyPitch)
                {
                    _whammyPitch = value;
                    _onWhammyPitchChange?.Invoke(value);
                }
            }
        }
    }
}
