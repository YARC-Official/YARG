using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
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
        [SerializeField]
        private TextMeshProUGUI _chartedBy;

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
            ShowOrHideCredit(_chartedBy, "Menu.Credits.Song.ChartedBy", Localize.List(GetCharterCredits(song)));
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

        private static List<string> GetCharterCredits(SongEntry song)
        {
            var credits = new List<string>(10);

            void Add(string item)
            {
                if (string.IsNullOrEmpty(item) || credits.Contains(item))
                {
                    return;
                }

                // Is this a joined list of credits?
                if (item.Contains(","))
                {
                    // Split the string at the commas, trim any leading or trailing spaces, add results to list
                    foreach (var substring in item.Split(','))
                    {
                        var trimmed = substring.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !credits.Contains(trimmed))
                        {
                            credits.Add(trimmed);
                        }
                    }

                    return;
                }

                credits.Add(item);
            }

            Add(song.CharterGuitar);
            Add(song.CharterBass);
            Add(song.CharterDrums);
            Add(song.CharterVocals);
            Add(song.CharterKeys);
            Add(song.CharterProKeys);

            // For future use
            // Add(song.CharterProGuitar);
            // Add(song.CharterProBass);
            // Add(song.CharterEliteDrums);

            // Sort credit entries alphabetically
            credits.Sort();

            return credits;
        }
    }
}