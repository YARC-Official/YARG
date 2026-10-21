using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
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

        private sealed class NeedleState
        {
            public VocalNeedleView View;
            public VocalNote Target;
            public VocalNote DisplayTarget;
            public double? LastSing;
            public double? LastHit;
            public int DisplayPart;
            public VocalsEngine Source;
            public void SetTarget(VocalNote note) => Target = note;
        }

        private readonly List<NeedleState> _needles = new();
        private readonly List<int> _needleIds = new();
        private readonly List<AsyncOperationHandle<Material>> _needleMaterials = new();
        private Instrument _chartSource;
        private int _micCount = 1;
        private List<SerializedMic> _microphoneSlots;
        private bool IsPartySession => Player.Profile.GameMode == GameMode.PartyVocals;

        public static Instrument ResolveChartSource(YargPlayer player, SongChart chart)
        {
            if (player.Profile.GameMode != GameMode.PartyVocals)
                return player.Profile.CurrentInstrument;
            return player.IsReplay
                ? ((VocalsEngineParameters) player.EngineParameterOverride).PartyVocalsChartSource
                : PartyVocalsChartSelection.Resolve(chart, PlayerContainer.GetPartyVocalsChartPreference());
        }

        public override bool ShouldUpdateInputsOnResume => false;

        protected override float[] StarMultiplierThresholds { get; set; } =
        {
            0.05f, 0.11f, 0.19f, 0.46f, 0.77f, 1.06f
        };

        private InstrumentDifficulty<VocalNote> NoteTrack { get; set; }
        private InstrumentDifficulty<VocalNote> OriginalNoteTrack { get; set; }

        private MicInputContext _inputContext;

        private VocalNote _lastTargetNote;
        private double?   _lastHitTime;
        private double?   _lastSingTime;
        private double    _previousStarPowerPercent;
        private bool      _hotStartChecked;
        private bool      _newHighScoreShown;

        private VocalsPlayerHUD _hud;
        private VocalPercussionTrack _percussionTrack;
        private bool _shouldHideNeedle;
        private bool _handlesCountdown;
        private List<VocalsPart> _allVocalParts;
        private List<VocalsPart> _originalAllVocalParts;
        private readonly Dictionary<(uint Tick, uint TickEnd), VocalNote> _percussionNoteLookup = new();

        private int _phraseIndex = -1;

        private const int NEEDLES_COUNT = 7;

        private SongChart _chart;

        public void Initialize(int index, int vocalIndex, YargPlayer player, SongChart chart,
            VocalsPlayerHUD hud, VocalPercussionTrack percussionTrack, int? lastHighScore, float trackSpeed, Instrument chartSource)
        {
            if (IsInitialized)
            {
                return;
            }

            base.Initialize(index, player, chart, lastHighScore);

            // Save the chart
            _chart = chart;

            _chartSource = chartSource;
            var multiTrack = chart.GetVocalsTrack(_chartSource);
            if (IsPartySession && !player.IsReplay)
                _microphoneSlots = player.Bindings.Microphones.Select(mic => mic.Serialize())
                    .Concat(player.Bindings.UnresolvedMicrophones).ToList();
            _micCount = IsPartySession
                ? (player.IsReplay ? ((VocalsEngineParameters) player.EngineParameterOverride).PartyVocalsMicCount
                    : Mathf.Max(1, _microphoneSlots.Count))
                : 1;
            if (_micCount > VocalsEngineParameters.MAX_PARTY_MIC_COUNT)
                throw new System.InvalidOperationException("Too many microphones for a Party Vocals session.");
            var needleIndex = (vocalIndex % NEEDLES_COUNT) + 1;
            InitializeNeedles(needleIndex, trackSpeed);

            if (Player.Profile.CurrentInstrument == Instrument.PartyVocals)
            {
                // Free Harmonies: clone and modify ALL parts so that vocal modifiers
                // (UnpitchedOnly, NoVocalPercussion) apply to every HARM line the
                // coordinator matches against, not just the primary chart.
                _allVocalParts = new List<VocalsPart>();
                foreach (var part in multiTrack.Parts)
                {
                    var cloned = part.Clone();
                    player.Profile.ApplyVocalModifiers(cloned);
                    _allVocalParts.Add(cloned);
                }
                // Pristine full-song snapshot (taken post-modifier so practice
                // sections never re-apply them). Practice filtering is destructive,
                // so each SetPracticeSection must clone from this, not from the
                // possibly-already-filtered _allVocalParts. The extra copy also
                // keeps the engine's mutable notes distinct from the snapshot.
                _originalAllVocalParts = _allVocalParts.Select(part => part.Clone()).ToList();
            }
            else
            {
                _allVocalParts = multiTrack.Parts;
            }

            _handlesCountdown = vocalIndex == 0;

            VocalsPart track;
            if (Player.Profile.CurrentInstrument == Instrument.PartyVocals)
            {
                // Already cloned and modified above; use HARM1 as the primary chart.
                track = _allVocalParts[0];
            }
            else
            {
                track = multiTrack.Parts[Player.Profile.HarmonyIndex];
                player.Profile.ApplyVocalModifiers(track);
            }

            OriginalNoteTrack = track.CloneAsInstrumentDifficulty();
            NoteTrack = OriginalNoteTrack;

            _phraseIndex = -1;
            _previousStarPowerPercent = 0.0;

            // Initialize player specific vocal visuals

            hud.Initialize(player.EnginePreset);
            _hud = hud;

            percussionTrack.Initialize(NoteTrack.Notes);
            _percussionTrack = percussionTrack;
            RebuildPercussionNoteLookup();

            bool[] connectedSlots = null;
            if (IsPartySession && !player.IsReplay && !player.Profile.IsBot && _microphoneSlots.Count > 0)
            {
                connectedSlots = new bool[_micCount];
                // Session slots contain connected bindings first, then unresolved bindings.
                // A name match cannot distinguish two microphones of the same model.
                for (int mic = 0; mic < player.Bindings.Microphones.Count; mic++)
                    connectedSlots[mic] = true;
            }
            _hud.ShowPlayerName(player, _needleIds, connectedSlots);

            // Create and start an input context for the mics
            if (!Player.IsReplay && (player.Bindings.Microphones.Count > 0 || _microphoneSlots?.Count > 0))
            {
                _inputContext = new MicInputContext(player.Bindings.Microphones, GameManager, IsPartySession, _microphoneSlots);
                _inputContext.Start();
            }

            Engine = CreateEngine();
            if (Engine is PartyVocalsCoordinatorEngine coordinator &&
                (!IsPartySession || _chartSource == Instrument.Harmony))
            {
                // Capture full-song membership once. Practice engines have filtered parts.
                var availableParts = new bool[coordinator.PartCount];
                for (int part = 0; part < availableParts.Length; part++)
                    availableParts[part] = coordinator.PartHasContent(part);
                _hud.InitializeHarmonyMeters(availableParts, VocalTrack.Colors);
            }

            if (GameManager.IsPractice)
            {
                Engine.SetSpeed(GameManager.SongSpeed >= 1 ? GameManager.SongSpeed : 1);
            }
            else
            {
                Engine.SetSpeed(GameManager.SongSpeed);
            }

        }

        protected override void FinishDestruction()
        {
            DetachPartyPhraseHandler();
            _inputContext?.Stop();
            DetachNeedleSources();
            foreach (var handle in _needleMaterials) Addressables.Release(handle);
            _needleMaterials.Clear();
        }

        protected override void OnMenuInput(YargPlayer _, ref GameInput input)
        {
            if (input.Action != (int) MenuAction.Start || !input.Button || !GameManager.CanPause)
            {
                return;
            }

            GameManager.TogglePause();
        }

        protected VocalsEngine CreateEngine()
        {
            DetachPartyPhraseHandler();
            DetachNeedleSources();
            if (!Player.IsReplay)
            {
                var singToActivateStarPower = SettingsManager.Settings.VoiceActivatedVocalStarPower.Value;

                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.Vocals.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds,
                    Player.Profile.CurrentDifficulty, MicDevice.UPDATES_PER_SECOND, singToActivateStarPower,
                    partyVocalsMicCount: _micCount,
                    partyVocalsChartSource: IsPartySession ? _chartSource : Instrument.Harmony);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (VocalsEngineParameters) Player.EngineParameterOverride;
            }

            // The hit window can just be taken from the params
            HitWindow = EngineParams.HitWindow;

            // Construct the appropriate engine based on the instrument.
            // PartyVocals uses the coordinator (multi-part matching across all HARM parts);
            // everything else uses the standard solo/harmony engine.
            VocalsEngine engine;
            var registeredTrack = NoteTrack;
            if (Player.Profile.CurrentInstrument == Instrument.PartyVocals)
            {
                var mergedNoteTrack = PartyVocalsCoordinatorEngine.BuildMergedTrack(_allVocalParts, NoteTrack);
                engine = new PartyVocalsCoordinatorEngine(
                    mergedNoteTrack, _allVocalParts, SyncTrack, EngineParams,
                    Player.Profile.IsBot, micCount: _micCount);
                if (IsPartySession) registeredTrack = mergedNoteTrack;
            }
            else
            {
                engine = new YargVocalsEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);
            }

            EngineContainer = GameManager.EngineManager.Register(engine, registeredTrack, Player.Profile.HarmonyIndex, _chart, Player.RockMeterPreset);

            if (engine is PartyVocalsCoordinatorEngine coordinator)
                coordinator.OnPartyVocalsPhrase += OnPartyVocalsPhrase;

            engine.OnComboIncrement += OnComboIncrement;
            engine.OnComboReset += OnComboReset;

            engine.OnStarPowerPhraseHit += _ => OnStarPowerPhraseHit();
            engine.OnStarPowerStatus += OnStarPowerStatus;
            engine.OnStarPowerReady += OnStarPowerReady;

            engine.OnTargetNoteChanged += (note) =>
            {
                _lastTargetNote = note;
            };

            engine.OnPhraseHit += (percent, fullPoints, isLastPhrase) =>
            {
                if (!fullPoints)
                {
                    IsFc = false;
                }

                LastCombo = Combo;

                ShowTextNotifications(isLastPhrase);

                // Order is important here. ShowVocalPhraseResult() will skip showing AWESOME! if other, more important notifications are already showing.
                // The party event follows this callback. Keep streak/FC priority here,
                // but let its authoritative grade supply successful phrase feedback.
                _hud.ShowPhraseHit(percent, Combo,
                    engine is not PartyVocalsCoordinatorEngine || !fullPoints);
            };

            engine.OnNoteHit += (_, note) =>
            {
                if (note.IsPercussion && _percussionNoteLookup.TryGetValue(
                        (note.Tick, note.TickEnd), out var visualNote))
                {
                    _percussionTrack.HitPercussionNote(visualNote);
                }
            };

            engine.OnNoteMissed += (_, note) =>
            {
                if (note.IsVocalPhrase)
                {
                    if (LastCombo >= 2)
                    {
                        GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
                    }

                    LastCombo = Combo;
                    _hud.SetFullCombo(false);
                }
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

            if (_handlesCountdown)
            {
                if (Player.Profile.CurrentInstrument == Instrument.Vocals)
                {
                    engine.BuildCountdownsFromSelectedPart();
                }
                else
                {
                    engine.BuildCountdownsFromAllParts(_allVocalParts);
                }

                engine.OnCountdownChange += (countdownLength, endTime) =>
                {
                    GameManager.VocalTrack.UpdateCountdown(countdownLength, endTime);
                };
            }

            if (engine is PartyVocalsCoordinatorEngine party)
            {
                for (int mic = 0; mic < _needles.Count; mic++)
                {
                    var needle = _needles[mic];
                    needle.Source = party.SubEngines[mic];
                    needle.Source.OnTargetNoteChanged += needle.SetTarget;
                }
            }
            return engine;
        }

        private void DetachPartyPhraseHandler()
        {
            if (Engine is PartyVocalsCoordinatorEngine coordinator)
                coordinator.OnPartyVocalsPhrase -= OnPartyVocalsPhrase;
        }

        private void OnPartyVocalsPhrase(PhraseGrade grade, IReadOnlyList<PartyPartResult> parts, bool isLastPhrase)
        {
            _hud.ShowPartyVocalsGrade(grade, Combo);
        }

        protected override void ResetVisuals()
        {
            _lastTargetNote = null;
            _lastSingTime = null;
            _lastHitTime = null;
            foreach (var needle in _needles)
            {
                needle.Target = null;
                needle.DisplayTarget = null;
                needle.LastSing = null;
                needle.LastHit = null;
                needle.View.Reset();
            }
            _hud.SetFullCombo(IsFc);
            _hud.ResetHarmonyMeters();
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
            _percussionTrack.Initialize(NoteTrack.Notes);
            RebuildPercussionNoteLookup();

            base.ResetPracticeSection();
        }

        public override void Rewind(double visualTime)
        {
            foreach (var needle in _needles) needle.View.Stop();
        }

        public override void PostRewind(double visualTime)
        {
            ResetVisuals();
            UpdateVisuals(visualTime);
        }

        protected override void UpdateInputs(double time)
        {
            if (Engine is PartyVocalsCoordinatorEngine coordinator) coordinator.ResetMicSangFlags();
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

            // In multiplayer, don't double the score multiplier in the strikeline element
            // Otherwise, it looks like the band multiplier applies on top of the score multiplier
            var engineStats = Engine.EngineStats;
            int displayMultiplier = GameManager.TotalPlayers > 1 && engineStats.IsStarPowerActive
                ? engineStats.ScoreMultiplier / 2
                : engineStats.ScoreMultiplier;

            if (Engine is PartyVocalsCoordinatorEngine coordinator)
            {
                double beatPhase = GameManager.BeatEventHandler.Visual.StrongBeat.CurrentPercentage;
                for (int part = 0; part < coordinator.PartCount; part++)
                {
                    var state = HarmonyMeterPresentation.Evaluate(
                        coordinator.PartInCurrentPhrase(part), coordinator.PartInNextPhrase(part),
                        coordinator.CurrentPhraseProgress, coordinator.CanonicalMeters[part],
                        coordinator.AwesomeThreshold, beatPhase);
                    _hud.UpdateHarmonyMeter(part, state, IsFc);
                }
            }

            // Update HUD
            _hud.UpdateInfo(fill, displayMultiplier,
                (float) Engine.GetStarPowerBarAmount(), Engine.EngineStats.IsStarPowerActive);
        }

        protected override void OnStarPowerReady()
        {
            base.OnStarPowerReady();
            _hud.ShowNotification(TextNotificationType.StarPowerReady);
        }

        private void ShowTextNotifications(bool isLastPhrase)
        {
            if (SettingsManager.Settings.DisableTextNotifications.Value)
            {
                return;
            }

            var isStarPowerActive = Engine.EngineStats.IsStarPowerActive;
            _previousStarPowerPercent = Engine.GetStarPowerBarAmount();

            var isMaxMultiplier = Engine.EngineStats.ScoreMultiplier == (isStarPowerActive ? 8 : 4);

            if (!_hotStartChecked && isMaxMultiplier && IsFc)
            {
                _hud.ShowNotification(TextNotificationType.HotStart);
                _hotStartChecked = true;
            }

            if (LastHighScore != null && !_newHighScoreShown && Score > LastHighScore)
            {
                _hud.ShowNotification(TextNotificationType.NewHighScore);
                _newHighScoreShown = true;
            }

            if (!isLastPhrase)
            {
                return;
            }
            if (IsFc)
            {
                _hud.ShowNotification(TextNotificationType.FullCombo);
            }
            else if (isMaxMultiplier)
            {
                _hud.ShowNotification(TextNotificationType.StrongFinish);
            }
        }

        private void InitializeNeedles(int firstMaterial, float trackSpeed)
        {
            transform.localPosition = Vector3.zero;
            // Clone only the visual subtree; the profile still owns one player and HUD.
            var originalRoot = _hittingParticleGroup.transform.parent;
            for (int mic = 0; mic < _micCount; mic++)
            {
                var root = mic == 0 ? originalRoot : Instantiate(originalRoot, transform);
                var renderer = mic == 0 ? _needleRenderer : FindClonedTransform(_needleRenderer.transform, originalRoot, root).GetComponent<MeshRenderer>();
                var model = renderer.gameObject;
                var rotation = mic == 0 ? _needleTransform : FindClonedTransform(_needleTransform, originalRoot, root);
                var particles = mic == 0 ? _hittingParticleGroup : FindClonedTransform(_hittingParticleGroup.transform, originalRoot, root).GetComponent<ParticleGroup>();
                var needleId = (firstMaterial - 1 + mic) % NEEDLES_COUNT + 1;
                _needleIds.Add(needleId);
                var material = Addressables.LoadAssetAsync<Material>($"VocalNeedle/{needleId}");
                _needleMaterials.Add(material);
                renderer.sharedMaterial = material.WaitForCompletion();
                foreach (var system in particles.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    var speed = main.startSpeed;
                    speed.constant *= mic == 0 ? trackSpeed : 1f;
                    main.startSpeed = speed;
                }
                _needles.Add(new NeedleState
                {
                    View = new VocalNeedleView(root, model, renderer, rotation, particles)
                });
            }
        }

        private static Transform FindClonedTransform(Transform original, Transform originalRoot, Transform cloneRoot)
        {
            var path = new Stack<int>();
            for (var current = original; current != originalRoot; current = current.parent)
                path.Push(current.GetSiblingIndex());
            while (path.Count > 0) cloneRoot = cloneRoot.GetChild(path.Pop());
            return cloneRoot;
        }

        private void DetachNeedleSources()
        {
            foreach (var needle in _needles)
            {
                if (needle.Source != null) needle.Source.OnTargetNoteChanged -= needle.SetTarget;
                needle.Source = null;
                needle.Target = null;
                needle.DisplayTarget = null;
                needle.LastSing = null;
                needle.LastHit = null;
            }
        }

        private void UpdateSingNeedle()
        {
            for (int mic = 0; mic < _needles.Count; mic++)
            {
                var needle = _needles[mic];
                float pitch = Engine.PitchSang;
                int part = Player.Profile.HarmonyIndex;
                var target = _lastTargetNote;
                bool singing = IsInThreshold(GameManager.InputTime, _lastSingTime);
                bool hitting = IsInThreshold(GameManager.InputTime, _lastHitTime);
                if (Engine is PartyVocalsCoordinatorEngine party)
                {
                    if (party.DidMicSingThisTick(mic)) needle.LastSing = GameManager.InputTime;
                    int matchedPart = party.GetMicDisplayPart(mic);
                    if (matchedPart >= 0)
                    {
                        needle.LastHit = GameManager.InputTime;
                        needle.DisplayPart = matchedPart;
                        needle.DisplayTarget = party.GetMicDisplayNote(mic);
                    }
                    else if (party.DidMicSingThisTick(mic))
                    {
                        needle.LastHit = null;
                    }
                    singing = IsInThreshold(GameManager.InputTime, needle.LastSing);
                    hitting = IsInThreshold(GameManager.InputTime, needle.LastHit);
                    pitch = party.GetMicPitch(mic);
                    part = needle.DisplayPart;
                    target = hitting ? needle.DisplayTarget ?? needle.Target : needle.Target;
                }
                needle.View.Update(target, pitch, singing, hitting, part, GameManager, EngineParams, _shouldHideNeedle);
            }
        }

        private void UpdatePercussionPhrase(double time)
        {
            // Prevent the HUD from hiding too quickly
            if (time < 0)
            {
                return;
            }

            while (ShouldAdvancePhraseIndex(time))
            {
                _phraseIndex++;

                // We've reached the end. No need to continue.
                if (_phraseIndex >= NoteTrack.Notes.Count)
                {
                    SetPercussionMode(false);
                    return;
                }

                var phrase = NoteTrack.Notes[_phraseIndex];
                SetPercussionMode(HasPercussion(phrase));
            }
        }

        private bool ShouldAdvancePhraseIndex(double time)
        {
            // Since phrases start at the note, and not sometime before it, use
            // the end times of phrases instead (where the phrase lines are). Problem
            // with this is that we still gotta account for the first phrase, so use
            // an index of -1 for that.
            bool beforeFirstPhrase = _phraseIndex == -1;
            if (beforeFirstPhrase)
            {
                // Track has no notes. Bail early.
                if (NoteTrack.Notes.Count <= 0)
                {
                    return false;
                }

                var firstPhrase = NoteTrack.Notes[0];
                var firstPhraseHasStarted = firstPhrase.Time <= time;
                return firstPhraseHasStarted || HasPercussion(firstPhrase);
            }

            bool atTheEndOfTrack = _phraseIndex >= NoteTrack.Notes.Count;
            if (atTheEndOfTrack)
            {
                return false;
            }

            var currentPhrase = NoteTrack.Notes[_phraseIndex];
            return currentPhrase.TimeEnd <= time;
        }

        private void SetPercussionMode(bool show)
        {
            _hud.SetHUDShowing(!show);
            _percussionTrack.ShowPercussionFret(show);
            _shouldHideNeedle = show;
        }

        private static bool HasPercussion(VocalNote phrase)
        {
            foreach (var note in phrase.ChildNotes)
            {
                if (note.IsPercussion)
                {
                    return true;
                }
            }

            return false;
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            var practiceNotes = OriginalNoteTrack.Notes.Where(n => IsVocalPhraseInPracticeRange(n, start, end)).ToList();

            NoteTrack = new InstrumentDifficulty<VocalNote>(
                OriginalNoteTrack.Instrument,
                OriginalNoteTrack.Difficulty,
                practiceNotes,
                OriginalNoteTrack.Phrases,
                OriginalNoteTrack.TextEvents);

            if (Player.Profile.CurrentInstrument == Instrument.PartyVocals)
            {
                _allVocalParts = _originalAllVocalParts.Select(part =>
                {
                    var sectionPart = part.Clone();
                    sectionPart.NotePhrases.RemoveAll(phrase =>
                        !IsVocalPhraseInPracticeRange(phrase.PhraseParentNote, start, end));
                    return sectionPart;
                }).ToList();
            }

            _phraseIndex = -1;

            // Removed by EngineManager
            EngineContainer = null;

            Engine = CreateEngine();
            Engine.SetSpeed(GameManager.SongSpeed >= 1 ? GameManager.SongSpeed : 1);
            ResetPracticeSection();
        }

        private void RebuildPercussionNoteLookup()
        {
            _percussionNoteLookup.Clear();
            foreach (var phrase in NoteTrack.Notes)
            {
                foreach (var note in phrase.ChildNotes)
                {
                    if (note.IsPercussion)
                    {
                        _percussionNoteLookup[(note.Tick, note.TickEnd)] = note;
                    }
                }
            }
        }

        private static bool IsVocalPhraseInPracticeRange(VocalNote note, uint start, uint end)
        {
            if (note.Tick >= start && note.Tick < end)
            {
                return true;
            }

            return note.ChildNotes.Count > 0 &&
                note.ChildNotes.All(child => child.Tick >= start && child.TotalTickEnd <= end);
        }

        public override void SetStemMuteState(bool muted)
        {
            // Vocals has no stem muting
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            var minimumTime = System.Math.Max(BaseEngine.LastQueuedInputTime, BaseEngine.CurrentTime);
            if (input.Time < minimumTime)
            {
                input = new GameInput(minimumTime, input.Action, input.Integer);
            }

            return false;
        }

        public override (ReplayFrame Frame, ReplayStats Stats) ConstructReplayData()
        {
            var frame = new ReplayFrame(Player.Profile, EngineParams, Engine.EngineStats, ReplayInputs.ToArray());
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name, Player.IsReplay));
        }
    }
}
