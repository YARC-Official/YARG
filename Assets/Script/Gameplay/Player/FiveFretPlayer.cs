using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Input;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : BasePlayer<GuitarEngine, GuitarNote>
    {
        private readonly GuitarEngineParameters _engineParams = new(0.16, 1, 0.08, 0.07, 0.035, false, true);

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        public override void Initialize(YargPlayer player, InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            List<Beatline> beats)
        {
            base.Initialize(player, chart, syncTrack, beats);

            Engine = new YargFiveFretEngine(Chart, SyncTrack, _engineParams);

            Debug.Log("Note count: " + Chart.Notes.Count);

            Engine.OnNoteHit += OnNoteHit;
            Engine.OnNoteMissed += OnNoteMissed;
            Engine.OnOverstrum += OnOverstrum;

            // These events are examples of how they can be used
            // They should be replaced in the future with proper events to be used by the frontend
            Engine.OnSoloStart += (solo) =>
            {
                Debug.Log($"Solo started (total notes: {solo.NoteCount}");
            };

            Engine.OnSoloEnd += (solo) =>
            {
                Debug.Log($"Solo ended (hit notes: {solo.NotesHit}/{solo.NoteCount}");
            };

            Engine.OnSustainEnd += (chordParent, timeEnded) =>
            {
                foreach (var note in chordParent.ChordEnumerator())
                {
                    (NotePool.GetByKey(note) as FiveFretNoteElement)?.SustainEnd();
                }
            };

            StarScoreThresholds = new int[StarMultiplierThresholds.Length];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarScoreThresholds[i] = Mathf.FloorToInt(Engine.BaseScore * StarMultiplierThresholds[i]);
            }

            _fretArray.Initialize(player.ColorProfile);
            HitWindowDisplay.SetHitWindowInfo(_engineParams, player.Profile.NoteSpeed);
        }

        protected override void Update()
        {
            base.Update();

            Score = Engine.EngineStats.Score;
            Combo = Engine.EngineStats.Combo;
        }

        protected override void UpdateInputs()
        {
            if (Player.Profile.IsBot)
            {
                Engine.UpdateBot(InputManager.InputUpdateTime);
                return;
            }

            base.UpdateInputs();
        }

        protected override void UpdateVisuals()
        {
            UpdateBaseVisuals(Engine.EngineStats);

            for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
            {
                _fretArray.SetPressed((int) fret, Engine.IsFretHeld(fret));
            }
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            ((FiveFretNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            foreach (var note in chordParent.ChordEnumerator())
            {
                // TODO: It is possible that this should be moved to BasePlayer
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.HitNote();

                if (note.Fret != 0)
                {
                    _fretArray.PlayHitAnimation(note.Fret - 1);
                }
            }
        }

        protected override void OnNoteMissed(int index, GuitarNote chordParent)
        {
            foreach (var note in chordParent.ChordEnumerator())
            {
                // TODO: It is possible that this should be moved to BasePlayer
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.MissNote();
            }

            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected override void OnOverstrum()
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected override void SubscribeToInputEvents()
        {
            InputManager.OnGameInput += OnGameInput;
        }

        protected override void UnsubscribeFromInputEvents()
        {
            InputManager.OnGameInput -= OnGameInput;
        }

        private void OnGameInput(YargPlayer player, GameInput input)
        {
            if (player != Player || GameManager.IsReplay)
            {
                return;
            }

            Engine.QueueInput(input);
            AddReplayInput(input);
        }
    }
}