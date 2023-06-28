using System.Collections.Generic;
using YARG.Core;

namespace YARG.Player
{
    public static class ProfileContainer
    {
        private static List<Profile> _profiles;
        public static IReadOnlyList<Profile> Profiles => _profiles;

        public static void CreateProfile(YargProfile profileInfo)
        {
            var profile = new Profile(profileInfo);
            _profiles.Add(profile);
        }

        public static bool RemoveProfile(Profile profile)
        {
            return _profiles.Remove(profile);
        }
    }
}