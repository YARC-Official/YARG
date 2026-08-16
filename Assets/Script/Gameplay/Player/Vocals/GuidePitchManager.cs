using System;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
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

        /// <summary>
        /// Which harmony part (0-based) has guide pitch enabled.
        /// -1 means off. For solo vocals only -1 and 0 are used.
        /// </summary>
        private int _enabledHarmonyIndex = -1;

        private readonly ToneChannel _toneChannel;
        private          VocalsTrack _vocalsTrack;

        public event Action<string, Color> OnGuidePitchChanged;

        /// <param name="toneChannel">The tone channel that renders the guide pitch.</param>
        /// <param name="vocalsTrack">The original (full-song) vocals track.</param>
        public GuidePitchManager(ToneChannel toneChannel, VocalsTrack vocalsTrack)
        {
            _toneChannel = toneChannel ?? throw new ArgumentNullException(nameof(toneChannel));
            _vocalsTrack = vocalsTrack ?? throw new ArgumentNullException(nameof(vocalsTrack));
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
            OnGuidePitchChanged?.Invoke(GetStatusString(), GetStatusColor());
        }

        /// <summary>
        /// Call when the practice section changes so the schedule follows the new section.
        /// </summary>
        public void OnPracticeSectionChanged(VocalsTrack sectionTrack)
        {
            _vocalsTrack = sectionTrack ?? throw new ArgumentNullException(nameof(sectionTrack));

            // Clamp enabled index in case the new section has fewer parts
            if (_enabledHarmonyIndex >= _vocalsTrack.Parts.Count)
            {
                _enabledHarmonyIndex = -1;
            }

            PublishSchedule();
            OnGuidePitchChanged?.Invoke(GetStatusString(), GetStatusColor());
        }

        public string GetStatusString()
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

        public Color GetStatusColor()
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

        public void Dispose() => _toneChannel.Dispose();

        private void PublishSchedule() => _toneChannel.SetSchedule(VocalToneSchedule.Build(GetEnabledPart()));

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
