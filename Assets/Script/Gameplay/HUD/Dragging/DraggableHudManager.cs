using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class DraggableHudManager : GameplayBehaviour
    {
        public bool EditMode { get; private set; }

        public DraggableHudElement SelectedElement { get; private set; }

        private bool _navigationPushed;

        public void SetEditHUD(bool on)
        {
            EditMode = on;

            if (on)
            {
                RegisterNavigationScheme();
            }
            else
            {
                if (_navigationPushed)
                {
                    Navigator.Instance.PopScheme();
                    _navigationPushed = false;
                }

                if (SelectedElement != null)
                {
                    SelectedElement.Deselect();
                }
            }
        }

        public void SetSelectedElement(DraggableHudElement element)
        {
            // Deselect the last element
            if (SelectedElement != null)
            {
                SelectedElement.Deselect();
            }

            // Select the new element
            SelectedElement = element;
            SelectedElement.Select();
        }

        private void RegisterNavigationScheme()
        {
            if (_navigationPushed)
            {
                return;
            }

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Red, "Back", () =>
                {
                    GameManager.SetEditHUD(false);
                })
            }, false));

            _navigationPushed = true;
        }
    }
}