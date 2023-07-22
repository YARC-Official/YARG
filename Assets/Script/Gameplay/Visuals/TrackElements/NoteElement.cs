using YARG.Core.Chart;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public enum NoteElemState
    {
        Waiting,
        Hit,
        Missed
    }

    public abstract class NoteElement<TNote, TPlayer> : TrackElement<TPlayer>
        where TNote : Note<TNote>
        where TPlayer : BasePlayer
    {
        public TNote NoteRef { get; set; }

        private NoteElemState _state;
        protected NoteElemState State
        {
            get => _state;
            private set
            {
                var old = _state;
                _state = value;

                HitStateChanged(old, value);
            }
        }

        protected NoteGroup NoteGroup;

        protected override double ElementTime => NoteRef.Time;

        protected abstract void HitStateChanged(NoteElemState from, NoteElemState to);

        protected override void InitializeElement()
        {
            State = NoteElemState.Waiting;
        }

        protected override void UpdateElement()
        {
            // A note can be missed at any time (no input, during sustain, etc.)
            if (NoteRef.WasMissed && State != NoteElemState.Missed)
            {
                State = NoteElemState.Missed;
                return;
            }

            // A note can only be hit when it's waiting.
            // It can't be hit after it was missed,
            // and it can't be hit after it was already hit
            if (NoteRef.WasHit && State == NoteElemState.Waiting)
            {
                State = NoteElemState.Hit;
            }
        }
    }
}