using UnityEngine;
using YARG.Core;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Menu
{
    public class ProfileMenu : MonoBehaviour
    {
        [SerializeField]
        private NavigationGroup _profileNavigationGroup;

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
            _profileNavigationGroup.ClearNavigatables();

            // Spawn in a profile view for each player
            foreach (var profile in PlayerContainer.Profiles)
            {
                var go = Instantiate(_profileViewPrefab, _profileList);
                go.GetComponent<ProfileView>().Init(profile, _profileSidebar);

                _profileNavigationGroup.AddNavigatable(go.GetComponent<NavigatableBehaviour>());
            }
        }

        public void AddProfile()
        {
            PlayerContainer.AddProfile(new YargProfile
            {
                Name = "Nathan",
                NoteSpeed = 8,
                HighwayLength = 1.2f,
                InstrumentType = GameMode.FourLaneDrums,
            });

            RefreshList();
        }

        public void EditProfile()
        {
            MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.EditProfileMenu);
        }
    }
}