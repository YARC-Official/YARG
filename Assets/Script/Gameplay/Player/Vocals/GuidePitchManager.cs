using System;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Localization;

namespace YARG.Gameplay.Player
{
    /// <summary>
    /// Manages the guide pitch feature for vocals practice mode.
    ///
    /// <para>
    /// Toggle state (which harmony part is active) lives here on the game thread. Selecting a part
    /// publishes its pitch schedule to a <see cref="ToneChannel"/>; the audio backend renders the
    /// tone against the song position, so it stays sample-accurate with the mixed stems without
    /// any per-sample work here.
    /// </para>
    /// </summary>
    public sealed class GuidePitchManager : IDisposable
    {
        /// <summary>Volume of the guide pitch tone relative to the master mix.</summary>
        private const double VOLUME = 0.35;

        /// <summary>
        /// Target duration for a full volume fade of the guide pitch tone, in seconds. Long enough
        /// to avoid an audible click at note boundaries, short enough to stay tight against onsets.
        /// </summary>
        private const double FADE_SECONDS = 0.015;

        /// <summary>
        /// Which harmony part (0-based) has guide pitch enabled.
        /// -1 means off. For solo vocals only -1 and 0 are used.
        /// </summary>
        private int _enabledHarmonyIndex = -1;

        private readonly ToneChannel           _toneChannel;
        private readonly VocalsTrack           _vocalsTrack;
        private readonly Action<string, Color> _statusChanged;

        /// <summary>
        /// Creates the guide pitch for the song being practiced, or returns <see langword="null"/>
        /// when it does not apply: the chart has no vocals, or the audio backend cannot supply a
        /// tone channel. Guide pitch is optional, so a null result is an ordinary outcome and
        /// leaves the rest of practice mode untouched.
        /// </summary>
        /// <param name="mixer">Mixer for the song being practiced.</param>
        /// <param name="vocalTrack">Vocal track being practiced against.</param>
        /// <param name="statusChanged">
        /// Invoked with the status text and color whenever the selected part changes, and once up
        /// front with the initial state.
        /// </param>
        public static GuidePitchManager Create(StemMixer mixer, VocalTrack vocalTrack,
            Action<string, Color> statusChanged)
        {
            // The vocal track object is left inactive for charts without vocals.
            if (!vocalTrack.gameObject.activeSelf || vocalTrack.OriginalVocalsTrack == null)
            {
                return null;
            }

            var toneChannel = mixer.CreateToneChannel(VOLUME, FADE_SECONDS);
            if (toneChannel == null)
            {
                return null;
            }

            var manager = new GuidePitchManager(toneChannel, vocalTrack.OriginalVocalsTrack,
                statusChanged);
            manager.NotifyStatusChanged();
            return manager;
        }

        private GuidePitchManager(ToneChannel toneChannel, VocalsTrack vocalsTrack,
            Action<string, Color> statusChanged)
        {
            _toneChannel = toneChannel;
            _vocalsTrack = vocalsTrack;
            _statusChanged = statusChanged;
        }

        /// <summary>
        /// Cycles the guide pitch to the next state.
        /// Solo vocals: Off → On → Off.
        /// Harmonies:   Off → HARM1 → HARM2 → HARM3 → Off (skipping empty parts).
        /// </summary>
        public void ToggleGuidePitch()
        {
            bool isHarmony = _vocalsTrack.Instrument == Instrument.Harmony;
            var  parts     = _vocalsTrack.Parts;

            if (!isHarmony)
            {
                _enabledHarmonyIndex = _enabledHarmonyIndex < 0 ? 0 : -1;
            }
            else
            {
                int  next  = _enabledHarmonyIndex + 1;
                bool found = false;
                for (int i = 0; i < parts.Count; i++)
                {
                    int candidate = (next + i) % (parts.Count + 1); // +1 to include Off
                    if (candidate >= parts.Count)
                    {
                        _enabledHarmonyIndex = -1;
                        found = true;
                        break;
                    }
                    if (parts[candidate].NotePhrases.Count > 0)
                    {
                        _enabledHarmonyIndex = candidate;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    _enabledHarmonyIndex = -1;
                }
            }

            PublishSchedule();
            NotifyStatusChanged();
        }

        public void Dispose() => _toneChannel.Dispose();

        /// <summary>
        /// Pushes the current part's schedule to the backend. A rejected schedule leaves the previous
        /// one playing, which in practice mode means the tone keeps following the section the player
        /// just left, so drop back to off rather than let it contradict the notes on screen.
        /// </summary>
        private void PublishSchedule()
        {
            if (_toneChannel.SetSchedule(VocalToneSchedule.Build(GetEnabledPart())))
            {
                return;
            }

            YargLogger.LogWarning("Could not update the guide pitch schedule; disabling it.");
            _enabledHarmonyIndex = -1;
            _toneChannel.SetSchedule(ReadOnlySpan<ToneSegment>.Empty);
        }

        private void NotifyStatusChanged() => _statusChanged?.Invoke(GetStatusString(), GetStatusColor());

        private string GetStatusString()
        {
            if (_enabledHarmonyIndex < 0)
            {
                return Localize.Key("Menu.Common.Off");
            }

            if (_vocalsTrack.Instrument != Instrument.Harmony)
            {
                return Localize.Key("Menu.Common.On");
            }

            return $"HARM{_enabledHarmonyIndex + 1}";
        }

        private Color GetStatusColor()
        {
            if (_enabledHarmonyIndex < 0 || _vocalsTrack.Instrument != Instrument.Harmony)
            {
                return Color.white;
            }

            int index = _enabledHarmonyIndex;
            if (index < 0 || index >= VocalTrack.Colors.Length)
            {
                return Color.white;
            }

            return VocalTrack.Colors[index];
        }

        private VocalsPart GetEnabledPart()
        {
            if (_enabledHarmonyIndex < 0 || _enabledHarmonyIndex >= _vocalsTrack.Parts.Count)
            {
                return null;
            }

            return _vocalsTrack.Parts[_enabledHarmonyIndex];
        }
    }
}
