using System.Collections.Generic;
using YARG.Core;

namespace YARG.Player
{
    public static class ProfileContainer
    {

        /*

         Profiles can only be assigned to 1 player at a time so the ProfileContainer only exposes the available profiles.

         */

        private static List<YargProfile> _availableProfiles;
        private static List<YargProfile> _takenProfiles;

        public static IReadOnlyList<YargProfile> AvailableProfiles => _availableProfiles;

        public static bool TakeProfile(YargProfile profile)
        {
            if (!_availableProfiles.Contains(profile))
            {
                return false;
            }

            _availableProfiles.Remove(profile);
            _takenProfiles.Add(profile);
            return true;
        }

        public static bool ReturnProfile(YargProfile profile)
        {
            if (_availableProfiles.Contains(profile))
            {
                return false;
            }
            if (!_takenProfiles.Contains(profile))
            {
                return false;
            }

            _availableProfiles.Add(profile);
            _takenProfiles.Remove(profile);
            return true;
        }
    }
}