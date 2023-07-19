using YARG.Core.Chart;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public abstract class NoteElement<TNote, TPlayer> : TrackElement<TPlayer>
        where TNote : Note<TNote>
        where TPlayer : BasePlayer
    {
        public TNote NoteRef { get; set; }

        protected NoteGroup NoteGroup;

        protected override double ElementTime => NoteRef.Time;
    }
}