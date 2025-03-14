using TMPro;
using UnityEngine;
using YARG.Core.Song;
using YARG.Localization;

namespace YARG.Menu.Credits
{
    public class SongCreditEntry : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _songName;
        [SerializeField]
        private TextMeshProUGUI _writtenBy;
        [SerializeField]
        private TextMeshProUGUI _performedBy;
        [SerializeField]
        private TextMeshProUGUI _courtesyOf;
        [SerializeField]
        private TextMeshProUGUI _albumCover;
        [SerializeField]
        private TextMeshProUGUI _license;

        public void Initialize(SongEntry song)
        {
            _songName.text = Localize.KeyFormat("Menu.Credits.Song.Name", song.Name, song.Artist);

            if (!string.IsNullOrEmpty(song.CreditWrittenBy) && song.CreditWrittenBy == song.CreditPerformedBy)
            {
                // Combine the "written by" and the "performed by" if they are the same
                _writtenBy.gameObject.SetActive(true);
                _writtenBy.text = Localize.KeyFormat(
                    "Menu.Credits.Song.WrittenAndPerformedBy", song.CreditWrittenBy);

                _performedBy.gameObject.SetActive(false);
            }
            else
            {
                ShowOrHideCredit(_writtenBy, "Menu.Credits.Song.WrittenBy", song.CreditWrittenBy);
                ShowOrHideCredit(_performedBy, "Menu.Credits.Song.PerformedBy", song.CreditPerformedBy);
            }

            ShowOrHideCredit(_courtesyOf, "Menu.Credits.Song.CourtesyOf", song.CreditCourtesyOf);
            ShowOrHideCredit(_albumCover, "Menu.Credits.Song.AlbumCover", song.CreditAlbumArtDesignedBy);
            ShowOrHideCredit(_license, "Menu.Credits.Song.License", song.CreditLicense);
        }

        private static void ShowOrHideCredit(TextMeshProUGUI text, string unlocalized, string metadata)
        {
            if (!string.IsNullOrEmpty(metadata))
            {
                text.gameObject.SetActive(true);
                text.text = Localize.KeyFormat(unlocalized, metadata);
            }
            else
            {
                text.gameObject.SetActive(false);
            }
        }
    }
}