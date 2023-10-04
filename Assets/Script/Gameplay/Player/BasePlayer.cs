using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Player;
using YARG.Settings;

namespace YARG.Gameplay.Player
{
    public abstract class BasePlayer : GameplayBehaviour
    {
        public const float STRIKE_LINE_POS       = -2f;
        public const float DEFAULT_ZERO_FADE_POS = 3f;
        public const float NOTE_SPAWN_OFFSET     = 5f;

        public const float TRACK_WIDTH = 2f;

        public double SpawnTimeOffset => (ZeroFadePosition + 2 + -STRIKE_LINE_POS) / NoteSpeed;

        [field: Header("Visuals")]
        [field: SerializeField]
        public Camera TrackCamera { get; private set; }

        [SerializeField]
        protected CameraPositioner CameraPositioner;
        [SerializeField]
        protected TrackMaterial TrackMaterial;
        [SerializeField]
        protected ComboMeter ComboMeter;
        [SerializeField]
        protected StarpowerBar StarpowerBar;
        [SerializeField]
        protected SunburstEffects SunburstEffects;
        [SerializeField]
        protected HitWindowDisplay HitWindowDisplay;

        [Header("Pools")]
        [SerializeField]
        protected KeyedPool NotePool;
        [SerializeField]
        protected Pool BeatlinePool;

        protected TrackView TrackView { get; private set; }

        protected SyncTrack SyncTrack { get; private set; }

        private List<GameInput> _replayInputs;

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        public YargPlayer Player { get; private set; }

        public float NoteSpeed
        {
            get
            {
                if (GameManager.IsPractice && GameManager.SelectedSongSpeed < 1)
                {
                    return Player.Profile.NoteSpeed;
                }

                return Player.Profile.NoteSpeed / GameManager.SelectedSongSpeed;
            }
        }

        public abstract BaseStats Stats { get; }

        public abstract float[] StarMultiplierThresholds { get; }

        public abstract int[] StarScoreThresholds { get; protected set; }

        public float ZeroFadePosition { get; private set; }
        public float FadeSize         { get; private set; }

        public int Score { get; protected set; }
        public int Combo { get; protected set; }

        public int NotesHit   { get; protected set; }
        public int TotalNotes { get; protected set; }

        protected bool IsFc;

        protected bool IsInitialized { get; private set; }

        protected List<Beatline> Beatlines;

        protected int BeatlineIndex;

        protected override void GameplayAwake()
        {
            _replayInputs = new List<GameInput>();

            IsFc = true;
        }

        public virtual void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView)
        {
            if (IsInitialized) return;

            Player = player;
            TrackView = trackView;

            SyncTrack = chart.SyncTrack;

            if (GameManager.IsReplay)
            {
                _replayInputs = new List<GameInput>(GameManager.Replay.Frames[index].Inputs);
                Debug.Log("Initialized replay inputs with " + _replayInputs.Count + " inputs");
            }

            Beatlines = SyncTrack.Beatlines;
            BeatlineIndex = 0;

            ZeroFadePosition = DEFAULT_ZERO_FADE_POS * Player.Profile.HighwayLength;
            FadeSize = Player.CameraPreset.FadeLength;

            if (player.Profile.HighwayLength > 1)
            {
                FadeSize *= player.Profile.HighwayLength;
            }

            IsInitialized = true;
        }

        protected virtual void FinishDestruction()
        {
        }

        public virtual void UpdateWithTimes(double inputTime)
        {
            if (GameManager.Paused)
            {
                return;
            }

            UpdateInputs(inputTime);
            UpdateVisualsWithTimes(inputTime);
        }

        protected virtual void UpdateVisualsWithTimes(double inputTime)
        {
            UpdateVisuals(inputTime);
            UpdateNotes(inputTime);
            UpdateBeatlines(inputTime);
        }

        public abstract void SetPracticeSection(uint start, uint end);

        public abstract void ResetPracticeSection();

        public abstract void SetReplayTime(double time);

        protected abstract void UpdateInputs(double inputTime);

        protected abstract void UpdateNotes(double songTime);

        protected abstract void UpdateVisuals(double songTime);

