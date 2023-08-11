using UnityEngine;
using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class ButtonBindView : BindView<float, ButtonBinding, SingleButtonBinding>
    {
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(EditProfileMenu editProfileMenu, ButtonBinding binding, SingleButtonBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);

            _debounceSlider.SetValueWithoutNotify(singleBinding.DebounceThreshold);
        }

        public void OnDebounceValueChanged(float value)
        {
            _singleBinding.DebounceThreshold = (long) value;
        }
    }
}