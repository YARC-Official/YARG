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

        private static readonly List<YargProfile> AvailableProfiles;
        private static readonly List<YargProfile> TakenProfiles;

        static ProfileContainer()
        {
            AvailableProfiles = new List<YargProfile>();
            TakenProfiles = new List<YargProfile>();
        }

        public static IReadOnlyList<YargProfile> Profiles => AvailableProfiles;

        public static bool TakeProfile(YargProfile profile)
        {
            if (!AvailableProfiles.Contains(profile))
            {
                return false;
            }

            AvailableProfiles.Remove(profile);
            TakenProfiles.Add(profile);
            return true;
        }

        public static bool ReturnProfile(YargProfile profile)
        {
            if (AvailableProfiles.Contains(profile))
            {
                return false;
            }
            if (!TakenProfiles.Contains(profile))
            {
                return false;
            }

            AvailableProfiles.Add(profile);
            TakenProfiles.Remove(profile);
            return true;
        }

        public static bool AddProfile(YargProfile profile)
        {
            if (AvailableProfiles.Contains(profile))
            {
                return false;
            }

            AvailableProfiles.Add(profile);
            return true;
        }

        public static bool RemoveProfile(YargProfile profile)
        {
            // TODO: Where would we handle removing YargPlayers with this profile?
            if (TakenProfiles.Contains(profile))
            {
                return false;
            }

            if (!AvailableProfiles.Contains(profile))
            {
                return false;
            }

            AvailableProfiles.Remove(profile);
            return true;
        }

        public static int LoadProfiles()
        {
            AvailableProfiles.Clear();
            TakenProfiles.Clear();

            string profilesPath = Path.Combine(PathHelper.PersistentDataPath, "profiles.json");
            if (!File.Exists(profilesPath))
            {
                return 0;
            }

            string profilesJson = File.ReadAllText(profilesPath);

            var profiles = JsonConvert.DeserializeObject<List<YargProfile>>(profilesJson);

            if (profiles is not null)
            {
                AvailableProfiles.AddRange(profiles);
            }

            return AvailableProfiles.Count;
        }

        public static int SaveProfiles()
        {
            // Test profiles, remove later
            var profile1 = new YargProfile
            {
                Name = "Riley",
                NoteSpeed = 10,
                HighwayLength = 1.5f,
            };

            var profile2 = new YargProfile
            {
                Name = "Nathan",
                NoteSpeed = 8,
                HighwayLength = 1.2f,
                InstrumentType = GameMode.FourLaneDrums,
            };

            var profile3 = new YargProfile
            {
                Name = "EliteAsian",
                NoteSpeed = 3,
                HighwayLength = 0.5f,
                InstrumentType = GameMode.Vocals,
                IsBot = true,
            };

            string profilesPath = Path.Combine(PathHelper.PersistentDataPath, "profiles.json");

            var allProfiles = new List<YargProfile>();
            allProfiles.AddRange(AvailableProfiles);
            allProfiles.AddRange(TakenProfiles);

            // Test (prevents duplicating profiles over and over again)
            if (allProfiles.Count == 0)
            {
                allProfiles.Add(profile1);
                allProfiles.Add(profile2);
                allProfiles.Add(profile3);
            }

            string profilesJson = JsonConvert.SerializeObject(allProfiles, Formatting.Indented);

            File.WriteAllText(profilesPath, profilesJson);

            return allProfiles.Count;
        }
    }
}