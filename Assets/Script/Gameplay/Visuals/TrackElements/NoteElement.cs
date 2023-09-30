using YARG.Core.Chart;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public enum SustainState
    {
        Waiting,
        Hitting,
        Missed
    }

    public abstract class NoteElement<TNote, TPlayer> : TrackElement<TPlayer>
        where TNote : Note<TNote>
        where TPlayer : TrackPlayer
    {
        public TNote NoteRef { get; set; }

        protected SustainState SustainState { get; private set; }

        protected NoteGroup NoteGroup;

        protected override double ElementTime => NoteRef.Time;

        protected override void InitializeElement()
        {
            SustainState = SustainState.Waiting;
        }

        public virtual void HitNote()
        {
            SustainState = SustainState.Hitting;
        }

        public virtual void MissNote()
        {
            SustainState = SustainState.Missed;
        }

        public virtual void SustainEnd()
        {
            SustainState = SustainState.Missed;
        }
    }
}