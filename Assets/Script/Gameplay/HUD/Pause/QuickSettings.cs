namespace YARG.Gameplay.HUD
{
    public class QuickSettings : GenericPause
    {
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