using System.Collections.Generic;
using YARG.Core;

namespace YARG.Input
{
    public static class BindsContainer
    {

        private static Dictionary<YargProfile, ProfileBindsContainer> _binds;

        public static ProfileBindsContainer GetBindsForProfile(YargProfile profile)
        {
            if (_binds.TryGetValue(profile, out var profileBinds))
            {
                return profileBinds;
            }

            var newContainer = new ProfileBindsContainer();
            _binds.Add(profile, newContainer);

            return newContainer;
        }

    }
}