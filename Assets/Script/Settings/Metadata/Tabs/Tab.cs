﻿using UnityEngine;
using YARG.Menu.Navigation;

namespace YARG.Settings.Metadata
{
    public abstract class Tab
    {
        public string Name { get; }
        public string Icon { get; }

        protected Tab(string name, string icon = "Generic")
        {
            Name = name;
            Icon = icon;
        }

        public abstract void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup);

        public virtual void BuildPreview(Transform uiContainer, Transform worldContainer)
        {
        }
    }
}