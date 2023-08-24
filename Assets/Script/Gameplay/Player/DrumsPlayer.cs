using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class DrumsPlayer : BasePlayer<DrumsEngine, DrumNote>
    {
        private DrumsEngineParameters _engineParams;

        [Header("Drums Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override float[] StarMultiplierThresholds { get; }  =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.29f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        protected override void UpdateVisuals(double songTime)
        {
            throw new System.NotImplementedException();
        }

        protected override InstrumentDifficulty<DrumNote> GetNotes(SongChart chart)
        {
            var track = chart.GetDrumsTrack(Player.Profile.Instrument).Clone();
            return track.Difficulties[Player.Profile.Difficulty];
        }

        protected override DrumsEngine CreateEngine()
        {
            _engineParams = new DrumsEngineParameters(0.15, 1);
            // var engine = new YargFiveFretEngine(NoteTrack, SyncTrack, _engineParams);

            // engine.OnNoteHit += OnNoteHit;
            // engine.OnNoteMissed += OnNoteMissed;
            // engine.OnOverstrum += OnOverstrum;
            //
            // engine.OnSoloStart += OnSoloStart;
            // engine.OnSoloEnd += OnSoloEnd;

            return null;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            // StarScoreThresholds = new int[StarMultiplierThresholds.Length];
            // for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            // {
            //     StarScoreThresholds[i] = Mathf.FloorToInt(Engine.BaseScore * StarMultiplierThresholds[i]);
            // }

            _fretArray.Initialize(Player.ColorProfile.FourLaneDrums, Player.Profile.LeftyFlip);
            HitWindowDisplay.SetHitWindowInfo(_engineParams, NoteSpeed);
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, DrumNote note)
        {
            ((DrumsNoteElement) poolable).NoteRef = note;
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
        }
    }
}