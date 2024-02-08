using Haukcode.sACN;
using System;
using PlasticBand.Haptics;
using UnityEngine;
using YARG.Integration.StageKit;
using YARG.Settings;

public class SacnController : MonoBehaviour
{
    //A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
    private static readonly Guid acnSourceId = new Guid("{4B454550-504C-4159-494E-475941524721}");
    private static readonly string acnSourceName = "YARG";
    private static readonly int universeSize = 512;
    private static SACNClient sendClient;
    private const float TargetFPS = 44f; //DMX spec says 44 updates per second is the max
    private float timeBetweenCalls = 1f / TargetFPS;
    private byte[] _dataPacket = new byte[universeSize];
    //DMX channels - 8 per color to match the stageKit layout. Default channels, the user must change them in settings.
    private static int[] dimmerChannels;
    private static int[] redChannels;
    private static int[] greenChannels;
    private static int[] blueChannels;
    private static int[] yellowChannels;

    void Start()
    {
        Debug.Log("Starting SacnController...");

        SettingsManager.SettingContainer.OnDMXChannelsChanged += HandleValueChanged;

        HandleValueChanged(null);

        StageKitLightingController.Instance.OnLedSet += HandleEvent;

        sendClient = new SACNClient(senderId: acnSourceId, senderName: acnSourceName,
            localAddress: SACNCommon.GetFirstBindAddress().IPAddress);

        InvokeRepeating(nameof(Sender), 0, timeBetweenCalls);

        //Many dmx fixtures have a 'Master dimmer' channel that controls the overall brightness of the fixture.
        //Got to turn those on.
        for (int i = 0; i < dimmerChannels.Length; i++)
        {
            _dataPacket[dimmerChannels[i]] = 255;
        }
    }

    private void HandleValueChanged(int[] value)
    {
        dimmerChannels = SettingsManager.Settings.DimmerChannels.Value;
        redChannels = SettingsManager.Settings.RedChannels.Value;
        greenChannels = SettingsManager.Settings.GreenChannels.Value;
        blueChannels = SettingsManager.Settings.BlueChannels.Value;
        yellowChannels = SettingsManager.Settings.YellowChannels.Value;
    }

    private void OnDestroy()
    {
        // A good controller will also turn everything off after not receiving a packet after 2.5 seconds.
        // But this doesn't hurt to do.
        for (int i = 0; i < _dataPacket.Length; i++)
        {
            _dataPacket[i] = 0; //turn everything off
        }

        //send final packet.
        Sender();
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
                SetChannelValues(redChannels, ledIsSet);
                break;

            case StageKitLedColor.Blue:
                SetChannelValues(blueChannels, ledIsSet);
                break;

            case StageKitLedColor.Green:
                SetChannelValues(greenChannels, ledIsSet);
                break;

            case StageKitLedColor.Yellow:
                SetChannelValues(yellowChannels, ledIsSet);
                break;

            case StageKitLedColor.None:
                // I'm not sure this is ever used, anywhere?
                break;

            case StageKitLedColor.All:
                SetChannelValues(yellowChannels, ledIsSet);
                SetChannelValues(greenChannels, ledIsSet);
                SetChannelValues(blueChannels, ledIsSet);
                SetChannelValues(redChannels, ledIsSet);
                break;

            default:
                Debug.Log("(Sacn) Unknown color: " + color);
                break;
        }
    }

    private void SetChannelValues(int[] channels, bool[] ledIsSet)
    {
        for (int i = 0; i < 8; i++)
        {
            _dataPacket[channels[i]] = ledIsSet[i] ? (byte) 255 : (byte) 0;
        }
    }

    private void Sender()
    {
        //Hardcoded to universe 1, as this is for non-professional use, I doubt anyone is running multiple universes.
        //Didn't want to confuse the user with settings for something they don't need. However, it's a simple change if needed.
        sendClient.SendMulticast(1, _dataPacket);
    }
}