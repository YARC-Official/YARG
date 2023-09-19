using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class ButtonBindView : BindView<float, ButtonBinding, SingleButtonBinding>
    {
        [SerializeField]
        private Slider _valueSlider;
        [SerializeField]
        private Image _pressedIndicator;

        [Space]
        [SerializeField]
        private Color _pressedColor;
        [SerializeField]
        private Color _releasedColor;

        [Space]
        [SerializeField]
        private ValueSlider _pressPointSlider;
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(EditProfileMenu editProfileMenu, ButtonBinding binding, SingleButtonBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);

            _pressPointSlider.SetValueWithoutNotify(singleBinding.PressPoint);
            _debounceSlider.SetValueWithoutNotify(singleBinding.DebounceThreshold);

            singleBinding.StateChanged += OnStateChanged;
            OnStateChanged(singleBinding.State);
        }

        private void OnDestroy()
        {
            SingleBinding.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(float state)
        {
            _valueSlider.value = state;

            _pressedIndicator.color = SingleBinding.IsPressed ? _pressedColor : _releasedColor;
        }

        public void OnPressPointChanged(float value)
        {
            SingleBinding.PressPoint = value;
        }

        public void OnDebounceValueChanged(float value)
        {
            SingleBinding.DebounceThreshold = (long) value;
        }
    }
}