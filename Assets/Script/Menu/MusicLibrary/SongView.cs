using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.ListMenu;

namespace YARG.Menu.MusicLibrary
{
    public class SongView : ViewObject<ViewType>
    {
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

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            // Set side text
            _sideText.text = viewType.GetSideText(selected);

            // Set star view
            var starAmount = viewType.GetStarAmount();
            _starView.gameObject.SetActive(starAmount is not null);
            if (starAmount is not null)
            {
                _starView.SetStars(starAmount.Value);
            }

            // Set secondary text type
            _secondaryTextContainer.SetActive(!viewType.UseAsMadeFamousBy);
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);

            // Show/hide favorite button
            var favoriteInfo = viewType.GetFavoriteInfo();
            _favoriteButtonContainer.SetActive(!selected && favoriteInfo.ShowFavoriteButton);
            _favoriteButtonContainerSelected.SetActive(selected && favoriteInfo.ShowFavoriteButton);
            UpdateFavoriteSprite(favoriteInfo);
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