using System;
using System.Linq;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Helpers;
using YARG.Input;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public class VocalsPlayer : BasePlayer
    {
        public VocalsEngineParameters EngineParams { get; private set; }
        public VocalsEngine           Engine       { get; private set; }

        public override BaseEngine BaseEngine => Engine;

        [SerializeField]
        private GameObject _needleVisualContainer;
        [SerializeField]
        private Transform _needleTransform;
        [SerializeField]
        private ParticleGroup _hittingParticleGroup;

        public override bool ShouldUpdateInputsOnResume => false;
        
        public override float[] StarMultiplierThresholds { get; protected set; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.18f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        private InstrumentDifficulty<VocalNote> NoteTrack { get; set; }
        private InstrumentDifficulty<VocalNote> OriginalNoteTrack { get; set; }

        private MicInputContext _inputContext;

        private VocalNote _lastTargetNote;

        private VocalsPlayerHUD _hud;

        public void Initialize(int index, YargPlayer player, SongChart chart, VocalsPlayerHUD hud)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart);

            hud.Initialize(player.EnginePreset);
            _hud = hud;

            // Get the notes from the specific harmony or solo part
            var multiTrack = chart.GetVocalsTrack(Player.Profile.CurrentInstrument);
            var partIndex = Player.Profile.CurrentInstrument == Instrument.Harmony
                ? Player.Profile.HarmonyIndex
                : 0;
            var track = multiTrack.Parts[partIndex];
            player.Profile.ApplyVocalModifiers(track);

            OriginalNoteTrack = track.CloneAsInstrumentDifficulty();
            NoteTrack = OriginalNoteTrack;

            // Create and start an input context for the mic
            if (!GameManager.IsReplay && player.Bindings.Microphone is not null)
            {
                _inputContext = new MicInputContext(player.Bindings.Microphone, GameManager);
                _inputContext.Start();
            }

            Engine = CreateEngine();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);
        }

        protected override void FinishDestruction()
        {
            _inputContext?.Stop();
        }

        protected VocalsEngine CreateEngine()
        {
            if (!GameManager.IsReplay)
            {
                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.Vocals.Create(StarMultiplierThresholds, Player.Profile.CurrentDifficulty, MicDevice.UPDATES_PER_SECOND);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (VocalsEngineParameters) Player.EngineParameterOverride;
            }

            // The hit window can just be taken from the params
            HitWindow = EngineParams.HitWindow;

            var engine = new YargVocalsEngine(NoteTrack, SyncTrack, EngineParams);

            engine.OnStarPowerPhraseHit += _ => OnStarPowerPhraseHit();
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnTargetNoteChanged += (note) =>
            {
                _lastTargetNote = note;
            };

            engine.OnPhraseHit += (percent, fullPoints) =>
            {
                _hud.ShowPhraseHit(percent);

                if (!fullPoints)
                {
                    IsFc = false;
                }

                _lastCombo = Combo;
            };

            engine.OnNoteMissed += (_, _) =>
            {
                if (_lastCombo >= 2)
                {
                    GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
                }

                _lastCombo = Combo;
            };

            return engine;
        }

        protected override void ResetVisuals()
        {
            _lastTargetNote = null;
        }

        public override void ResetPracticeSection()
        {
            Engine.Reset(true);

            if (NoteTrack.Notes.Count > 0)
            {
                NoteTrack.Notes[0].OverridePreviousNote();
                NoteTrack.Notes[^1].OverrideNextNote();
            }

            base.ResetPracticeSection();
        }

        protected override void UpdateInputs(double time)
        {
            // Push all inputs from mic
            if (!GameManager.IsReplay && _inputContext is not null)
            {
                foreach (var input in _inputContext.GetInputsFromMic())
                {
                    var i = input;
                    OnGameInput(ref i);
                }
            }

            base.UpdateInputs(time);
        }

        /// <summary>
        /// Calculate if the engine considers this point in time as singing.
        /// </summary>
        private static double GetTimeThreshold(double lastTime)
        {
            // Add an arbitrary value to prevent it from hiding too fast
            return lastTime + 1f / MicDevice.UPDATES_PER_SECOND + 0.05;
        }

        protected override void UpdateVisuals(double time)
        {
            const float NEEDLE_POS_LERP = 30f;
            const float NEEDLE_POS_SNAP_MULTIPLIER = 10f;

            const float NEEDLE_ROT_LERP = 25f;
            const float NEEDLE_ROT_MAX = 12f;

            // Get combo meter fill
            float fill = 0f;
            if (Engine.State.PhraseTicksTotal != null)
            {
                fill = (float) (Engine.State.PhraseTicksHit / Engine.State.PhraseTicksTotal.Value);
                fill /= (float) EngineParams.PhraseHitPercent;
            }

            // Update HUD
            _hud.UpdateInfo(fill, Engine.EngineStats.ScoreMultiplier,
                (float) Engine.EngineStats.StarPowerAmount, Engine.EngineStats.IsStarPowerActive);

            // Get the appropriate sing time
            var singTime = GameManager.InputTime - Player.Profile.InputCalibrationSeconds;

            // Get whether or not the player has sang within the time threshold.
            // We gotta use a threshold here because microphone inputs are passed every X seconds,
            // not in a constant stream.
            if (singTime >= GetTimeThreshold(Engine.State.LastSingTime))
            {
                // Hide the needle if there's no singing
                if (_needleVisualContainer.activeSelf)
                {
                    _needleVisualContainer.SetActive(false);
                    _hittingParticleGroup.Stop();
                }
            }
            else
            {
                float lerpRate = NEEDLE_POS_LERP;

                // Show needle
                if (!_needleVisualContainer.activeSelf)
                {
                    _needleVisualContainer.SetActive(true);

                    // Lerp X times faster if we've just started showing the needle
                    lerpRate *= NEEDLE_POS_SNAP_MULTIPLIER;
                }

                var transformCache = transform;
                float lastNotePitch = _lastTargetNote?.PitchAtSongTime(GameManager.SongTime) ?? -1f;

                if (_lastTargetNote is not null && singTime < GetTimeThreshold(Engine.State.LastHitTime))
                {
                    // Show particles if hitting
                    _hittingParticleGroup.Play();

                    float pitch;
                    float targetRotation = 0f;

                    if (!_lastTargetNote.IsNonPitched)
                    {
                        // If the player is hitting, just set the needle position to the note
                        pitch = lastNotePitch;

                        // Rotate the needle a little bit depending on how off it is (unless it's non-pitched)
                        // Get how off the player is
                        (float pitchDist, _) = GetPitchDistanceIgnoringOctave(lastNotePitch, Engine.State.PitchSang);

                        // Determine how off that is compared to the hit window
                        float distPercent = Mathf.Clamp(pitchDist / (float) EngineParams.HitWindow.MaxWindow, -1f, 1f);

                        // Use that to get the target rotation
                        targetRotation = distPercent * NEEDLE_ROT_MAX;
                    }
                    else
                    {
                        // If the note is non-pitched, just use the singing position
                        pitch = Engine.State.PitchSang + 12f;
                    }

                    // Transform!
                    float z = GameManager.VocalTrack.GetPosForPitch(pitch);
                    var lerp = Mathf.Lerp(transformCache.localPosition.z, z, Time.deltaTime * lerpRate);
                    transformCache.localPosition = new Vector3(0f, 0f, lerp);
                    _needleTransform.rotation = Quaternion.Lerp(_needleTransform.rotation,
                        Quaternion.Euler(0f, targetRotation, 0f), Time.deltaTime * NEEDLE_ROT_LERP);
                }
                else
                {
                    // Stop particles if not hitting
                    _hittingParticleGroup.Stop();

                    // Since the player is not hitting the note here, we need to offset it correctly.
                    // Get the pitch, and move to the correct octave.
                    float pitch = Engine.State.PitchSang;
                    if (_lastTargetNote is not null && !_lastTargetNote.IsNonPitched)
                    {
                        (_, int octaveShift) = GetPitchDistanceIgnoringOctave(lastNotePitch, pitch);

                        int lastNoteOctave = (int) (lastNotePitch / 12f);

                        // Set the pitch's octave to the target one
                        pitch = Engine.State.PitchSang % 12f;
                        pitch += 12f * (lastNoteOctave + octaveShift);
                    }
                    else
                    {
                        // Hard code a value of one octave up to
                        // make the needle sit more in the middle
                        pitch += 12f;
                    }

                    // Set the position of the needle
                    var z = GameManager.VocalTrack.GetPosForPitch(pitch);
                    var lerp = Mathf.Lerp(transformCache.localPosition.z, z, Time.deltaTime * lerpRate);
                    transformCache.localPosition = new Vector3(0f, 0f, lerp);

                    // Lerp the rotation to none
                    _needleTransform.rotation = Quaternion.Lerp(_needleTransform.rotation,
                        Quaternion.identity, Time.deltaTime * NEEDLE_ROT_LERP);
                }
            }
        }

        /// <returns>
        /// The first value in the pair (<c>Distance</c>) is the distance between <paramref name="target"/> and '
        /// <paramref name="other"/> ignoring the octave.<br/>
        /// The second value in the pair (<c>OctaveShift</c>) is how much the <paramref name="target"/> octave
        /// had to be shifted in order for the closest distance to be found.
        /// </returns>
        /// <param name="target">The target note (as MIDI pitch).</param>
        /// <param name="other">The other note (as MIDI pitch).</param>
        private static (float Distance, int OctaveShift) GetPitchDistanceIgnoringOctave(float target, float other)
        {
            // Normalize the parameters
            target %= 12f;
            other %= 12f;

            // Start off with the current octave
            float closest = other - target;
            int octaveShift = 0;

            // Upper octave
            float upperDist = (other + 12f) - target;
            if (Mathf.Abs(upperDist) < Mathf.Abs(closest))
            {
                closest = upperDist;
                octaveShift = 1;
            }

            // Lower octave
            float lowerDist = (other - 12f) - target;
            if (Mathf.Abs(lowerDist) < Mathf.Abs(closest))
            {
                closest = lowerDist;
                octaveShift = -1;
            }

            return (closest, octaveShift);
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            var practiceNotes = OriginalNoteTrack.Notes.Where(n => n.Tick >= start && n.Tick < end).ToList();

            NoteTrack = new InstrumentDifficulty<VocalNote>(
                OriginalNoteTrack.Instrument,
                OriginalNoteTrack.Difficulty,
                practiceNotes,
                OriginalNoteTrack.Phrases,
                OriginalNoteTrack.TextEvents);

            Engine = CreateEngine();
            ResetPracticeSection();
        }

        public override void SetStemMuteState(bool muted)
        {
            // Vocals has no stem muting
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            // Ignore SP in practice mode
            if (input.GetAction<VocalsAction>() == VocalsAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }
    }
}