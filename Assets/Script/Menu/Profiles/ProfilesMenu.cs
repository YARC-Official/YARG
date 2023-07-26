using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Menu.Profiles
{
    public class ProfilesMenu : MonoBehaviour
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

        private void OnEnable()
        {
            RefreshList();
        }

        private void OnDisable()
        {
            PlayerContainer.SaveProfiles();
        }

        // TODO: Move the ProfileContainer
        private void OnApplicationQuit()
        {
            PlayerContainer.SaveProfiles();
        }

        private void RefreshList()
        {
            // Deselect
            _profileSidebar.HideContents();

            // Remove old ones
            _profileList.transform.DestroyChildren();
            _navigationGroup.ClearNavigatables();

            // Spawn in a profile view for each player
            foreach (var profile in PlayerContainer.Profiles)
            {
                var go = Instantiate(_profileViewPrefab, _profileList);
                go.GetComponent<ProfileView>().Init(profile, _profileSidebar);

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
            });

            RefreshList();
        }
    }
}