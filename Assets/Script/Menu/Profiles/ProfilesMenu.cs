using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
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

        [Space]
        [SerializeField]
        private InputDeviceDialogMenu _deviceDialog;

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

        public UniTask<InputDevice> ShowDeviceDialog()
        {
            return _deviceDialog.Show();
        }
    }
}