using YARG.Career;
using YARG.Core.Song;

namespace YARG.Menu.Career
{
    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;
        public bool HasSongEntry => _songEntry != null;

        private readonly CareerSong _song;
        private readonly SongEntry  _songEntry;

        // TODO: Need a way to tell if the tier is still locked or the song is not the first in a playlist tier

        public SongViewType(CareerSong song)
        {
            _song = song;
            _songEntry = song.SongEntry;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_songEntry?.Name ?? "Not Found In Library", TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_songEntry?.Artist ?? "", TextType.Secondary, selected);
        }
    }
}