using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core;
using YARG.Util;

namespace YARG.Player
{
    public static class ProfileContainer
    {

        /*

         Profiles can only be assigned to 1 player at a time so the ProfileContainer only exposes the available profiles.

         */

        private static readonly List<YargProfile> _allProfiles;
        private static readonly List<YargProfile> _takenProfiles;

        public static IReadOnlyList<YargProfile> Profiles => _allProfiles;
        public static IReadOnlyList<YargProfile> TakenProfiles => _takenProfiles;

        static ProfileContainer()
        {
            _allProfiles = new List<YargProfile>();
            _takenProfiles = new List<YargProfile>();
        }

        public static bool TakeProfile(YargProfile profile)
        {
            if (_takenProfiles.Contains(profile))
            {
                return false;
            }

            _takenProfiles.Add(profile);
            return true;
        }

        public static bool ReturnProfile(YargProfile profile)
        {
            if (!_takenProfiles.Contains(profile))
            {
                return false;
            }

            _takenProfiles.Remove(profile);
            return true;
        }

        public static bool AddProfile(YargProfile profile)
        {
            if (_allProfiles.Contains(profile))
            {
                return false;
            }

            _allProfiles.Add(profile);
            return true;
        }

        public static bool RemoveProfile(YargProfile profile)
        {
            if (!_allProfiles.Contains(profile))
            {
                return false;
            }

            // TODO: Where would we handle removing YargPlayers with this profile?
            if (_takenProfiles.Contains(profile))
            {
                return false;
            }

            _allProfiles.Remove(profile);
            return true;
        }

        public static int LoadProfiles()
        {
            _allProfiles.Clear();
            _takenProfiles.Clear();

            string profilesPath = Path.Combine(PathHelper.PersistentDataPath, "profiles.json");
            if (!File.Exists(profilesPath))
            {
                return 0;
            }

            string profilesJson = File.ReadAllText(profilesPath);

            var profiles = JsonConvert.DeserializeObject<List<YargProfile>>(profilesJson);

            if (profiles is not null)
            {
                _allProfiles.AddRange(profiles);
            }

            return _allProfiles.Count;
        }

        public static int SaveProfiles()
        {
            string profilesPath = Path.Combine(PathHelper.PersistentDataPath, "profiles.json");

            string profilesJson = JsonConvert.SerializeObject(_allProfiles, Formatting.Indented);

            File.WriteAllText(profilesPath, profilesJson);

            return _allProfiles.Count;
        }
    }
}