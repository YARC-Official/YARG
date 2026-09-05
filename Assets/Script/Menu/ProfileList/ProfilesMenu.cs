using System;
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
using YARG.Input.Bindings;
using YARG.Input.Serialization;
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
    public class ProfilesMenu : MonoBehaviour
    {
        public const string NUMBER_FORMAT = "0.0###";

        private const string PROFILES_TAB = "profiles";
        private const string BINDINGS_TAB = "bindings";

        private enum ProfileMenuTab
        {
            Profiles,
            Bindings
        }

        private static ProfileMenuTab _currentTab = ProfileMenuTab.Profiles;
        public ControllerFamily CurrentBindingSetFilter = ControllerFamily.FiveFretGuitar;

        [SerializeField]
        private NavigationGroup _navigationGroup;

        [Space]
        [SerializeField]
        private Transform _leftPaneList;
        [SerializeField]
        private BindingSetsFilter _bindingSetsFilter;
        [SerializeField]
        private ProfileCenterPane _profileCenterPane;
        [SerializeField]
        private GameObject _bindingCenterPane;

        [Space]
        [SerializeField]
        private HeaderTabs _headerTabs;
        [SerializeField]
        private GameObject _profileViewPrefab;
        [SerializeField]
        private GameObject _profileListHeaderPrefab;

        private readonly int _maxConnected = HighwayCameraRendering.MAX_MATRICES;

        public bool CanConnectProfile => PlayerContainer.Players.Count < _maxConnected;

        private void OnEnable()
        {
            RefreshProfileList();

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () => MenuManager.Instance.PopMenu(), hide: true),
            }, true));

            _profileCenterPane.gameObject.SetActive(true);
            _bindingCenterPane.SetActive(false);

            _headerTabs.TabChanged += OnTabChanged;

            PlayerContainer.PlayerAdded += OnPlayerAdded;
        }

        private void OnDisable()
        {
            PlayerContainer.EnsureValidInstruments();
            PlayerContainer.SaveProfiles();

            // Update player icons if a profile has changed its GameMode.
            // Persistent singletons may already be destroyed when Unity exits play mode.
            StatsManager.Instance?.UpdateActivePlayers();

            Navigator.Instance?.PopScheme();

            PlayerContainer.PlayerAdded -= OnPlayerAdded;
        }

        public void RefreshBindingSetList()
        {
            if (_currentTab is not ProfileMenuTab.Bindings)
            {
                return;
            }

            // Deselect
            _profileCenterPane.HideContents();

            // Remove old ones
            _leftPaneList.transform.DestroyChildren();
            _navigationGroup.ClearNavigatables();
        }

        public void RefreshProfileList(YargProfile selectedProfile = null)
        {
            if (_currentTab is not ProfileMenuTab.Profiles)
            {
                return;
            }

            // Deselect
            _profileCenterPane.HideContents();

            // Remove old ones
            _leftPaneList.transform.DestroyChildren();
            _navigationGroup.ClearNavigatables();

            var activeProfiles = PlayerContainer.Players.Select(e => e.Profile).ToArray();
            var otherProfiles = PlayerContainer.Profiles.Except(activeProfiles).OrderBy(e => e.Name).ToArray();

            AddProfileListGroup(Localize.Key("Menu.ProfileList.ActiveProfiles"), activeProfiles);
            AddProfileListGroup(Localize.Key("Menu.ProfileList.Players"), otherProfiles.Where(e => !e.IsBot));
            AddProfileListGroup(Localize.Key("Menu.ProfileList.Bots"), otherProfiles.Where(e => e.IsBot));
            AddUnloadedGroup(Localize.Key("Menu.ProfileList.CouldNotLoad"));

            if (selectedProfile == null)
            {
                return;
            }

            SetSelectedProfile(selectedProfile);
        }

        private void AddProfileListGroup(string header, IEnumerable<YargProfile> profiles)
        {
            if (!profiles.Any())
            {
                return;
            }

            AddListHeader(header);

            // Spawn in a profile view for each player
            foreach (var profile in profiles)
            {
                var go = Instantiate(_profileViewPrefab, _leftPaneList);
                go.GetComponent<ProfileView>().Init(this, profile, _profileCenterPane);
                _navigationGroup.AddNavigatable(go);
            }
        }

        private void AddListHeader(string header)
        {
            var headerGo = Instantiate(_profileListHeaderPrefab, _leftPaneList);
            headerGo.GetComponentInChildren<TextMeshProUGUI>().text = header;
            _navigationGroup.AddNavigatable(headerGo);
        }

        private void AddUnloadedGroup(string header)
        {
            if (PlayerContainer.UnloadedProfiles.Count == 0)
            {
                return;
            }

            var headerGo = Instantiate(_profileListHeaderPrefab, _leftPaneList);
            headerGo.GetComponentInChildren<TextMeshProUGUI>().text = header;
            _navigationGroup.AddNavigatable(headerGo);

            foreach (var record in PlayerContainer.UnloadedProfiles)
            {
                var go = Instantiate(_profileViewPrefab, _leftPaneList);
                go.GetComponent<ProfileView>().InitUnloaded(this, record, _profileCenterPane);
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

            RefreshProfileList();
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

            RefreshProfileList();
        }

        public void MoveProfileUp(YargProfile profile)
        {
            PlayerContainer.MoveUp(PlayerContainer.GetPlayerFromProfile(profile));
            RefreshProfileList(profile);
        }

        public void MoveProfileDown(YargProfile profile)
        {
            PlayerContainer.MoveDown(PlayerContainer.GetPlayerFromProfile(profile));
            RefreshProfileList(profile);
        }

        #nullable enable
        private YargProfile? GetSelectedProfile()
        #nullable disable
        {
            var profileView = _leftPaneList.GetComponentsInChildren<ProfileView>()
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
            var profileView = _leftPaneList.GetComponentsInChildren<ProfileView>()
                .LastOrDefault(e => e.Profile == profile);
            if (profileView != null)
            {
                profileView.SetSelected(true, SelectionOrigin.Programmatically);
            }
        }

        public void OnPlayerAdded(YargPlayer player)
        {
            RefreshProfileList(GetSelectedProfile());
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

        private void OnTabChanged(string tabId)
        {
            _profileCenterPane.gameObject.SetActive(tabId == PROFILES_TAB);
            _bindingCenterPane.SetActive(tabId == BINDINGS_TAB);
            _bindingSetsFilter.gameObject.SetActive(tabId == BINDINGS_TAB);

            switch (tabId)
            {
                case PROFILES_TAB:
                    _currentTab = ProfileMenuTab.Profiles;
                    RefreshProfileList();
                    break;
                case BINDINGS_TAB:
                    _currentTab = ProfileMenuTab.Bindings;
                    RefreshBindingSetList();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unexpected tabId {tabId}");
            };

            
        }
    }
}
