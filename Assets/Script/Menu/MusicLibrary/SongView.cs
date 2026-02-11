using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Data;
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
        private GameObject _instrumentDifficultyViewContainer;
        [SerializeField]
        private InstrumentDifficultyView _instrumentDifficultyView;
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
        private RectTransform _starsObtainedView;
        [SerializeField]
        private TextMeshProUGUI _starsObtainedText;
        [SerializeField]
        private TextMeshProUGUI _scoreText;

        [Space]
        [SerializeField]
        private Image _trackGradient;
        [SerializeField]
        private Image _normalCategoryHeaderGradient;
        [SerializeField]
        private GameObject _buttonHeaderBackground;

        [SerializeField]
        private Image _selectedSourceIconBackground;

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

            var scoreInfoMode = SettingsManager.Settings.HighScoreInfo.Value;

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

            _selectedSourceIconBackground.enabled = selected & viewType is SongViewType;

            // Set score and instrument display
            var scoreInfo = viewType.GetScoreInfo();
            _instrumentDifficultyViewContainer.SetActive(scoreInfo is not null);
            if (scoreInfo is not null)
            {
                _instrumentDifficultyView.SetInfo(scoreInfo.Value);
            }

            // Set star view
            var starAmount = viewType.GetStarAmount();
            _starView.gameObject.SetActive(scoreInfoMode == HighScoreInfoMode.Stars && starAmount is not null);
            if (starAmount is not null)
            {
                _starView.SetStars(starAmount.Value);
            }

            // Set "As Made Famous By" text
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);
            if (viewType.UseAsMadeFamousBy)
            {
                _asMadeFamousByTextContainer.GetComponent<TextMeshProUGUI>().color = selected ? MenuData.Colors.BrightText : MenuData.Colors.TrackDefaultSecondary;
            }

            // Set stars obtained view
            _starsObtainedView.gameObject.SetActive(viewType is SortHeaderViewType);
            if (viewType is SortHeaderViewType)
            {
                _starsObtainedText.text = viewType.GetSideText(selected);
            }

            _scoreText.gameObject.SetActive(scoreInfoMode == HighScoreInfoMode.Score && viewType is SongViewType);
            if (scoreInfoMode == HighScoreInfoMode.Score)
            {
                _scoreText.text = viewType.GetSideText(selected);
            }

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

            // Set height
            if (viewType is SortHeaderViewType)
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
            }
            else if (viewType is ButtonViewType)
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 70);
            }
            else
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
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

        protected override void SetBackground(bool selected, BaseViewType.BackgroundType type)
        {
            _trackGradient.gameObject.SetActive(selected);

            NormalBackground.SetActive(false);
            SelectedBackground.SetActive(false);
            CategoryBackground.SetActive(false);
            _buttonHeaderBackground.SetActive(false);

            switch (type)
            {
                case BaseViewType.BackgroundType.Normal:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        NormalBackground.SetActive(true);
                    }

                    break;
                case BaseViewType.BackgroundType.Category:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        CategoryBackground.SetActive(true);
                    }

                    break;
                case BaseViewType.BackgroundType.Button:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        _buttonHeaderBackground.SetActive(true);
                    }

                    break;
                default:
                    break;
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