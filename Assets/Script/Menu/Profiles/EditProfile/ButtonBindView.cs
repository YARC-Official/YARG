using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class ButtonBindView : BindView<float, SingleButtonBinding>
    {
        public void OnDebounceValueChanged(float value)
        {
            _singleBinding.DebounceThreshold = (long) value;
        }
    }
}