using UnityEngine;
using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class AxisBindView : BindView<float, AxisBinding, SingleAxisBinding>
    {
        [SerializeField]
        private ValueSlider _maxValueSlider;
        [SerializeField]
        private ValueSlider _minValueSlider;
        [SerializeField]
        private ValueSlider _zeroPointSlider;

        public override void Init(EditProfileMenu editProfileMenu, AxisBinding binding, SingleAxisBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);

            _maxValueSlider.Value = singleBinding.Maximum;
            _minValueSlider.Value = singleBinding.Minimum;
            _zeroPointSlider.Value = singleBinding.ZeroPoint;
        }

        public void OnMaxValueChanged(float value)
        {
            SingleBinding.Maximum = value;

            if (value < SingleBinding.Minimum)
                _minValueSlider.Value = value;

            if (value < SingleBinding.ZeroPoint)
                _zeroPointSlider.Value = value;
        }

        public void OnMinValueChanged(float value)
        {
            SingleBinding.Minimum = value;

            if (value > SingleBinding.Maximum)
                _maxValueSlider.Value = value;

            if (value > SingleBinding.ZeroPoint)
                _zeroPointSlider.Value = value;
        }

        public void OnZeroPointChanged(float value)
        {
            SingleBinding.ZeroPoint = value;

            if (value > SingleBinding.Maximum)
                _maxValueSlider.Value = value;

            if (value < SingleBinding.Minimum)
                _minValueSlider.Value = value;
        }
    }
}