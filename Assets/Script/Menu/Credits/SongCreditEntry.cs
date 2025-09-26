using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Cysharp.Text;
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
        [SerializeField]
        private TextMeshProUGUI _composedBy;
        [SerializeField]
        private TextMeshProUGUI _arrangedBy;
        [SerializeField]
        private TextMeshProUGUI _producedBy;
        [SerializeField]
        private TextMeshProUGUI _engineeredBy;
        [SerializeField]
        private TextMeshProUGUI _mixedBy;
        [SerializeField]
        private TextMeshProUGUI _masteredBy;
        [SerializeField]
        private TextMeshProUGUI _publishedBy;

        private List<SongCredit> _songCredits = new();

        public void Initialize(SongEntry song)
        {
            _songName.text = Localize.KeyFormat("Menu.Credits.Song.Name", song.Name, song.Artist);

            // Written, Composed, Arranged, Performed, Produced, Engineered, Mixed, and Mastered need to be combined
            // if any or all are identical, so they are dealt with differently than ones that cannot be combined
            AddSongCredit(_writtenBy, "Menu.Credits.Song.Written", song.CreditWrittenBy);
            AddSongCredit(_performedBy, "Menu.Credits.Song.Performed", song.CreditPerformedBy);
            AddSongCredit(_composedBy, "Menu.Credits.Song.Composed", song.CreditComposedBy);
            AddSongCredit(_arrangedBy, "Menu.Credits.Song.Arranged", song.CreditArrangedBy);
            AddSongCredit(_producedBy, "Menu.Credits.Song.Produced", song.CreditProducedBy);
            AddSongCredit(_engineeredBy, "Menu.Credits.Song.Engineered", song.CreditEngineeredBy);
            AddSongCredit(_mixedBy, "Menu.Credits.Song.Mixed", song.CreditMixedBy);
            AddSongCredit(_masteredBy,  "Menu.Credits.Song.Mastered", song.CreditMasteredBy);

            HandleCombinedCredits();

            ShowOrHideCredit(_courtesyOf, "Menu.Credits.Song.CourtesyOf", song.CreditCourtesyOf);
            ShowOrHideCredit(_albumCover, "Menu.Credits.Song.AlbumCover", song.CreditAlbumArtDesignedBy);
            ShowOrHideCredit(_publishedBy, "Menu.Credits.Song.PublishedBy", song.CreditPublishedBy);
            ShowOrHideCredit(_chartedBy, "Menu.Credits.Song.ChartedBy", Localize.List(GetCharterCredits(song)));
            ShowOrHideCredit(_license, "Menu.Credits.Song.License", song.CreditLicense);
        }

        private static void ShowOrHideCredit(TextMeshProUGUI text, string localized)
        {
            if (!string.IsNullOrEmpty(localized))
            {
                text.gameObject.SetActive(true);
                text.text = localized;
            }
            else
            {
                text.gameObject.SetActive(false);
            }
        }

        private static void ShowOrHideCredit(TextMeshProUGUI text, string unlocalized, string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
            {
                ShowOrHideCredit(text, string.Empty);
            }
            else
            {
                ShowOrHideCredit(text, Localize.KeyFormat(unlocalized, metadata));
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
            Add(song.CharterVenue);

            // For future use
            // Add(song.CharterProGuitar);
            // Add(song.CharterProBass);
            // Add(song.CharterEliteDrums);

            // Sort credit entries alphabetically
            credits.Sort();

            return credits;
        }

        private void HandleCombinedCredits()
        {
            // Match up _songCredit entries that should be combined together and combine them into the first entry.

            if (_songCredits.Count == 0)
            {
                return;
            }

            // I give up, I'm using LINQ for this
            var groups = _songCredits.Select((credit, index) => (credit, index))
                .GroupBy(x => x.credit.Metadata).ToList();

            foreach (var group in groups)
            {
                var items = group.ToList();

                // How?
                if (items.Count == 0)
                {
                    continue;
                }

                var sb = ZString.CreateStringBuilder();

                var roles = items.Select(x => Localize.Key(x.credit.LocalizationKey)).ToList();

                var first = items[0].credit;

                sb.Append(Localize.List(roles));
                sb.AppendFormat(" {0}", Localize.KeyFormat("Menu.Credits.Song.By", first.Metadata));

                ShowOrHideCredit(first.Text, sb.ToString());

                // Hide the rest
                for (int i = 1; i < items.Count; i++)
                {
                    ShowOrHideCredit(items[i].credit.Text, string.Empty);
                }
            }
        }

        private void AddSongCredit(TextMeshProUGUI text, string unlocalized, string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
            {
                ShowOrHideCredit(text, unlocalized, metadata);
                return;
            }

            _songCredits.Add(new SongCredit(text, unlocalized, metadata));
        }

        private readonly struct SongCredit
        {
            public readonly TextMeshProUGUI Text;
            public readonly string          LocalizationKey;
            public readonly string          Metadata;

            public SongCredit(TextMeshProUGUI text, string unlocalized, string metadata)
            {
                Text = text;
                LocalizationKey = unlocalized;
                Metadata = metadata;
            }
        }
    }
}