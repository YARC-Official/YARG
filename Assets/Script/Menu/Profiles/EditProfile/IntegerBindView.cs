using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class IntegerBindView : BindView<int, IntegerBinding, SingleIntegerBinding>
    {
        public override void Init(EditProfileMenu editProfileMenu, IntegerBinding binding, SingleIntegerBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);
        }
    }
}