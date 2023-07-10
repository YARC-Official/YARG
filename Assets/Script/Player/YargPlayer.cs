using YARG.Core;
using YARG.Input;
using YARG.Player.Input;

namespace YARG.Player
{
    public class YargPlayer
    {
        public YargProfile Profile { get; private set; }

        public InputStrategy InputStrategy;
        public MicInput MicInput;

        public ProfileBindings Bindings { get; private set; }

        public YargPlayer(YargProfile profile)
        {
            Profile = profile;
            Bindings = new(profile);
        }

        public void SetProfile(YargProfile profile)
        {
            if (Profile is not null)
            {
                if (!ProfileContainer.ReturnProfile(Profile))
                {
                    return;
                }
            }

            if (ProfileContainer.TakeProfile(profile))
            {
                Profile = profile;
                Bindings = new(profile);
            }
        }
    }
}