        protected virtual void ResetVisuals()
        {
            ComboMeter.SetFullCombo(IsFc);
            TrackView.ForceReset();

            NotePool.ReturnAllObjects();
            BeatlinePool.ReturnAllObjects();
        }

        private void UpdateBeatlines(double songTime)
        {
            while (BeatlineIndex < Beatlines.Count && Beatlines[BeatlineIndex].Time <= songTime + SpawnTimeOffset)
            {
                var beatline = Beatlines[BeatlineIndex];

                // Skip this frame if the pool is full
                if (!BeatlinePool.CanSpawnAmount(1))
                {
                    break;
                }

                var poolable = BeatlinePool.TakeWithoutEnabling();
                if (poolable == null)
                {
                    Debug.LogWarning("Attempted to spawn beatline, but it's at its cap!");
                    break;
                }

                ((BeatlineElement) poolable).BeatlineRef = beatline;
                poolable.EnableFromPool();

                BeatlineIndex++;
            }
        }

        protected void Start()
        {
            if (!GameManager.IsReplay)
            {
                SubscribeToInputEvents();
            }
        }

        protected override void GameplayDestroy()
        {
            if (!GameManager.IsReplay)
            {
                UnsubscribeFromInputEvents();
            }

            FinishDestruction();
        }

        protected void AddReplayInput(GameInput input)
        {
            _replayInputs.Add(input);
        }

