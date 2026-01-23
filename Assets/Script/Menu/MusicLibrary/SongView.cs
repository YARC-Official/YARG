using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Menu.ListMenu;
using YARG.Settings;
using YARG.Song;

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

            StarAmount starHeaderAmount = StarAmount.None;

            foreach (StarAmount amount in Enum.GetValues(typeof(StarAmount)))
            {
                if (amount == StarAmount.None || amount == StarAmount.NoPart)
                    continue; // skip irrelevant entries

                string displayName = amount.GetDisplayName();

                if (_categoryText.text.Contains(">" + displayName + "<"))
                {
                    starHeaderAmount = amount;
                    break;
                }
            }

            if (starHeaderAmount != StarAmount.None && SettingsManager.Settings.LibrarySort == SortAttribute.Stars)
            {
                int starCount = starHeaderAmount.GetStarCount();
                Sprite starSprite = starHeaderAmount == StarAmount.StarGold ? _starGoldSprite : _starWhiteSprite;

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