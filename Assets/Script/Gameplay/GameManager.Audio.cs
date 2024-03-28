using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        public class StemState
        {
            public readonly double Volume;
            public int Total;
            public int Audible;
            public int ReverbCount;

            public StemState(double volume)
            {
                Volume = volume;
            }

            public double CalculateVolumeSetting()
            {
                return Volume * Audible / Total;
            }
        }

        private readonly Dictionary<SongStem, StemState> _stemStates = new();
        private SongStem _backgroundStem;
        private int _starPowerActivations = 0;

        private void LoadAudio()
        {
            _stemStates.Clear();
            GlobalAudioHandler.UseMinimumStemVolume = Song.Source.Str.ToLowerInvariant() == "yarg";
            _mixer = Song.LoadAudio(GlobalVariables.State.SongSpeed);
            if (_mixer == null)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load audio!";
                return;
            }

            _mixer.SongEnd += OnAudioEnd;
            _backgroundStem = SongStem.Song;
            foreach (var channel in _mixer.Channels)
            {
                double volume = GlobalAudioHandler.GetVolumeSetting(channel.Stem);
                var stemState = new StemState(volume);
                switch (channel.Stem)
                {
                    case SongStem.Drums:
                    case SongStem.Drums1:
                    case SongStem.Drums2:
                    case SongStem.Drums3:
                    case SongStem.Drums4:
                        _stemStates.TryAdd(SongStem.Drums, stemState);
                        break;
                    default:
                        _stemStates.Add(channel.Stem, stemState);
                        break;
                }
            }

            _backgroundStem = _stemStates.Count > 1 ? SongStem.Song : _stemStates.First().Key;
        }

        private void StarPowerClap(Beatline beat)
        {
            if (_starPowerActivations < 1 || beat.Type == BeatlineType.Weak)
                return;

            GlobalAudioHandler.PlaySoundEffect(SfxSample.Clap);
        }

        public void ChangeStarPowerStatus(bool active)
        {
            if (!SettingsManager.Settings.ClapsInStarpower.Value)
                return;

            _starPowerActivations += active ? 1 : -1;
            if (_starPowerActivations < 0)
                _starPowerActivations = 0;
        }

        public void ChangeStemMuteState(SongStem stem, bool muted)
        {
            if (!SettingsManager.Settings.MuteOnMiss.Value || !_stemStates.TryGetValue(stem, out var state))
            {
                return;
            }

            if (muted)
            {
                --state.Audible;
            }
            else if (state.Audible < state.Total)
            {
                ++state.Audible;
            }

            double volume = state.CalculateVolumeSetting();
            GlobalAudioHandler.SetVolumeSetting(stem, volume);
        }

        public void ChangeStemReverbState(SongStem stem, bool reverb)
        {
            var setting = SettingsManager.Settings.UseStarpowerFx.Value;
            if (setting == StarPowerFxMode.Off)
            {
                return;
            }

            StemState state;
            while (!_stemStates.TryGetValue(stem, out state))
            {
                if (stem == _backgroundStem)
                {
                    return;
                }
                stem = _backgroundStem;
            }

            if (setting == StarPowerFxMode.MultitrackOnly && stem == _backgroundStem)
            {
                return;
            }

            if (reverb)
            {
                ++state.ReverbCount;
            }
            else if (state.ReverbCount > 0)
            {
                --state.ReverbCount;
            }

            bool reverbActive = state.ReverbCount > 0;
            GlobalAudioHandler.SetReverbSetting(stem, reverbActive);
        }

        private void OnAudioEnd()
        {
            EndSong().Forget();
        }
    }
}