        protected abstract void SubscribeToInputEvents();
        protected abstract void UnsubscribeFromInputEvents();
    }

    public abstract class BasePlayer<TEngine, TNote> : BasePlayer
        where TEngine : BaseEngine
        where TNote : Note<TNote>
    {
        private int _replayInputIndex;

        public TEngine Engine { get; private set; }

        protected List<TNote> Notes { get; private set; }

        protected int NoteIndex { get; private set; }

        protected InstrumentDifficulty<TNote> NoteTrack { get; private set; }

        private InstrumentDifficulty<TNote> OriginalNoteTrack { get; set; }

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart, trackView);

            OriginalNoteTrack = GetNotes(chart);
            player.Profile.ApplyModifiers(OriginalNoteTrack);

            NoteTrack = OriginalNoteTrack;
            Notes = NoteTrack.Notes;

            Engine = CreateEngine();

            ResetNoteCounters();

            FinishInitialization();
        }

        protected abstract InstrumentDifficulty<TNote> GetNotes(SongChart chart);
        protected abstract TEngine CreateEngine();

        protected void ResetNoteCounters()
        {
            NoteIndex = 0;
            NotesHit = 0;
            TotalNotes = Notes.Sum(i => Engine.GetNumberOfNotes(i));
        }

        protected virtual void FinishInitialization()
        {
            GameManager.BeatEventManager.Subscribe(StarpowerBar.PulseBarIfAble,
                new BeatEventManager.Info(1f / 4f, 0f));

            TrackMaterial.Initialize(ZeroFadePosition, FadeSize);
            CameraPositioner.Initialize(Player.CameraPreset);
        }

        protected override void FinishDestruction()
        {
            base.FinishDestruction();

            GameManager.BeatEventManager.Unsubscribe(StarpowerBar.PulseBarIfAble);
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            var practiceNotes = OriginalNoteTrack.Notes.Where(n => n.Tick >= start && n.Tick < end).ToList();

            Debug.Log($"Practice notes: {practiceNotes.Count}");

            var instrument = OriginalNoteTrack.Instrument;
            var difficulty = OriginalNoteTrack.Difficulty;
            var phrases = OriginalNoteTrack.Phrases;
            var textEvents = OriginalNoteTrack.TextEvents;

            NoteTrack = new InstrumentDifficulty<TNote>(instrument, difficulty, practiceNotes, phrases, textEvents);
            Notes = NoteTrack.Notes;

            ResetNoteCounters();

            Beatlines = SyncTrack.Beatlines.Where(b => b.Tick >= start && b.Tick <= end).ToList();
            BeatlineIndex = 0;

            Engine = CreateEngine();
            ResetPracticeSection();
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

            BeatlineIndex = 0;
            ResetNoteCounters();

            ResetVisuals();
        }

        public override void SetReplayTime(double time)
        {
            ResetVisuals();

            IsFc = true;

            BeatlineIndex = 0;
            ResetNoteCounters();

            _replayInputIndex = Engine.ProcessUpToTime(time, ReplayInputs);
            UpdateVisualsWithTimes(time);
        }

        protected override void UpdateInputs(double inputTime)
        {
            if (Player.Profile.IsBot)
            {
                Engine.UpdateBot(inputTime);
                return;
            }

            if (GameManager.IsReplay)
            {
                while (_replayInputIndex < ReplayInputs.Count && inputTime >= ReplayInputs[_replayInputIndex].Time)
                {
                    Engine.QueueInput(ReplayInputs[_replayInputIndex++]);
                }
            }

            if (Engine.IsInputQueued)
            {
                Engine.UpdateEngine();
            }
            else
            {
                Engine.UpdateEngine(inputTime);
            }
        }

        protected void UpdateBaseVisuals(BaseStats stats, double songTime)
        {
            int maxMultiplier = stats.IsStarPowerActive ? 8 : 4;
            bool groove = stats.ScoreMultiplier == maxMultiplier;

            TrackMaterial.SetTrackScroll(songTime, NoteSpeed);
            TrackMaterial.GrooveMode = groove;
            TrackMaterial.StarpowerMode = stats.IsStarPowerActive;

            ComboMeter.SetCombo(stats.ScoreMultiplier, maxMultiplier, stats.Combo);
            StarpowerBar.SetStarpower(stats.StarPowerAmount, stats.IsStarPowerActive);
            SunburstEffects.SetSunburstEffects(groove, stats.IsStarPowerActive);

            TrackView.UpdateNoteStreak(stats.Combo);
        }

        protected override void UpdateNotes(double songTime)
        {
            while (NoteIndex < Notes.Count && Notes[NoteIndex].Time <= songTime + SpawnTimeOffset)
            {
                var note = Notes[NoteIndex];

                // Skip this frame if the pool is full
                if (!NotePool.CanSpawnAmount(note.ChildNotes.Count + 1))
                {
                    break;
                }

                NoteIndex++;

                // Don't spawn hit or missed notes
                if (note.WasHit || note.WasMissed)
                {
                    continue;
                }

                // Spawn all of the notes and child notes
                foreach (var child in note.ChordEnumerator())
                {
                    SpawnNote(child);
                }
            }
        }

        protected void SpawnNote(TNote note)
        {
            var poolable = NotePool.KeyedTakeWithoutEnabling(note);
            if (poolable == null)
            {
                Debug.LogWarning("Attempted to spawn note, but it's at its cap!");
                return;
            }

            InitializeSpawnedNote(poolable, note);
            poolable.EnableFromPool();
        }

        protected abstract void InitializeSpawnedNote(IPoolable poolable, TNote note);

        protected virtual void OnNoteHit(int index, TNote note)
        {
            NotesHit++;
        }

        protected virtual void OnNoteMissed(int index, TNote note)
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected virtual void OnOverstrum()
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected virtual void OnSoloStart(SoloSection solo)
        {
            TrackView.StartSolo(solo);
        }

        protected virtual void OnSoloEnd(SoloSection solo)
        {
            TrackView.EndSolo(solo.SoloBonus);
        }

        protected override void SubscribeToInputEvents()
        {
            Player.Bindings.SubscribeToGameplayInputs(Player.Profile.GameMode, OnGameInput);
        }

        protected override void UnsubscribeFromInputEvents()
        {
            Player.Bindings.UnsubscribeFromGameplayInputs(Player.Profile.GameMode, OnGameInput);
        }

        protected void OnGameInput(ref GameInput input)
        {
            // Ignore while paused
            if (GameManager.Paused) return;

            double adjustedTime = GameManager.GetRelativeInputTime(input.Time);
            input = new(adjustedTime, input.Action, input.Integer);

            // Allow the input to be explicitly ignored before processing it
            if (InterceptInput(ref input)) return;
            OnInputProcessed(ref input);
        }

        protected abstract bool InterceptInput(ref GameInput input);

        protected virtual void OnInputProcessed(ref GameInput input)
        {
            Engine.QueueInput(input);
            AddReplayInput(input);
        }
    }
}