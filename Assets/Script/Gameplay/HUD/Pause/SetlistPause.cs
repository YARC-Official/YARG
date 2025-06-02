using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class SetlistPause : GenericPause
    {
        [SerializeField]
        private GameObject _skipObject;

        protected override void GameplayAwake()
        {
            base.GameplayAwake();

            if (GlobalVariables.State.ShowIndex == GlobalVariables.State.ShowSongs.Count - 1)
            {
                // There is no next song, so hide the skip button
                _skipObject.SetActive(false);
            }
        }

        public void Skip()
        {
            PauseMenuManager.Skip();
        }
    }
}