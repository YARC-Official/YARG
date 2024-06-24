using System.Collections.Generic;
using System.Linq;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Gameplay.HUD
{
    public class DraggableHudManager : GameplayBehaviour
    {
        public bool EditMode { get; private set; }

        public DraggableHudElement SelectedElement { get; private set; }

        private List<DraggableHudElement> _draggableElements;
        private bool _navigationPushed;

        private void Start()
        {
            _draggableElements = GetComponentsInChildren<DraggableHudElement>().ToList();
        }

        public void RemoveDraggableElement(DraggableHudElement elem)
        {
            _draggableElements.Remove(elem);
        }

        public void SetEditHUD(bool on)
        {
            EditMode = on;
            foreach (var element in _draggableElements)
            {
                element.OnEditModeChanged(on);
            }

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

        public void ResetAllHUDPositions()
        {
            foreach (var draggable in _draggableElements)
            {
                draggable.ResetElement();
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
                }),
                new NavigationScheme.Entry(MenuAction.Orange, "Reset All", () =>
                {
                    var dialog = DialogManager.Instance.ShowMessage("Are You Sure?",
                        "Are you sure you want to reset the position of all elements? This action cannot be undone.");

                    dialog.ClearButtons();
                    dialog.AddDialogButton("Cancel", MenuData.Colors.BrightButton, DialogManager.Instance.ClearDialog);
                    dialog.AddDialogButton("Reset", MenuData.Colors.CancelButton, () =>
                    {
                        ResetAllHUDPositions();
                        DialogManager.Instance.ClearDialog();
                    });
                })
            }, false));

            _navigationPushed = true;
        }
    }
}