﻿using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;

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

        private void OnEnable()
        {
            RefreshList();

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () => MenuManager.Instance.PopMenu()),
            }, true));
        }

        private void OnDisable()
        {
            PlayerContainer.EnsureValidInstruments();

            PlayerContainer.SaveProfiles();
            
            // Update player icons if a profile has changed its GameMode.
            StatsManager.Instance.UpdateActivePlayers();

            Navigator.Instance.PopScheme();
        }

        private void RefreshList()
        {
            // Deselect
            _profileSidebar.HideContents();

            // Remove old ones
            _profileList.transform.DestroyChildren();
            _navigationGroup.ClearNavigatables();

            var activeProfiles = PlayerContainer.Players.Select(e => e.Profile).OrderBy(e => e.Name).ToArray();
            var otherProfiles = PlayerContainer.Profiles.Except(activeProfiles).OrderBy(e => e.Name).ToArray();
            AddListGroup(Localize.Key("Menu.ProfileList.ActiveProfiles"), activeProfiles);
            AddListGroup(Localize.Key("Menu.ProfileList.Players"), otherProfiles.Where(e => !e.IsBot));
            AddListGroup(Localize.Key("Menu.ProfileList.Bots"), otherProfiles.Where(e => e.IsBot));
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

        public void AddProfile()
        {
            PlayerContainer.AddProfile(new YargProfile
            {
                Name = "New Profile",
                NoteSpeed = 5,
                HighwayLength = 1,
                GameMode = GameMode.FiveFretGuitar,
                AutoConnect = false,
            });

            RefreshList();
        }

        public void AddBotProfile()
        {
            PlayerContainer.AddProfile(new YargProfile
            {
                Name = "Bot",
                NoteSpeed = 5,
                HighwayLength = 1,
                GameMode = GameMode.FiveFretGuitar,
                IsBot = true
            });

            RefreshList();
        }
    }
}