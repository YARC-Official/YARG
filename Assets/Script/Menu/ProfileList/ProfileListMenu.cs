using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Localization;
using YARG.Menu.HighwayConfiguration;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Settings.Customization;
using static YARG.Core.Game.ColorProfile;
using static YARG.Menu.HighwayConfiguration.DrumsHighwayConfigurationMenu;

namespace YARG.Menu.ProfileList
{
    public class ProfileListMenu : MonoBehaviour
    {
        [SerializeField]
        private NavigationGroup _navigationGroup;

        [Space]
        [SerializeField]
        private ProfileSidebar _profileSidebar;
        [SerializeField]
        private Transform _profileList;

        [Space]
        [SerializeField]
        private GameObject _profileViewPrefab;
        [SerializeField]
        private GameObject _profileListHeaderPrefab;

        private readonly int _maxConnected = HighwayCameraRendering.MAX_MATRICES;

        public bool CanConnectProfile => PlayerContainer.Players.Count < _maxConnected;

        private void OnEnable()
        {
            RefreshList();

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () => MenuManager.Instance.PopMenu(), hide: true),
            }, true));

            PlayerContainer.PlayerAdded += OnPlayerAdded;
        }

        private void OnDisable()
        {
            PlayerContainer.EnsureValidInstruments();
            PlayerContainer.SaveProfiles();

            // Update player icons if a profile has changed its GameMode.
            StatsManager.Instance.UpdateActivePlayers();

            Navigator.Instance.PopScheme();

            PlayerContainer.PlayerAdded -= OnPlayerAdded;
        }

        public void RefreshList(YargProfile selectedProfile = null)
        {
            // Deselect
            _profileSidebar.HideContents();

            // Remove old ones
            _profileList.transform.DestroyChildren();
            _navigationGroup.ClearNavigatables();

            var activeProfiles = PlayerContainer.Players.Select(e => e.Profile).ToArray();
            var otherProfiles = PlayerContainer.Profiles.Except(activeProfiles).OrderBy(e => e.Name).ToArray();

            AddListGroup(Localize.Key("Menu.ProfileList.ActiveProfiles"), activeProfiles);
            AddListGroup(Localize.Key("Menu.ProfileList.Players"), otherProfiles.Where(e => !e.IsBot));
            AddListGroup(Localize.Key("Menu.ProfileList.Bots"), otherProfiles.Where(e => e.IsBot));

            if (selectedProfile == null)
            {
                return;
            }

            SetSelectedProfile(selectedProfile);
        }

        private void AddListGroup(string header, IEnumerable<YargProfile> profiles)
        {
            if (!profiles.Any())
            {
                return;
            }

            var headerGo = Instantiate(_profileListHeaderPrefab, _profileList);
            headerGo.GetComponentInChildren<TextMeshProUGUI>().text = header;
            _navigationGroup.AddNavigatable(headerGo);

            // Spawn in a profile view for each player
            foreach (var profile in profiles)
            {
                var go = Instantiate(_profileViewPrefab, _profileList);
                go.GetComponent<ProfileView>().Init(this, profile, _profileSidebar);
                _navigationGroup.AddNavigatable(go);
            }
        }

        // TODO: Since we're using this outside of ProfileListMenu, we should probably find a better home for it
        public static string GetUniqueProfileName(string profileName)
        {
            var existingNames = PlayerContainer.Profiles.Select(p => p.Name);

            if (!existingNames.Contains(profileName))
            {
                return profileName;
            }

            int count = 1;
            string newName;
            do
            {
                newName = $"{profileName} {count}";
                count++;
            } while (existingNames.Contains(newName));

            return newName;
        }

        public void AddProfile()
        {
            PlayerContainer.AddProfile(new YargProfile
            {
                Name = GetUniqueProfileName("New Profile"),
                NoteSpeed = 5,
                HighwayLength = 1,
                GameMode = GameMode.FiveFretGuitar
            });

            RefreshList();
        }

        public void AddBotProfile()
        {
            PlayerContainer.AddProfile(new YargProfile
            {
                Name = GetUniqueProfileName("Bot"),
                NoteSpeed = 5,
                HighwayLength = 1,
                GameMode = GameMode.FiveFretGuitar,
                IsBot = true
            });

            RefreshList();
        }

        public void MoveProfileUp(YargProfile profile)
        {
            PlayerContainer.MoveUp(PlayerContainer.GetPlayerFromProfile(profile));
            RefreshList(profile);
        }

        public void MoveProfileDown(YargProfile profile)
        {
            PlayerContainer.MoveDown(PlayerContainer.GetPlayerFromProfile(profile));
            RefreshList(profile);
        }

        #nullable enable
        private YargProfile? GetSelectedProfile()
        #nullable disable
        {
            var profileView = _profileList.GetComponentsInChildren<ProfileView>()
                .FirstOrDefault(e => e.Selected);
            if (profileView != null)
            {
                return profileView.Profile;
            }

            return null;
        }

        public void SetSelectedProfile(YargProfile profile)
        {
            // Have to use LastOrDefault() here as this GetComponentsInChildren() call may include recently Destroyed objects.
            var profileView = _profileList.GetComponentsInChildren<ProfileView>()
                .LastOrDefault(e => e.Profile == profile);
            if (profileView != null)
            {
                profileView.SetSelected(true, SelectionOrigin.Programmatically);
            }
        }

        public void OnPlayerAdded(YargPlayer player)
        {
            RefreshList(GetSelectedProfile());
        }

        private void OpenDrumsHighwayConfigurationMenu(
            Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> specs,
            IFretColorProvider colorProvider,
            List<DrumsHighwayItem> defaultList,
            string header,
            SetOrdering setOrderingInProfile,
            Instrument instrument,
            YargProfile profile
        ) {
            var menu = DrumsHighwayConfigurationMenu.Instance;
            if (menu == null)
                return;


            menu.Initialize(
                specs,
                colorProvider,
                defaultList,
                Localize.Key("Menu.HighwayOrdering", header),
                setOrderingInProfile,
                instrument,
                profile
            );

            menu.gameObject.SetActive(true);
        }
        public void CloseDrumsHighwayConfigurationMenu()
        {
            var menu = DrumsHighwayConfigurationMenu.Instance;
            if (menu == null)
                return;

            menu.gameObject.SetActive(false);
        }

        public void OpenFourLaneDrumsHighwayConfigurationMenu()
        {
            var profile = GetSelectedProfile();
            var colorProvider = CustomContentManager.ColorProfiles.GetPresetById(profile.ColorProfile).FourLaneDrums;
            OpenDrumsHighwayConfigurationMenu(
                DrumsHighwaySpecs.FOUR_LANE_SPECS,
                colorProvider,
                profile.FourLaneDrumsHighwayOrdering.ToList(),
                "4LaneHeader",
                (newOrdering) => { profile.FourLaneDrumsHighwayOrdering = newOrdering.ToArray(); },
                Instrument.FourLaneDrums,
                profile
            );
        }


        public void OpenProDrumsHighwayConfigurationMenu()
        {
            var profile = GetSelectedProfile();
            var colorProvider = CustomContentManager.ColorProfiles.GetPresetById(profile.ColorProfile).FourLaneDrums;
            OpenDrumsHighwayConfigurationMenu(
                DrumsHighwaySpecs.PRO_DRUMS_SPECS,
                colorProvider,
                profile.ProDrumsHighwayOrdering.ToList(),
                "ProHeader",
                (newOrdering) => { profile.ProDrumsHighwayOrdering = newOrdering.ToArray(); },
                Instrument.ProDrums,
                profile
            );
        }

        public void OpenFiveLaneDrumsHighwayConfigurationMenu()
        {
            var profile = GetSelectedProfile();
            var colorProvider = CustomContentManager.ColorProfiles.GetPresetById(profile.ColorProfile).FiveLaneDrums;
            OpenDrumsHighwayConfigurationMenu(
                DrumsHighwaySpecs.FIVE_LANE_SPECS,
                colorProvider,
                profile.FiveLaneDrumsHighwayOrdering.ToList(),
                "5LaneHeader",
                (newOrdering) => { profile.FiveLaneDrumsHighwayOrdering = newOrdering.ToArray(); },
                Instrument.FiveLaneDrums,
                profile
            );
        }
    }
}