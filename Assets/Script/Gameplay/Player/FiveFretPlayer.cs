using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Input;
using YARG.Player;

namespace YARG.Gameplay
{
    public sealed class FiveFretPlayer : BasePlayer<GuitarEngine, GuitarNote>
    {
        private readonly GuitarEngineParameters _engineParams = new(0.14, 1, 0.08,
            0.065, false, true);

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        [Space]
        public int Score;
        public int NoteStreak;

        public override void Initialize(YargPlayer player, InstrumentDifficulty<GuitarNote> chart)
        {
            base.Initialize(player, chart);

            Engine = new YargFiveFretEngine(Chart.Notes, _engineParams);

            Debug.Log("Note count: " + Chart.Notes.Count);

            Engine.OnNoteHit += OnNoteHit;
            Engine.OnNoteMissed += OnNoteMissed;
            Engine.OnOverstrum += OnOverstrum;

            // TODO: Move colors to profile
            _fretArray.Initialize(new[] {
                Color.green,
                Color.red,
                Color.yellow,
                Color.blue,
                new(1f, 0.5f, 0f),
            });
        }

        protected override void Update()
        {
            base.Update();

            Score = Engine.EngineStats.Score;
            NoteStreak = Engine.EngineStats.Combo;
        }

        protected override void UpdateInputs()
        {
            if (Player.Profile.IsBot)
            {
                Engine.UpdateBot(GameManager.RealSongTime);
                return;
            }

            base.UpdateInputs();
        }

        protected override void UpdateVisuals()
        {
            UpdateBaseVisuals(Engine.EngineStats);

            for(var fret = GuitarAction.Green; fret <= GuitarAction.Orange; fret++)
            {
                _fretArray.SetPressed((int)fret, Engine.IsFretHeld(fret));
            }
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            ((FiveFretVisualNote) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, GuitarNote note)
        {

        }

        protected override void OnNoteMissed(int index, GuitarNote note)
        {
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