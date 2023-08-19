using System;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;

namespace YARG.Menu.Settings
{
    public class BaseSettingNavigatable : NavigatableBehaviour
    {
        private BaseSettingVisual _baseSettingVisual;
        private bool _focused;

        protected override void Awake()
        {
            base.Awake();

            _baseSettingVisual = GetComponent<BaseSettingVisual>();
        }

        public override void Confirm()
        {
            var scheme = _baseSettingVisual.GetNavigationScheme();
            scheme.PopCallback = () =>
            {
                _focused = false;
            };

            Navigator.Instance.PushScheme(scheme);
            _focused = true;
        }

        private void OnDisable()
        {
            // If the visual's nav scheme is still in the stack, make sure to pop it.
            if (_focused)
            {
                Navigator.Instance.PopScheme();
            }
        }
    }
}