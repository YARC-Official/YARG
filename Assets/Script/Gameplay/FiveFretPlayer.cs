using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Input;
using YARG.Player;

namespace YARG.Gameplay
{
    public class FiveFretPlayer : BasePlayer<GuitarEngine, GuitarNote>
    {

        private readonly GuitarEngineParameters _engineParams = new(0.14, 1, 0.08,
            0.065, true);

        public override void Initialize(YargPlayer player, List<GuitarNote> notes)
        {
            base.Initialize(player, notes);

            Engine = new YargFiveFretEngine(Notes, _engineParams);
        }

        protected override void Update()
        {
            throw new System.NotImplementedException();
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