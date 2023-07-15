using System;
using UnityEngine;
using YARG.Core;

namespace YARG.Menu
{
    public class BindingMenu : MonoBehaviour
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