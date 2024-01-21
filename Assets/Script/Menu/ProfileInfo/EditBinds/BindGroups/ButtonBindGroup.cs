using UnityEngine;
using YARG.Input;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public class ButtonBindGroup : BindGroup<SingleButtonBindView, float, ButtonBinding, SingleButtonBinding>
    {
        [Space]
        [SerializeField]
        private ButtonDisplay _rawPressedIndicator;
        [SerializeField]
        private ButtonDisplay _calibratedPressedIndicator;

        [Space]
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(EditBindsTab editBindsTab, YargPlayer player, ButtonBinding binding)
        {
            base.Init(editBindsTab, player, binding);

            _debounceSlider.SetValueWithoutNotify(binding.DebounceThreshold);
        }

        protected override void OnStateChanged()
        {
            _rawPressedIndicator.IsPressed = _binding.RawState;
            _calibratedPressedIndicator.IsPressed = _binding.State;
        }

        public void OnDebounceValueChanged(float value)
        {
            _binding.DebounceThreshold = (long) value;
        }
    }
}