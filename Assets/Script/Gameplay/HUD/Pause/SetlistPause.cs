using UnityEngine;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class SetlistPause : GenericPause
    {
        [SerializeField]
        private GameObject _skipObject;

        protected override void OnEnable()
        {
            base.OnEnable();

            // Disable skip option if this is the last song
            if (GlobalVariables.State.ShowIndex == GlobalVariables.State.ShowSongs.Count - 1)
            {
                _skipObject.SetActive(false);
                var navigationGroup = GetComponentInChildren<NavigationGroup>();
                navigationGroup.RemoveNavigatable(_skipObject.GetComponent<NavigatableBehaviour>());
            }
        }

        public void Skip()
        {
            PauseMenuManager.Skip();
        }
    }
}