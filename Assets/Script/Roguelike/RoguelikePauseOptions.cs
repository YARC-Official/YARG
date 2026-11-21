using UnityEngine;
using YARG.Menu.Navigation;

namespace YARG.Roguelike
{
    /// <summary>
    /// Hides pause and fail menu options that would let a roguelike round be retried or made easier
    /// mid-song. Options are found by the method their button calls, so no prefab changes are needed.
    /// </summary>
    public static class RoguelikePauseOptions
    {
        private static readonly string[] BlockedMethods =
        {
            "Restart",
            "TogglePractice",
            "EnableNoFail",
        };

        public static void HideBlockedOptions(Component pauseMenu)
        {
            if (!RoguelikeSession.IsRoguelikeSong)
            {
                return;
            }

            var navigationGroup = pauseMenu.GetComponentInChildren<NavigationGroup>(true);
            bool removedSelected = false;
            foreach (var button in pauseMenu.GetComponentsInChildren<NavigatableButton>(true))
            {
                if (!IsBlocked(button))
                {
                    continue;
                }

                if (navigationGroup != null)
                {
                    removedSelected |= navigationGroup.SelectedBehaviour == button;
                    navigationGroup.RemoveNavigatable(button);
                }
                button.gameObject.SetActive(false);
            }

            if (removedSelected)
            {
                // Don't leave the menu with nothing selected
                navigationGroup.SelectFirst();
            }
        }

        private static bool IsBlocked(NavigatableButton button)
        {
            foreach (var method in BlockedMethods)
            {
                if (button.CallsMethod(method))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
