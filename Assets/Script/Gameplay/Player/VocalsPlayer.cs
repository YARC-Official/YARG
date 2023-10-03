using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public class VocalsPlayer : BasePlayer
    {
        public VocalsEngineParameters EngineParams { get; private set; }
        public VocalsEngine Engine { get; private set; }

        public override BaseStats Stats { get; }

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.18f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        protected InstrumentDifficulty<VocalNote> NoteTrack { get; private set; }

        public new void Initialize(int index, YargPlayer player, SongChart chart)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart);

            // TODO: Selectable harmony part
            // Get the notes from the specific harmony or solo part
            var multiTrack = chart.GetVocalsTrack(Player.Profile.CurrentInstrument);
            var track = multiTrack.Parts[0];
            NoteTrack = track.CloneAsInstrumentDifficulty();

            Engine = CreateEngine();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);
        }

        protected VocalsEngine CreateEngine()
        {
            EngineParams = new VocalsEngineParameters(1.0, 0.9, StarMultiplierThresholds);

            var engine = new YargVocalsEngine(NoteTrack, SyncTrack, EngineParams);

            return engine;
        }

        protected override void ResetVisuals()
        {
        }

        public override void ResetPracticeSection()
        {
        }

        protected override void UpdateInputs(double time)
        {
        }

        protected override void UpdateVisuals(double time)
        {
        }

        public override void SetPracticeSection(uint start, uint end)
        {
        }

        public override void SetReplayTime(double time)
        {
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
        }

        protected override void OnInputProcessed(ref GameInput input)
        {
        }
    }
}