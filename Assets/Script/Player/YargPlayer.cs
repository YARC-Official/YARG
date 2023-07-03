using YARG.Core;
using YARG.Player.Input;

namespace YARG.Player
{
    public class YargPlayer
    {
        public YargProfile Profile { get; private set; }

        public InputStrategy InputStrategy;
        public MicInput MicInput;

        public YargPlayer(YargProfile profile)
        {
            Profile = profile;
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
            }
        }
    }
}