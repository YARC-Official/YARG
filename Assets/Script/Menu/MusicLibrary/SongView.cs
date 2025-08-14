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

        [Space]
        [SerializeField]
        private Image _trackGradient;
        [SerializeField]
        private Image _normalCategoryHeaderGradient;

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            // use category header primary text (which supports wider text), when used as section header
            if(viewType.UseWiderPrimaryText)
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
            var scoreInfo = viewType.GetScoreInfo();
            _instrumentDifficultyViewContainer.SetActive(scoreInfo is not null);
            if (scoreInfo is not null)
            {
                _instrumentDifficultyView.SetInfo(scoreInfo.Value);
            }

            // Set star view
            var starAmount = viewType.GetStarAmount();
            _starView.gameObject.SetActive(starAmount is not null);
            if (starAmount is not null)
            {
                _starView.SetStars(starAmount.Value);
            }

            // Set "As Made Famous By" text
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);

            // Set stars obtained view
            _starsObtainedView.gameObject.SetActive(viewType is SortHeaderViewType);
            if (viewType is SortHeaderViewType)
            {
                _starsObtainedText.text = viewType.GetSideText(selected);
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
            else
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
            }
        }

        protected override void SetBackground(bool selected, BaseViewType.BackgroundType type)
        {
            base.SetBackground(selected, type);

            _trackGradient.gameObject.SetActive(false);
            _normalCategoryHeaderGradient.gameObject.SetActive(false);
            if (selected && type is BaseViewType.BackgroundType.Category)
            {
                _normalCategoryHeaderGradient.gameObject.SetActive(true);
            }
            else
            {
                _trackGradient.gameObject.SetActive(true);
            }

            NormalBackground.SetActive(false);
            SelectedBackground.SetActive(false);
            CategoryBackground.SetActive(false);

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