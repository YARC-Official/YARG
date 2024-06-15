using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class QuickSettings : GenericPause
    {
        [SerializeField]
        private GameObject _editHudButton;

        protected override void OnSongStarted()
        {
            _editHudButton.gameObject.SetActive(GameManager.Players.Count <= 1);
        }

        public override void Back()
        {
            PauseMenuManager.PopMenu();
        }

        public void EditHUD()
        {
            GameManager.SetEditHUD(true);
        }
    }
}