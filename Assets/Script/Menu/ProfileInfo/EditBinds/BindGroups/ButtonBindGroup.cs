using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

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
        private Image _pressedIndicator;

        protected override void OnStateChanged()
        {
            _pressedIndicator.color = _binding.State ? _pressedColor : _releasedColor;
        }
    }
}