using UnityEngine;

namespace YARG.Menu.Data
{
    public class MenuData : MonoSingleton<MenuData>
    {
        public static NavigationIcons NavigationIcons => Instance._navigationIcons;
        public static MenuColors Colors => Instance._colors;

        [SerializeField]
        private NavigationIcons _navigationIcons;
        [SerializeField]    
        private MenuColors _colors;
    }
}