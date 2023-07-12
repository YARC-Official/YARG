using UnityEngine;

namespace YARG.Menu.BindingMenu
{
    public class BindingMenu : MonoBehaviour
    {
        public void Back()
        {
            MenuNavigator.Instance.PopMenu();
        }
    }
}