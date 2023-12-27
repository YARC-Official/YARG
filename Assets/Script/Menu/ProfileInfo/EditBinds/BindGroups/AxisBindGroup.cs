using UnityEngine;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class AxisBindGroup : BindGroup<SingleAxisBindView, float, AxisBinding, SingleAxisBinding>
    {
        [Space]
        [SerializeField]
        private AxisDisplay _valueDisplay;

        protected override void OnStateChanged()
        {
            _valueDisplay.Value = _binding.State;
        }
    }
}