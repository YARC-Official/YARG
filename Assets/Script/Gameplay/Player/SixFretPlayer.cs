using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using YARG.Player;
using YARG.Gameplay.HUD;
using YARG.Core.Replays;

namespace YARG.Gameplay.Player
{
    public sealed class SixFretPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        public override bool ShouldUpdateInputsOnResume => true;

        private static float[] GuitarStarMultiplierThresholds => new[] { 0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f };

        public GuitarEngineParameters EngineParams { get; private set; }

        [SerializeField]
        private FretArray _fretArray;

        public override float[] StarMultiplierThresholds { get; protected set; } = GuitarStarMultiplierThresholds;
        public override int[] StarScoreThresholds { get; protected set; }

        private SongStem _stem;
        private int _sustainCount;
        public float WhammyFactor { get; private set; }

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer, int? currentHighScore)
        {
            _stem = player.Profile.CurrentInstrument.ToSongStem();
            if (_stem == SongStem.Bass && mixer[SongStem.Bass] == null)
            {
                _stem = SongStem.Rhythm;
            }
            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);
            _currentNoteIndex = 0;
        }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetSixFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.GetDifficulty(Player.Profile.CurrentDifficulty);
        }

        protected override GuitarEngine CreateEngine()
        {
            bool isBass = Player.Profile.CurrentInstrument == Instrument.SixFretBass;
            if (!Player.IsReplay)
            {
                EngineParams = Player.EnginePreset.FiveFretGuitar.Create(StarMultiplierThresholds, isBass);
            }
            else
            {
                EngineParams = (GuitarEngineParameters) Player.EngineParameterOverride;
            }

            var engine = new YargFiveFretEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);
            HitWindow = EngineParams.HitWindow;

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverhit;
            engine.OnSustainStart += OnSustainStart;
            engine.OnSustainEnd += OnSustainEnd;
            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;
            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;
            engine.OnCountdownChange += OnCountdownChange;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            IndicatorStripes.Initialize(Player.EnginePreset.FiveFretGuitar);
            _fretArray.FretCount = 3; // Display only 3 frets
            _fretArray.Initialize(
                Player.ThemePreset,
                Player.Profile.GameMode,
                Player.ColorProfile.FiveFretGuitar, // Temporarily use FiveFretGuitar until SixFretGuitar is recognized
                Player.Profile.LeftyFlip);

            GameManager.BeatEventHandler.Subscribe(_fretArray.PulseFretColors);
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            ((SixFretNoteElement) poolable).NoteRef = note;
        }

        public override void SetReplayTime(double time)
        {
            base.SetReplayTime(time);
            _currentNoteIndex = 0;
        }


        private int _currentNoteIndex = 0;

        protected override void UpdateNotes(double songTime)
        {
            // Use a local variable to track note index
            while (_currentNoteIndex < Notes.Count && Notes[_currentNoteIndex].Time <= songTime + SpawnTimeOffset)
            {
                var note = Notes[_currentNoteIndex];

                // Skip this frame if the pool is full
                if (!NotePool.CanSpawnAmount(note.ChildNotes.Count + 1))
                {
                    break;
                }

                _currentNoteIndex++;

                // Call base to let engine track the note
                base.OnNoteSpawned(note);

                // Don't spawn hit or missed notes
                if (note.WasHit || note.WasMissed)
                {
                    continue;
                }

                // For six-fret guitar, only spawn the primary note for each display group
                var notesToSpawn = GetPrimaryNotesForDisplay(note);
                foreach (var child in notesToSpawn)
                {
                    SpawnNote(child);
                }
            }
        }

        private List<GuitarNote> GetPrimaryNotesForDisplay(GuitarNote chord)
        {
            var primaryNotes = new List<GuitarNote>();
            var allNotes = new List<GuitarNote>();
            
            // Convert AllNotes enumerator to a list
            foreach (var note in chord.AllNotes)
            {
                allNotes.Add(note);
            }

            // Group notes by lane (1+4, 2+5, 3+6 are same lanes)
            var laneGroups = new Dictionary<int, List<GuitarNote>>();
            foreach (var n in allNotes)
            {
                int lane = GetLaneForFret(n.Fret);
                if (!laneGroups.ContainsKey(lane))
                    laneGroups[lane] = new List<GuitarNote>();
                laneGroups[lane].Add(n);
            }

            // For each lane, spawn the primary note (lowest fret number)
            // This prevents duplicate notes in the same lane (like 1+4)
            // For cross-lane bar chords, this will spawn notes in each lane
            foreach (var group in laneGroups)
            {
                var notesInLane = group.Value;
                var primaryNote = notesInLane.OrderBy(n => n.Fret).First();
                primaryNotes.Add(primaryNote);
            }

            return primaryNotes;
        }

        private void SpawnNote(GuitarNote note)
        {
            var poolable = NotePool.KeyedTakeWithoutEnabling(note);
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn note, but it's at its cap!");
                return;
            }

            InitializeSpawnedNote(poolable, note);
            poolable.EnableFromPool();
        }





        private int GetLaneForFret(int fret)
        {
            // Lanes: B1+W4=0, B2+W5=1, B3+W6=2
            if (fret >= 1 && fret <= 6)
            {
                return (fret - 1) % 3;
            }
            return 0;
        }

        private int GetDisplayIndexForFret(int fret)
        {
            // Same as lane for display purposes
            return GetLaneForFret(fret);
        }


        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);
            if (GameManager.Paused) return;
            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as SixFretNoteElement)?.HitNote();

                if (note.Fret != (int) SixFretGuitarFret.Open)
                {
                    // Map 6-fret positions to display positions (1-3 for both lower and upper)
                    int displayFret = GetDisplayFretForFret(note.Fret);
                    _fretArray.PlayHitAnimation(displayFret);
                }
                else
                {
                    _fretArray.PlayOpenHitAnimation();
                }
            }
        }

        /// <summary>
        /// Maps 6-fret controller positions to 3 display positions
        /// Black frets 1-3 → Display positions 0-2
        /// White frets 4-6 → Display positions 0-2
        /// </summary>
        private int GetDisplayFretForFret(int fret)
        {
            switch (fret)
            {
                case 1: return 0; // B1 → Display position 0
                case 2: return 1; // B2 → Display position 1
                case 3: return 2; // B3 → Display position 2
                case 4: return 0; // W4 → Display position 0
                case 5: return 1; // W5 → Display position 1
                case 6: return 2; // W6 → Display position 2
                default: return 0; // Default fallback
            }
        }

        protected override void OnNoteMissed(int index, GuitarNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);
            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as SixFretNoteElement)?.MissNote();
            }
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);

            // Update 3 display frets, mapping all 6 controller frets
            for (int displayFret = 0; displayFret < 3; displayFret++)
            {
                bool isPressed = false;
                
                switch (displayFret)
                {
                    case 0: // Display position 0
                        isPressed = Engine.IsFretHeld(GuitarAction.Fret1) || // B1
                                   Engine.IsFretHeld(GuitarAction.Fret4) || // W4
                                   (Engine.IsFretHeld(GuitarAction.Fret1) && Engine.IsFretHeld(GuitarAction.Fret4)); // B1+W4
                        break;
                    case 1: // Display position 1
                        isPressed = Engine.IsFretHeld(GuitarAction.Fret2) || // B2
                                   Engine.IsFretHeld(GuitarAction.Fret5) || // W5
                                   (Engine.IsFretHeld(GuitarAction.Fret2) && Engine.IsFretHeld(GuitarAction.Fret5)); // B2+W5
                        break;
                    case 2: // Display position 2
                        isPressed = Engine.IsFretHeld(GuitarAction.Fret3) || // B3
                                   Engine.IsFretHeld(GuitarAction.Fret6) || // W6
                                   (Engine.IsFretHeld(GuitarAction.Fret3) && Engine.IsFretHeld(GuitarAction.Fret6)); // B3+W6
                        break;
                }
                
                _fretArray.SetPressed(displayFret, isPressed);
            }
        }

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(_stem, muted);
                IsStemMuted = muted;
            }
        }

        public override void SetStarPowerFX(bool active)
        {
            GameManager.ChangeStemReverbState(_stem, active);
        }

        protected override void ResetVisuals()
        {
            base.ResetVisuals();
            _fretArray.ResetAll();
        }

        private void OnSustainStart(GuitarNote parent)
        {
            foreach (var note in parent.AllNotes)
            {
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                if (note.Fret != (int) SixFretGuitarFret.Open)
                {
                    // Map 6-fret positions to display positions
                    int displayFret = GetDisplayFretForFret(note.Fret);
                    _fretArray.SetSustained(displayFret, true);
                }

                _sustainCount++;
            }
        }

        private void OnSustainEnd(GuitarNote parent, double timeEnded, bool finished)
        {
            foreach (var note in parent.AllNotes)
            {
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                (NotePool.GetByKey(note) as SixFretNoteElement)?.SustainEnd(finished);

                if (note.Fret != (int) SixFretGuitarFret.Open)
                {
                    // Map 6-fret positions to display positions
                    int displayFret = GetDisplayFretForFret(note.Fret);
                    _fretArray.SetSustained(displayFret, false);
                }

                _sustainCount--;
            }

            if (!finished)
            {
                if (!parent.IsDisjoint || _sustainCount == 0)
                {
                    SetStemMuteState(true);
                }
            }

            if (_sustainCount == 0)
            {
                WhammyFactor = 0;
                GameManager.ChangeStemWhammyPitch(_stem, 0);
            }
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            if (input.GetAction<GuitarAction>() == GuitarAction.StarPower && GameManager.IsPractice) return true;
            return false;
        }

        protected override void OnInputQueued(GameInput input)
        {
            base.OnInputQueued(input);

            if (_sustainCount > 0 && input.GetAction<GuitarAction>() == GuitarAction.Whammy)
            {
                WhammyFactor = Mathf.Clamp01(input.Axis);
                GameManager.ChangeStemWhammyPitch(_stem, WhammyFactor);
            }
        }

        public override (ReplayFrame Frame, ReplayStats Stats) ConstructReplayData()
        {
            var frame = new ReplayFrame(Player.Profile, EngineParams, Engine.EngineStats, ReplayInputs.ToArray());
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name));
        }
    }
}