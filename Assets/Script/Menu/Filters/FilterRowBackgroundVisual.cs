using System.Reflection;
using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Types;

namespace YARG.Menu.Filters
{
    public sealed class FilterRowBackgroundVisual : BaseSettingVisual
    {
        private static readonly FieldInfo EvenBackgroundField =
            typeof(BaseSettingVisual).GetField("_evenBackground", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            var evenBackground = transform.Find("Even Background");
            if (evenBackground != null)
            {
                EvenBackgroundField?.SetValue(this, evenBackground.gameObject);
            }
        }

        public override void AssignIndex(int index)
        {
            if (EvenBackgroundField?.GetValue(this) is GameObject)
            {
                base.AssignIndex(index);
            }
        }

        protected override void AssignSettingFromVariable(ISettingType reference)
        {
        }

        protected override void RefreshVisual()
        {
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return NavigationScheme.Empty;
        }
    }
}
