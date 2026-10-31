using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Playlists;

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
            if (!Setlist.HasNext)
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