using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;
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

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.18f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        protected InstrumentDifficulty<VocalNote> NoteTrack { get; private set; }

        private MicInputContext _inputContext;

        public new void Initialize(int index, YargPlayer player, SongChart chart)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart);

            // TODO: Selectable harmony part
            // Get the notes from the specific harmony or solo part
            var multiTrack = chart.GetVocalsTrack(Player.Profile.CurrentInstrument);
            var track = multiTrack.Parts[0];
            NoteTrack = track.CloneAsInstrumentDifficulty();

            // Create and start an input context for the mic
            _inputContext = new MicInputContext(player.Bindings.Microphone, GameManager);
            _inputContext.Start();

            Engine = CreateEngine();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);
        }

        protected override void FinishDestruction()
        {
            _inputContext.Stop();
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
            _inputContext.PushInputsToEngine(Engine);

            base.UpdateInputs(time);
        }

        protected override void UpdateVisuals(double time)
        {
            if (_inputContext.Device.LastOutputFrame == null) return;
            var micFrame = _inputContext.Device.LastOutputFrame.Value;

            if (!micFrame.VoiceDetected)
            {
                // Hide the needle if there's no singing
                _needleVisualContainer.SetActive(false);
            }
            else
            {
                _needleVisualContainer.SetActive(true);

                // Set the position of the needle
                var z = GameManager.VocalTrack.GetPosForPitch(micFrame.PitchAsMidiNote);
                var lerp = Mathf.Lerp(transform.localPosition.z, z, Time.deltaTime * 12f);
                transform.localPosition = new Vector3(0f, 0f, lerp);
            }
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