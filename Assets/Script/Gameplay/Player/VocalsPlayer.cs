using UnityEngine;
using YARG.Audio;
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

        protected InstrumentDifficulty<VocalNote> NoteTrack { get; private set; }

        private MicInputContext _inputContext;

        private VocalNote _lastTargetNote;

        private VocalsPlayerHUD _hud;

        public void Initialize(int index, YargPlayer player, SongChart chart, VocalsPlayerHUD hud)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart);

            _hud = hud;

            // TODO: Selectable harmony part
            // Get the notes from the specific harmony or solo part
            var multiTrack = chart.GetVocalsTrack(Player.Profile.CurrentInstrument);
            var track = multiTrack.Parts[0];
            NoteTrack = track.CloneAsInstrumentDifficulty();

            // Create and start an input context for the mic
            if (!GameManager.IsReplay)
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
            EngineParams = new VocalsEngineParameters(1.0, 0.7,
                IMicDevice.UPDATES_PER_SECOND, StarMultiplierThresholds);

            var engine = new YargVocalsEngine(NoteTrack, SyncTrack, EngineParams);

            engine.OnTargetNoteChanged += (note) =>
            {
                _lastTargetNote = note;
            };

            return engine;
        }

        protected override void ResetVisuals()
        {
            _lastTargetNote = null;
        }

        public override void ResetPracticeSection()
        {
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
            _hud.UpdateInfo(fill, Engine.EngineStats.ScoreMultiplier);

            if (GameManager.SongTime >= GetTimeThreshold(Engine.State.VisualLastSingTime))
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
                if (_lastTargetNote is not null)
                {
                    float micNote = pitch % 12f;

                    // TODO: THIS DOES NOT WORK

                    // Since the hit detection rolls over to the next/last octave,
                    // we must check the neighbouring octaves as well to see if it's
                    // closer, and use that instead.
                    float closestDist = float.PositiveInfinity;
                    for (int i = -1; i <= 1; i++)
                    {
                        float note = micNote + (_lastTargetNote.Octave + i) * 12f;
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
                if (GameManager.SongTime < GetTimeThreshold(Engine.State.VisualLastHitTime))
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
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
        }
    }
}