using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.ProKeys.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class ProKeysPlayer : TrackPlayer<ProKeysEngine, ProKeysNote>
    {
        public override float[] StarMultiplierThresholds { get; protected set; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        public ProKeysEngineParameters EngineParams { get; private set; }

        public override bool ShouldUpdateInputsOnResume => true;

        protected override InstrumentDifficulty<ProKeysNote> GetNotes(SongChart chart)
        {
            var track = chart.ProKeys.Clone();
            return track.Difficulties[Player.Profile.CurrentDifficulty];
        }

        protected override ProKeysEngine CreateEngine()
        {
            if (!GameManager.IsReplay)
            {
                // Create the engine params from the engine preset
                // EngineParams = Player.EnginePreset.FiveFretGuitar.Create(StarMultiplierThresholds, isBass);
                EngineParams = new ProKeysEngineParameters();
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (ProKeysEngineParameters) Player.EngineParameterOverride;
            }

            var engine = new YargProKeysEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);

            HitWindow = EngineParams.HitWindow;

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            return engine;
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);
        }

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(SongStem.Keys, muted);
                IsStemMuted = muted;
            }
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, ProKeysNote note)
        {
            ((ProKeysNoteElement) poolable).NoteRef = note;
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            // Ignore SP in practice mode
            if (input.GetAction<ProKeysAction>() == ProKeysAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }
    }
}