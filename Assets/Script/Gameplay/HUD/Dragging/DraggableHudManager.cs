using System.Collections.Generic;
using System.Linq;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class DraggableHudManager : GameplayBehaviour
    {
        public bool EditMode { get; private set; }

        public DraggableHudElement SelectedElement { get; private set; }
        public HUDPositionProfile PositionProfile { get; private set; }

        private List<DraggableHudElement> _draggableElements;
        private bool _navigationPushed;

        protected override void GameplayAwake()
        {
            base.GameplayAwake();

            // Get the correct HUD profile based on the gameplay
            string profileName;
            if (GlobalVariables.State.IsReplay)
            {
                profileName = "Replay";
            }
            else if (GlobalVariables.State.IsPractice)
            {
                profileName = "Practice";
            }
            else
            {
                profileName = "Normal";
            }

            // Load that profile. If the profile version does not match the current one, create a brand new one.
            if (SettingsManager.Settings.HUDPositionProfiles.TryGetValue(profileName, out var profile) &&
                profile.Version == HUDPositionProfile.CURRENT_VERSION)
            {
                PositionProfile = profile;
            }
            else
            {
                PositionProfile = new HUDPositionProfile();
                SettingsManager.Settings.HUDPositionProfiles[profileName] = PositionProfile;
            }
        }

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
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () =>
                {
                    GameManager.SetEditHUD(false);
                }),
                new NavigationScheme.Entry(MenuAction.Select, "Menu.Common.ResetAll", () =>
                {
                    var dialog = DialogManager.Instance.ShowMessage("Are You Sure?",
                        "Are you sure you want to reset the position of all elements? This action cannot be undone.");

                    dialog.ClearButtons();
                    dialog.AddDialogButton("Menu.Common.Cancel", MenuData.Colors.BrightButton,
                        DialogManager.Instance.ClearDialog);
                    dialog.AddDialogButton("Menu.Common.Reset", MenuData.Colors.CancelButton, () =>
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