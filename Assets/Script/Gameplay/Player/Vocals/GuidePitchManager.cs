using System;
using UnityEngine;
using YARG.Audio.Effects;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Gameplay.Player
{
    /// <summary>
    /// Manages the guide pitch feature for vocals practice mode.
    ///
    /// <para>
    /// Toggle state (which harmony part is active) lives here on the game thread.
    /// All audio work — note scanning and sine-wave synthesis — is delegated to
    /// <see cref="GuidePitchSynthDsp"/>, which runs on the audio thread and
    /// queries the render-ahead song position directly so it is sample-accurate
    /// with the mixed stems.
    /// </para>
    /// </summary>
    public sealed class GuidePitchManager : IDisposable
    {

        /// <summary>
        /// Which harmony part (0-based) has guide pitch enabled.
        /// -1 means off. For solo vocals only -1 and 0 are used.
        /// </summary>
        private int _enabledHarmonyIndex = -1;

        private readonly GuidePitchSynthDsp _dsp;
        private readonly IDisposable        _dspHandle;
        private          VocalsTrack        _vocalsTrack;

        public event Action<string, Color> OnGuidePitchChanged;

        /// <param name="dsp">The DSP processor that produces the audio.</param>
        /// <param name="dspHandle">The disposable handle returned by <c>AttachOutputDsp</c>.</param>
        /// <param name="vocalsTrack">The original (full-song) vocals track.</param>
        public GuidePitchManager(GuidePitchSynthDsp dsp, IDisposable dspHandle, VocalsTrack vocalsTrack)
        {
            _dsp         = dsp         ?? throw new ArgumentNullException(nameof(dsp));
            _dspHandle   = dspHandle   ?? throw new ArgumentNullException(nameof(dspHandle));
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

            _dsp.SetPart(GetEnabledPart());
            OnGuidePitchChanged?.Invoke(GetStatusString(), GetStatusColor());
        }

        /// <summary>
        /// Call when the practice section changes so the DSP resets its note scan.
        /// </summary>
        public void OnPracticeSectionChanged(VocalsTrack sectionTrack)
        {
            _vocalsTrack = sectionTrack ?? throw new ArgumentNullException(nameof(sectionTrack));

            // Clamp enabled index in case the new section has fewer parts
            if (_enabledHarmonyIndex >= _vocalsTrack.Parts.Count)
            {
                _enabledHarmonyIndex = -1;
            }

            // Re-push the current part (or null). SetPart() signals a scan reset in the DSP.
            _dsp.SetPart(GetEnabledPart());
            OnGuidePitchChanged?.Invoke(GetStatusString(), GetStatusColor());
        }

        public string GetStatusString()
        {
            // TODO: Localize?
            if (_enabledHarmonyIndex < 0)
            {
                return "OFF";
            }

            if (_vocalsTrack.Instrument != Instrument.Harmony)
            {
                return "ON";
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

        public void Dispose() => _dspHandle.Dispose();

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
