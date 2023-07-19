using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Input;
using YARG.Player;
using YARG.Settings.ColorProfiles;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : BasePlayer<GuitarEngine, GuitarNote>
    {
        private readonly GuitarEngineParameters _engineParams = new(0.14, 1, 0.08, 0.065, false, true);

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override void Initialize(YargPlayer player, InstrumentDifficulty<GuitarNote> chart, List<Beatline> beats)
        {
            base.Initialize(player, chart, beats);

            Engine = new YargFiveFretEngine(Chart.Notes, _engineParams);

            Debug.Log("Note count: " + Chart.Notes.Count);

            Engine.OnNoteHit += OnNoteHit;
            Engine.OnNoteMissed += OnNoteMissed;
            Engine.OnOverstrum += OnOverstrum;

            Engine.OnStarPowerPhraseHit += note =>
            {
                Debug.Log("Hit star power phrase at " + note.Time);
            };

            Engine.OnStarPowerPhraseMissed += note =>
            {
                Debug.Log("Missed star power phrase at " + note.Time);
            };

            _fretArray.Initialize(ColorProfile.Default);
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
                Engine.UpdateBot(InputManager.BeforeUpdateTime);
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
            ((FiveFretNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, GuitarNote note)
        {
            OnNoteHitSpecific(note);
            foreach (var child in note.ChildNotes)
            {
                OnNoteHitSpecific(child);
            }
        }

        // TODO: Not sure of the best way to do this, but this is the simplest to me
        private void OnNoteHitSpecific(GuitarNote note)
        {
            if (note.Fret == 0) return;

            _fretArray.PlayHitAnimation(note.Fret - 1);
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