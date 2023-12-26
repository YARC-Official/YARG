using TMPro;
using UnityEngine;
using YARG.Menu.ListMenu;

namespace YARG.Menu.MusicLibrary
{
    public class SongView : ViewObject<ViewType>
    {
        [SerializeField]
        private TextMeshProUGUI _sideText;

        [Space]
        [SerializeField]
        private GameObject _secondaryTextContainer;
        [SerializeField]
        private GameObject _asMadeFamousByTextContainer;

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            // Set side text
            _sideText.text = viewType.GetSideText(selected);

            // Set secondary text type
            _secondaryTextContainer.SetActive(!viewType.UseAsMadeFamousBy);
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);
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
    }
}