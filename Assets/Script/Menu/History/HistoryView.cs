using UnityEngine.UI;
using YARG.Menu.ListMenu;

namespace YARG.Menu.History
{
    public class HistoryView : ViewObject<ViewType>
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void OnClick()
        {
            ViewType.ViewClick();
        }

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            _button.interactable = true;
        }

        public override void Hide()
        {
            base.Hide();

            _button.interactable = false;
        }
    }
}