using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Helpers;
using YARG.Input;
using YARG.Player;
using YARG.Settings;

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
        private MeshRenderer _needleRenderer;
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
        private double?   _lastHitTime;
        private double?   _lastSingTime;

        private VocalsPlayerHUD _hud;
        private VocalPercussionTrack _percussionTrack;
        private bool _shouldHideNeedle;

        private int _phraseIndex = -1;

        private const int NEEDLES_COUNT = 7;

        private SongChart _chart;

        public void Initialize(int index, int vocalIndex, YargPlayer player, SongChart chart,
            VocalsPlayerHUD hud, VocalPercussionTrack percussionTrack, int? lastHighScore)
        {
            if (IsInitialized)
            {
                return;
            }

            base.Initialize(index, player, chart, lastHighScore);

            // Needle materials have names starting from 1.
            var needleIndex = (vocalIndex % NEEDLES_COUNT) + 1;
            var materialPath = $"VocalNeedle/{needleIndex}";
            _needleRenderer.material = Addressables.LoadAssetAsync<Material>(materialPath).WaitForCompletion();

            // Update speed of particles
            var particles = _hittingParticleGroup.GetComponentsInChildren<ParticleSystem>();
            foreach (var system in particles)
            {
                // This interface is weird lol, `.main` is readonly but
                // doesn't need to be re-assigned, changes are forwarded automatically
                var main = system.main;

                var startSpeed = main.startSpeed;
                startSpeed.constant *= player.Profile.NoteSpeed;
                main.startSpeed = startSpeed;
                main.startColor = VocalTrack.Colors[Player.Profile.HarmonyIndex];
            }

            // Get the notes from the specific harmony or solo part

            var multiTrack = chart.GetVocalsTrack(Player.Profile.CurrentInstrument);

            var track = multiTrack.Parts[Player.Profile.HarmonyIndex];
            player.Profile.ApplyVocalModifiers(track);

            OriginalNoteTrack = track.CloneAsInstrumentDifficulty();
            NoteTrack = OriginalNoteTrack;

            _phraseIndex = -1;

            // Initialize player specific vocal visuals

            hud.Initialize(player.EnginePreset);
            _hud = hud;

            percussionTrack.Initialize(NoteTrack.Notes);
            _percussionTrack = percussionTrack;

            _hud.ShowPlayerName(player, needleIndex);

            // Create and start an input context for the mic
            if (!Player.IsReplay && player.Bindings.Microphone != null)
            {
                _inputContext = new MicInputContext(player.Bindings.Microphone, GameManager);
                _inputContext.Start();
            }

            Engine = CreateEngine();

            Engine.OnComboIncrement += OnComboIncrement;
            Engine.OnComboReset += OnComboReset;

            if (vocalIndex == 0)
            {
                if (Player.Profile.CurrentInstrument == Instrument.Vocals)
                {
                    Engine.BuildCountdownsFromSelectedPart();
                }
                else
                {
                    Engine.BuildCountdownsFromAllParts(multiTrack.Parts);
                }

                Engine.OnCountdownChange += (countdownLength, endTime) =>
                {
                    GameManager.VocalTrack.UpdateCountdown(countdownLength, endTime);
                };
            }

            if (GameManager.IsPractice)
            {
                Engine.SetSpeed(GameManager.SongSpeed >= 1 ? GameManager.SongSpeed : 1);
            }
            else
            {
                Engine.SetSpeed(GameManager.SongSpeed);
            }

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);
        }

        protected override void FinishDestruction()
        {
            _inputContext?.Stop();
        }

        protected VocalsEngine CreateEngine()
        {
            if (!Player.IsReplay)
            {
                var singToActivateStarPower = SettingsManager.Settings.VoiceActivatedVocalStarPower.Value;

                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.Vocals.Create(StarMultiplierThresholds,
                    Player.Profile.CurrentDifficulty, MicDevice.UPDATES_PER_SECOND, singToActivateStarPower);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (VocalsEngineParameters) Player.EngineParameterOverride;
            }

            // The hit window can just be taken from the params
            HitWindow = EngineParams.HitWindow;

            var engine = new YargVocalsEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);
            EngineContainer = GameManager.EngineManager.Register(engine, NoteTrack.Instrument, _chart);

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

                LastCombo = Combo;
            };

            engine.OnNoteHit += (_, note) =>
            {
                if (note.IsPercussion)
                {
                    _percussionTrack.HitPercussionNote(note);
                }
            };

            engine.OnNoteMissed += (_, _) =>
            {
                if (LastCombo >= 2)
                {
                    GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
                }

                LastCombo = Combo;
            };

            engine.OnSing += (singing) =>
            {
                _lastSingTime = singing
                    ? GameManager.InputTime
                    : null;
            };

            engine.OnHit += (hitting) =>
            {
                _lastHitTime = hitting
                    ? GameManager.InputTime
                    : null;
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

            _phraseIndex = -1;

            base.ResetPracticeSection();
        }

        protected override void UpdateInputs(double time)
        {
            // Push all inputs from mic
            if (!Player.IsReplay && _inputContext != null)
            {
                foreach (var input in _inputContext.GetInputsFromMic())
                {
                    var i = input;
                    OnGameInput(ref i);
                }
            }

            base.UpdateInputs(time);
        }

        private bool IsInThreshold(double currentTime, double? lastTime)
        {
            if (lastTime is null)
            {
                return false;
            }

            return currentTime - lastTime.Value <= 1f / EngineParams.ApproximateVocalFps + 0.05;
        }

        protected override void UpdateVisuals(double visualTime)
        {
            UpdatePercussionPhrase(visualTime);
            UpdateSingNeedle();

            // Get combo meter fill
            float fill = 0f;
            if (Engine.PhraseTicksTotal != null && Engine.PhraseTicksTotal.Value != 0)
            {
                fill = (float) (Engine.PhraseTicksHit / Engine.PhraseTicksTotal.Value);
                fill /= (float) EngineParams.PhraseHitPercent;
            }

            // Update HUD
            _hud.UpdateInfo(fill, Engine.EngineStats.ScoreMultiplier,
                (float) Engine.GetStarPowerBarAmount(), Engine.EngineStats.IsStarPowerActive);

        }

        private float GetNeedleRotation(float pitchDist)
        {
            const float NEEDLE_ROT_MAX = 12f;

            // Reduce the provided distance by applying a dead zone. This will prevent oversteer if the player's current pitch is well within the "Perfect" window.
            var deadzoneInSemitones = EngineParams.PitchWindowPerfect / 2;
            var adjustedPitchDist = ApplyPitchDeadZone(pitchDist, deadzoneInSemitones);

            // Determine how off that is compared to the hit window
            float distPercent = Mathf.Clamp(adjustedPitchDist / (EngineParams.PitchWindow - deadzoneInSemitones), -1f, 1f);

            // Use that to get the target rotation
            return distPercent * NEEDLE_ROT_MAX;
        }

        private float ApplyPitchDeadZone(float pitchDist, float deadZoneInSemitones)
        {
            if (pitchDist >= 0.0f)
            {
                return Mathf.Max(0.0f, pitchDist - deadZoneInSemitones);
            }

            return Mathf.Min(0.0f, pitchDist + deadZoneInSemitones);
        }

        private void UpdateSingNeedle()
        {
            const float NEEDLE_POS_LERP = 30f;
            const float NEEDLE_POS_SNAP_MULTIPLIER = 10f;

            const float NEEDLE_ROT_LERP = 25f;

            // Get the appropriate sing time
            var singTime = GameManager.InputTime - Player.Profile.InputCalibrationSeconds;

            // Get whether or not the player has sang within the time threshold.
            // We gotta use a threshold here because microphone inputs are passed every X seconds,
            // not in a constant stream.
            if (!IsInThreshold(singTime, _lastSingTime) || _shouldHideNeedle)
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

                if (_lastTargetNote is not null && IsInThreshold(singTime, _lastHitTime))
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
                        (float pitchDist, _) = GetPitchDistanceIgnoringOctave(lastNotePitch, Engine.PitchSang);
                        targetRotation = GetNeedleRotation(pitchDist);
                    }
                    else
                    {
                        // If the note is non-pitched, just use the singing position
                        pitch = Engine.PitchSang + 12f;
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
                    float pitch = Engine.PitchSang;
                    if (_lastTargetNote is not null && !_lastTargetNote.IsNonPitched)
                    {
                        (_, int octaveShift) = GetPitchDistanceIgnoringOctave(lastNotePitch, pitch);

                        int lastNoteOctave = (int) (lastNotePitch / 12f);

                        // Set the pitch's octave to the target one
                        pitch = Engine.PitchSang % 12f;
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

        private void UpdatePercussionPhrase(double time)
        {
            // Prevent the HUD from hiding too quickly
            if (time < 0)
            {
                return;
            }

            // Since phrases start at the note, and not sometime before it, use
            // the end times of phrases instead (where the phrase lines are). Problem
            // with this is that we still gotta account for the first phrase, so use
            // an index of -1 for that.
            while (_phraseIndex == -1 ||
                (_phraseIndex < NoteTrack.Notes.Count && NoteTrack.Notes[_phraseIndex].TimeEnd <= time))
            {
                _phraseIndex++;

                // End if that's the last note
                if (_phraseIndex >= NoteTrack.Notes.Count)
                {
                    break;
                }

                var phrase = NoteTrack.Notes[_phraseIndex];

                bool hasPercussion = false;
                uint totalTime = 0;
                foreach (var note in phrase.ChildNotes)
                {
                    if (note.IsPercussion)
                    {
                        hasPercussion = true;
                        continue;
                    }

                    totalTime += note.TotalTickLength;
                }

                _hud.SetHUDShowing(totalTime != 0);
                _percussionTrack.ShowPercussionFret(hasPercussion);
                _shouldHideNeedle = hasPercussion;
            }
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

            _phraseIndex = -1;

            Engine = CreateEngine();
            ResetPracticeSection();
        }

        public override void SetStemMuteState(bool muted)
        {
            // Vocals has no stem muting
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
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

        public override (ReplayFrame Frame, ReplayStats Stats) ConstructReplayData()
        {
            var frame = new ReplayFrame(Player.Profile, EngineParams, Engine.EngineStats, ReplayInputs.ToArray());
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name));
        }
    }
}
