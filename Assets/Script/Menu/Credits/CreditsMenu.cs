using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Song;

namespace YARG.Menu.Credits
{
    public class CreditsMenu : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private const string CREDITS_URL =
            "https://raw.githubusercontent.com/YARC-Official/Contributors/master/contributors.json";

        public class Contributor
        {
            public string Name;
            public string SpecialRole;
            public Dictionary<string, string> Socials;
            public Dictionary<string, string[]> Contributions;
        }

        [SerializeField]
        private Transform _creditsContainer;
        [SerializeField]
        private ScrollRect _scrollRect;
        [SerializeField]
        private float _maxScrollRate = 150f;
        [SerializeField]
        private float _scrollRateIncreaseMultiplier = 0.25f;

        [Space]
        [SerializeField]
        private GameObject _headerPrefab;
        [SerializeField]
        private CreditEntry _cardPrefab;
        [SerializeField]
        private SongCreditEntry _songCreditPrefab;

        private float _scrollRate;

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () => MenuManager.Instance.PopMenu())
            }, true));

            _scrollRate = _maxScrollRate;
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private void Start()
        {
#if UNITY_EDITOR
            // Make sure the credits are up to date in the editor
            DownloadCredits();
#endif

            var creditsFilePath = Path.Combine(PathHelper.StreamingAssetsPath, "Credits.json");
            var json = File.ReadAllText(creditsFilePath);
            var contributors = JsonConvert.DeserializeObject<Contributor[]>(json);

            CreateHeader("GameStartedBy");
            CreateCredits(contributors
                .Where(i => i.SpecialRole == "Founder")
            );

            CreateHeader("LeadArtist");
            CreateCredits(contributors
                .Where(i => i.SpecialRole == "LeadArtist")
            );

            CreateHeader("SetlistManager");
            CreateCredits(contributors
                .Where(i => i.SpecialRole == "SetlistManager")
            );

            CreateHeader("Maintainers");
            CreateCredits(contributors
                .Where(i => i.SpecialRole == "Maintainer")
            );

            CreateHeader("Contributors");
            CreateCredits(contributors
                .Where(i => string.IsNullOrEmpty(i.SpecialRole))
            );

            CreateHeader("SpecialThanks");
            CreateCredits(contributors
                .Where(i => i.SpecialRole == "Supporter")
            );

            CreateHeader("Songs");
            CreateSongCredits();
        }

        private void Update()
        {
            // Return the scroll rate
            if (_scrollRate < _maxScrollRate)
            {
                _scrollRate += Time.deltaTime * (_maxScrollRate * _scrollRateIncreaseMultiplier);
                _scrollRate = Mathf.Min(_scrollRate, _maxScrollRate);
            }

            if (_scrollRate > 0f)
            {
                // Use velocity, so the scroll speed stays consistent in different lengths
                _scrollRect.velocity = new Vector2(0f, _scrollRate);
            }
        }

        private void CreateHeader(string unlocalizedName)
        {
            var header = Instantiate(_headerPrefab, _creditsContainer);
            header.GetComponent<TextMeshProUGUI>().text = Localize.Key("Menu.Credits.Header", unlocalizedName);
        }

        private void CreateCredits(IEnumerable<Contributor> contributors)
        {
            // The contributors file is kind of in a random order
            contributors = contributors.OrderBy(i => i.Name);

            foreach (var contributor in contributors)
            {
                var card = Instantiate(_cardPrefab, _creditsContainer);
                card.Initialize(contributor);
            }
        }

        private void CreateSongCredits()
        {
            foreach (var song in SongContainer.Songs)
            {
                // If the song has any of these properties, then add it to the credits
                if (!string.IsNullOrEmpty(song.CreditWrittenBy) ||
                    !string.IsNullOrEmpty(song.CreditPerformedBy) ||
                    !string.IsNullOrEmpty(song.CreditCourtesyOf) ||
                    !string.IsNullOrEmpty(song.CreditAlbumArtDesignedBy) ||
                    !string.IsNullOrEmpty(song.CreditLicense))
                {
                    var card = Instantiate(_songCreditPrefab, _creditsContainer);
                    card.Initialize(song);
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            _scrollRate = -(_maxScrollRate * _scrollRateIncreaseMultiplier);
        }

        public void OnScroll(PointerEventData eventData)
        {
            _scrollRate = -(_maxScrollRate * _scrollRateIncreaseMultiplier);
        }

#if UNITY_EDITOR

        // Credits should only be downloaded in the editor or when
        // the game is building. In a build, the credits will just
        // be taken from the streaming assets folder, and will only
        // be updated when the game updates.

        public static void DownloadCredits()
        {
            var creditsPath = Path.Combine(Application.streamingAssetsPath, "Credits.json");

            if (File.Exists(creditsPath))
            {
                File.Delete(creditsPath);
            }

            using var client = new WebClient();
            var value = client.DownloadString(CREDITS_URL);
            File.WriteAllText(creditsPath, value);
        }

#endif
    }
}