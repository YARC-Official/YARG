using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class AxisBindView : BindView<float, AxisBinding, SingleAxisBinding>
    {
        public override void Init(EditProfileMenu editProfileMenu, AxisBinding binding, SingleAxisBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);
        }
    }
}