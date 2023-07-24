using UnityEngine;
using YARG.Core;

namespace YARG.Menu.EditProfile
{
    public class EditProfileMenu : MonoBehaviour
    {
        public static YargProfile CurrentProfile { get; set; }

        private void OnEnable()
        {
            RefreshBindings();
        }

        private void RefreshBindings()
        {

        }
    }
}