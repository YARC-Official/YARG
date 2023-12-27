using UnityEngine;
using UnityEngine.UI;
using YARG.Input;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public class ButtonBindGroup : BindGroup<SingleButtonBindView, float, ButtonBinding, SingleButtonBinding>
    {
        [Space]
        [SerializeField]
        private Color _pressedColor;
        [SerializeField]
        private Color _releasedColor;

        [Space]
        [SerializeField]
        private Image _rawPressedIndicator;
        [SerializeField]
        private Image _calibratedPressedIndicator;

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
            _rawPressedIndicator.color = _binding.RawState ? _pressedColor : _releasedColor;
            _calibratedPressedIndicator.color = _binding.State ? _pressedColor : _releasedColor;
        }

        public void OnDebounceValueChanged(float value)
        {
            _binding.DebounceThreshold = (long) value;
        }
    }
}