using YARG.Core;
using YARG.Player.Input;

namespace YARG.Player
{
    public class Profile
    {
        public YargProfile ProfileInfo { get; private set; }

        public InputStrategy InputStrategy;
        public MicInput MicInput;

        public Profile(YargProfile profileInfo)
        {
            ProfileInfo = profileInfo;
        }
    }
}