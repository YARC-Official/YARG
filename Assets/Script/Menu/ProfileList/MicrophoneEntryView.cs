using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class MicrophoneEntryView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _name;

        private YargProfile _profile;
        private ProfileView _profileView;
        private ProfileCenterPane _profileSidebar;
        private MicDevice _microphone;

        public void Initialize(YargProfile profile, ProfileView profileView, ProfileCenterPane profileSidebar, MicDevice microphone)
        {
            _name.text = microphone.DisplayName;
            _profile = profile;
            _profileView = profileView;
            _profileSidebar = profileSidebar;
            _microphone = microphone;
        }

        public void Remove()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.RemoveMicrophone(_microphone);
            _profileSidebar.UpdateSidebar(_profile, _profileView);
        }
    }
}
