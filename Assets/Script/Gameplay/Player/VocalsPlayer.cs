using System;
using System.Linq;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
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
        public VocalsEngine Engine { get; private set; }

        public override BaseEngine BaseEngine => Engine;
        public override BaseStats Stats => Engine.EngineStats;

        [SerializeField]
        private GameObject _needleVisualContainer;
        [SerializeField]
        private ParticleGroup _hittingParticleGroup;

        public override float[] StarMultiplierThresholds { get; } =
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

            _hud = hud;

            // Get the notes from the specific harmony or solo part
            var multiTrack = chart.GetVocalsTrack(Player.Profile.CurrentInstrument);
            var track = multiTrack.Parts[Player.Profile.HarmonyIndex];

            OriginalNoteTrack = track.CloneAsInstrumentDifficulty();
            player.Profile.ApplyModifiers(OriginalNoteTrack);

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
            // Hit window is in semitones (total width).
            double hitWindow = Player.Profile.CurrentDifficulty switch
            {
                Difficulty.Easy   => 3.5,
                Difficulty.Medium => 3.0,
                Difficulty.Hard   => 2.5,
                Difficulty.Expert => 2.0,
                _ => throw new InvalidOperationException("Unreachable")
            };

            // These percentages may seem low, but accounting for delay,
            // plosives not being detected, etc., it's pretty good.
            double hitPercent = Player.Profile.CurrentDifficulty switch
            {
                Difficulty.Easy   => 0.325,
                Difficulty.Medium => 0.400,
                Difficulty.Hard   => 0.450,
                Difficulty.Expert => 0.575,
                _ => throw new InvalidOperationException("Unreachable")
            };

            EngineParams = new VocalsEngineParameters(hitWindow, hitPercent, true,
                IMicDevice.UPDATES_PER_SECOND, StarMultiplierThresholds);

            var engine = new YargVocalsEngine(NoteTrack, SyncTrack, EngineParams);

            engine.OnTargetNoteChanged += (note) =>
            {
                _lastTargetNote = note;
            };

            engine.OnPhraseHit += (percent) =>
            {
                _hud.ShowPhraseHit(percent);
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

            IsFc = true;

            ResetVisuals();
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
        private double GetTimeThreshold(double lastTime)
        {
            // Add an arbitrary value to prevent it from hiding too fast
            return lastTime + 1f / IMicDevice.UPDATES_PER_SECOND + 0.05;
        }

        protected override void UpdateVisuals(double time)
        {
            // Get combo meter fill
            float fill = 0f;
            if (Engine.State.PhraseTicksTotal != null)
            {
                fill = (float) (Engine.State.PhraseTicksHit / Engine.State.PhraseTicksTotal.Value);
                fill /= (float) EngineParams.PhraseHitPercent;
            }

            // Update HUD
            _hud.UpdateInfo(fill, Engine.EngineStats.ScoreMultiplier, (float) Engine.EngineStats.StarPowerAmount);

            if (GameManager.SongTime >= GetTimeThreshold(Engine.State.LastSingTime))
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
                float lerpRate = 30f;

                // Show needle
                if (!_needleVisualContainer.activeSelf)
                {
                    _needleVisualContainer.SetActive(true);

                    // Lerp 10 times faster if we've just started showing the needle
                    lerpRate *= 10f;
                }

                // Get the pitch, and move to the correct octave
                float pitch = Engine.State.PitchSang;
                if (_lastTargetNote is not null && !_lastTargetNote.IsNonPitched)
                {
                    float octavePitch = pitch % 12f;

                    // Because the octave wraps around, we need to try
                    // to surrounding octaves to see which value is the closest
                    float closestDist = float.PositiveInfinity;
                    for (int i = -1; i <= 1; i++)
                    {
                        float note = octavePitch + (_lastTargetNote.Octave + i) * 12f;
                        float dist = Mathf.Abs(_lastTargetNote.Pitch - note);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            pitch = note;
                        }
                    }
                }
                else
                {
                    // Hard code a value of one octave up to
                    // make the needle sit more in the middle
                    pitch += 12f;
                }

                // Set the position of the needle
                var z = GameManager.VocalTrack.GetPosForPitch(pitch);
                var lerp = Mathf.Lerp(transform.localPosition.z, z, Time.deltaTime * lerpRate);
                transform.localPosition = new Vector3(0f, 0f, lerp);

                // Show particles if hitting, stop them if not.
                if (GameManager.SongTime < GetTimeThreshold(Engine.State.LastHitTime))
                {
                    _hittingParticleGroup.Play();
                }
                else
                {
                    _hittingParticleGroup.Stop();
                }
            }
        }

        public override void UpdateWithTimes(double inputTime)
        {
            base.UpdateWithTimes(inputTime);

            Score = Engine.EngineStats.Score;
            Combo = Engine.EngineStats.Combo;
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

        protected override bool InterceptInput(ref GameInput input)
        {
            // Ignore SP in practice mode
            if (input.GetAction<VocalsAction>() == VocalsAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }
    }
}