using System;
using Haukcode.sACN;
using PlasticBand.Haptics;
using UnityEngine;
using YARG.Integration.StageKit;
using YARG.Settings;

namespace YARG.Integration.Sacn
{

    public class SacnController : MonoSingleton<SacnController>
    {
        //DMX spec says 44 updates per second is the max
        private const float TARGET_FPS = 44f;
        private const float TIME_BETWEEN_CALLS = 1f / TARGET_FPS;

        //Each universe supports up to 512 channels
        private const int UNIVERSE_SIZE = 512;

        private const string ACN_SOURCE_NAME = "YARG";

        private byte[] _dataPacket = new byte[UNIVERSE_SIZE];
        //A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
        private readonly Guid AcnSourceId = new Guid("{4B454550-504C-4159-494E-475941524721}");

        private SACNClient _sendClient;

        //DMX channels - 8 per color to match the stageKit layout. Default channels, the user must change them in settings.
        private int[] _dimmerChannels;
        private int[] _redChannels;
        private int[] _greenChannels;
        private int[] _blueChannels;
        private int[] _yellowChannels;

        public void HandleEnabledChanged(bool enabled)
        {
            if (enabled)
            {
                Debug.Log("Starting SacnController...");

                StageKitLightingController.Instance.OnLedSet += HandleEvent;

                UpdateDMXChannels();

                _sendClient = new SACNClient(senderId: AcnSourceId, senderName: ACN_SOURCE_NAME,
                    localAddress: SACNCommon.GetFirstBindAddress().IPAddress);

                InvokeRepeating(nameof(Sender), 0, TIME_BETWEEN_CALLS);

                //Many DMX fixtures have a 'Master dimmer' channel that controls the overall brightness of the fixture.
                //Got to turn those on.
                for (int i = 0; i < _dimmerChannels?.Length; i++)
                {
                    _dataPacket[_dimmerChannels[i] - 1] = 255;
                }
            }
            else
            {
                OnDestroy();
            }
        }

        public void UpdateDMXChannels()
        {
            _dimmerChannels = SettingsManager.Settings?.DMXDimmerChannels.Value;
            _redChannels = SettingsManager.Settings?.DMXRedChannels.Value;
            _greenChannels = SettingsManager.Settings?.DMXGreenChannels.Value;
            _blueChannels = SettingsManager.Settings?.DMXBlueChannels.Value;
            _yellowChannels = SettingsManager.Settings?.DMXYellowChannels.Value;
        }

        private void OnDestroy()
        {
            if (_sendClient == null) return;

            Debug.Log("Killing SacnController...");

            // A good controller will also turn everything off after not receiving a packet after 2.5 seconds.
            // But this doesn't hurt to do.
            for (int i = 0; i < _dataPacket.Length; i++)
            {
                _dataPacket[i] = 0; //turn everything off
            }

            //force send final packet.
            Sender();

            _sendClient.Dispose();

            CancelInvoke(nameof(Sender));

            StageKitLightingController.Instance.OnLedSet -= HandleEvent;
        }

        private void HandleEvent(StageKitLedColor color, StageKitLed value)
        {
            bool[] ledIsSet = new bool[8];

            // Set the values of ledIsSet based on the StageKitLed enum
            for (int i = 0; i < 8; i++)
            {
                ledIsSet[i] = (value & (StageKitLed) (1 << i)) != 0;
            }

            // Handle the event based on color
            switch (color)
            {
                case StageKitLedColor.Red:
                    SetChannelValues(_redChannels, ledIsSet);
                    break;

                case StageKitLedColor.Blue:
                    SetChannelValues(_blueChannels, ledIsSet);
                    break;

                case StageKitLedColor.Green:
                    SetChannelValues(_greenChannels, ledIsSet);
                    break;

                case StageKitLedColor.Yellow:
                    SetChannelValues(_yellowChannels, ledIsSet);
                    break;

                case StageKitLedColor.None:
                    // I'm not sure this is ever used, anywhere?
                    break;

                case StageKitLedColor.All:
                    SetChannelValues(_yellowChannels, ledIsSet);
                    SetChannelValues(_greenChannels, ledIsSet);
                    SetChannelValues(_blueChannels, ledIsSet);
                    SetChannelValues(_redChannels, ledIsSet);
                    break;

                default:
                    Debug.LogWarning("(Sacn) Unknown color: " + color);
                    break;
            }
        }

        private void SetChannelValues(int[] channels, bool[] ledIsSet)
        {
            for (int i = 0; i < 8; i++)
            {
                _dataPacket[channels[i] - 1] = ledIsSet[i] ? (byte) 255 : (byte) 0;
            }
        }

        private void Sender()
        {
            //Hardcoded to universe 1, as this is for non-professional use, I doubt anyone is running multiple universes.
            //Didn't want to confuse the user with settings for something they don't need. However, it's a simple change if needed.
            //Same goes for sending multicast vs singlecast. Sacn spec says multicast is the correct default way to go but
            //singlecast can be used if needed.
            _sendClient.SendMulticast(1, _dataPacket);
        }
    }
}