using YARG.Core.Song;
using YARG.Scores;

namespace YARG.Menu.History
{
    public class GameRecordViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        private readonly GameRecord _gameRecord;
        private readonly SongMetadata _songMetadata;

        public GameRecordViewType(GameRecord gameRecord)
        {
            _gameRecord = gameRecord;

            // TODO
            // var songsByHash = GlobalVariables.Instance.SongContainer.SongsByHash;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_gameRecord.SongName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_gameRecord.SongArtist, TextType.Secondary, selected);
        }
    }
}