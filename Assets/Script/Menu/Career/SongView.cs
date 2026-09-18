using TMPro;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Career
{
    /// <summary>
    /// The one pool object CareerMenu uses for every row kind (progress header, tier header, locked
    /// tier header, song), so it switches layout from the row's <see cref="ViewType.CareerInfo"/>.
    /// Every reference is optional: unwired parts are simply skipped and the row falls back to the
    /// primary/secondary text that <see cref="ViewObject{TViewType}"/> already fills in.
    /// </summary>
    public class SongView : ViewObject<ViewType>
    {
        // Song-only widgets (the star display). Deliberately not the primary/secondary text nodes:
        // tier headers use those too.
        [SerializeField]
        private GameObject _songExtrasContainer;

        [Space]
        // Optional. The node holding the *secondary* (artist / album) line; it is hidden on the career
        // header row, which uses the header widgets below instead. Pointing this at Primary Text blanks
        // the player names on row 0.
        [SerializeField]
        private GameObject _secondaryTextContainer;

        [Space]
        [Header("Career Header Row")]
        [SerializeField]
        private GameObject _headerContainer;
        [SerializeField]
        private TextMeshProUGUI _playerNamesText;
        [SerializeField]
        private TextMeshProUGUI _currentTierText;
        [SerializeField]
        private TextMeshProUGUI _tierStarsText;
        [SerializeField]
        private TextMeshProUGUI _totalStarsText;

        [Space]
        [Header("Song Row")]
        [SerializeField]
        private StarView _starView;

        [Space]
        [Header("Row Heights")]
        [SerializeField]
        private float _headerRowHeight = 70f;
        [SerializeField]
        private float _songRowHeight = 105f;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        /// <summary>
        /// Optional: call from a button on the row prefab. Locked rows are selectable for browsing
        /// but start nothing.
        /// </summary>
        public void OnClick()
        {
            if (ViewType is not null && ViewType.IsClickable)
            {
                ViewType.ViewClick();
            }
        }

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            var info = viewType?.GetCareerInfo();
            var kind = info?.Kind ?? ViewType.CareerRowKind.Song;
            var isSong = kind == ViewType.CareerRowKind.Song;
            var isCareerHeader = kind == ViewType.CareerRowKind.CareerHeader;

            // Headers are a single summary line, songs get the full row.
            if (_rect != null)
            {
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    isSong || isCareerHeader ? _songRowHeight : _headerRowHeight);
            }

            SetActive(_songExtrasContainer, isSong);
            SetActive(_headerContainer, isCareerHeader);

            // Headers have no detail line of their own; the career header uses its own widgets.
            SetActive(_secondaryTextContainer, !isCareerHeader);

            if (isCareerHeader)
            {
                PopulateHeader(info.Value);
            }

            PopulateStars(info);
        }

        private void PopulateHeader(ViewType.CareerInfo info)
        {
            SetText(_playerNamesText, info.PlayerNames);
            SetText(_currentTierText, info.TierName);
            SetText(_tierStarsText, StarText(info.EarnedStars, info.AvailableStars));
            SetText(_totalStarsText, StarText(info.TotalEarnedStars, info.TotalAvailableStars));
        }

        /// <summary>
        /// Best stars for the song; 0 (nothing filled in) means it has not been completed yet. Header
        /// rows report their stars as text instead, so the display is hidden there.
        /// </summary>
        private void PopulateStars(ViewType.CareerInfo? info)
        {
            if (_starView == null)
            {
                return;
            }

            var showStars = info?.Kind == ViewType.CareerRowKind.Song;
            _starView.gameObject.SetActive(showStars);

            if (showStars)
            {
                _starView.SetStars(info.Value.BestStars);
            }
        }

        private static string StarText(int earned, int available)
        {
            return Localize.KeyFormat("Menu.Career.StarProgress", earned, available);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void SetText(TextMeshProUGUI target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }
    }
}
