using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.ListMenu;
using YARG.Settings;

namespace YARG.Menu.MusicLibrary
{
    public class SongView : ViewObject<ViewType>
    {
        [SerializeField]
        private GameObject _songNameContainer;
        [SerializeField]
        private TextMeshProUGUI _sideText;
        [SerializeField]
        private StarView _starView;

        [Space]
        [SerializeField]
        private GameObject _secondaryTextContainer;
        [SerializeField]
        private GameObject _asMadeFamousByTextContainer;

        [Space]
        [SerializeField]
        private GameObject _favoriteButtonContainer;
        [SerializeField]
        private GameObject _favoriteButtonContainerSelected;
        [SerializeField]
        private Image[] _favoriteButtons;

        [Space]
        [SerializeField]
        private Sprite _favoriteUnfilled;
        [SerializeField]
        private Sprite _favouriteFilled;

        [Space]
        [SerializeField]
        private GameObject _categoryNameContainer;
        [SerializeField]
        private TextMeshProUGUI _categoryText;
        
        [SerializeField]
        private GameObject _starHeaderGroup;
        [SerializeField]
        private Image[] _starHeaderImages;

        [SerializeField]
        private Sprite _starGoldSprite;
        [SerializeField]
        private Sprite _starWhiteSprite;

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            // use category header primary text (which supports wider text), when used as section header
            if (viewType.UseWiderPrimaryText)
            {
                _songNameContainer.SetActive(false);
                _categoryNameContainer.SetActive(true);
            }
            else
            {
                _songNameContainer.SetActive(true);
                _categoryNameContainer.SetActive(false);
            }

            // Set side text
            _sideText.text = viewType.GetSideText(selected);

            // Set star view
            var starAmount = viewType.GetStarAmount();
            _starView.gameObject.SetActive(starAmount is not null);
            if (starAmount is not null)
            {
                _starView.SetStars(starAmount.Value);
            }

            // Set "As Made Famous By" text
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);

            // Show/hide favorite button

            var favoriteInfo = viewType.GetFavoriteInfo();

            if (SettingsManager.Settings.ShowFavoriteButton.Value)
            {
                _favoriteButtonContainer.SetActive(!selected && favoriteInfo.ShowFavoriteButton);
                _favoriteButtonContainerSelected.SetActive(selected && favoriteInfo.ShowFavoriteButton);
                UpdateFavoriteSprite(favoriteInfo);
            }
            else
            {
                _favoriteButtonContainer.SetActive(false);
                _favoriteButtonContainerSelected.SetActive(false);
            }

            string header = _categoryText.text;
            string CleanHeaderText(string header)
            {
                return Regex.Replace(header, "<.*?>", string.Empty);
            }

            string rawHeader = CleanHeaderText(_categoryText.text);

            int parsedStars = 0;
            bool isGoldStars = rawHeader == "Gold Stars";
            bool isNumericStars = false;

            Match match = Regex.Match(rawHeader, @"^(\d+) Stars$");
            if (match.Success)
            {
                isNumericStars = int.TryParse(match.Groups[1].Value, out parsedStars);
            }

            if (isGoldStars || isNumericStars)
            {
                int starCount = isGoldStars ? 5 : parsedStars;
                Sprite starSprite = isGoldStars ? _starGoldSprite : _starWhiteSprite;

                _categoryText.gameObject.SetActive(false);
                _starHeaderGroup.SetActive(true);

                for (int i = 0; i < _starHeaderImages.Length; i++)
                {
                    bool show = i < starCount;
                    _starHeaderImages[i].gameObject.SetActive(show);
                    if (show)
                    {
                        _starHeaderImages[i].sprite = starSprite;
                    }
                }
            }
            else
            {
                _categoryText.gameObject.SetActive(true);
                _starHeaderGroup.SetActive(false);
            }
        }

        private void UpdateFavoriteSprite(ViewType.FavoriteInfo favoriteInfo)
        {
            if (!favoriteInfo.ShowFavoriteButton) return;

            foreach (var button in _favoriteButtons)
            {
                button.sprite = favoriteInfo.IsFavorited
                    ? _favouriteFilled
                    : _favoriteUnfilled;
            }
        }

        public void PrimaryTextClick()
        {
            if (!Showing) return;

            ViewType.PrimaryButtonClick();
        }

        public void SecondaryTextClick()
        {
            if (!Showing) return;

            ViewType.SecondaryTextClick();
        }

        public void FavoriteClick()
        {
            if (!Showing) return;

            ViewType.FavoriteClick();

            // Update the sprite after in case the state changed
            UpdateFavoriteSprite(ViewType.GetFavoriteInfo());
        }
    }
}