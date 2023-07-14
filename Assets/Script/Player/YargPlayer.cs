using System;
using YARG.Core;
using YARG.Input;
using YARG.Player.Input;

namespace YARG.Player
{
    public class YargPlayer : IDisposable
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

        public void Dispose()
        {
            // Add all dispose stuff here

            InputStrategy?.Dispose();
        }
    }
}