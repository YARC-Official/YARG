using System;
using UnityEngine;
using YARG.Core;
using YARG.Player;

namespace YARG.Menu
{
    public class ProfileMenu : MonoBehaviour
    {
        [SerializeField]
        private Transform _profileList;

        [Space]
        [SerializeField]
        private GameObject _profileViewPrefab;

        private void OnEnable()
        {
            RefreshList();
        }

        public void Back()
        {
            MenuNavigator.Instance.PopMenu();
        }

        private void RefreshList()
        {
            // Remove old ones
            foreach (Transform child in _profileList.transform)
            {
                Destroy(child.gameObject);
            }

            // Spawn in a profile view for each player
            foreach (var profile in ProfileContainer.Profiles)
            {
                var go = Instantiate(_profileViewPrefab, _profileList);
                go.GetComponent<ProfileView>().Init(profile);
            }
        }

        public void AddProfile()
        {
            ProfileContainer.AddProfile(new YargProfile
            {
                Name = "Nathan",
                NoteSpeed = 8,
                HighwayLength = 1.2f,
                InstrumentType = GameMode.FourLaneDrums,
            });

            RefreshList();
        }
    }
}