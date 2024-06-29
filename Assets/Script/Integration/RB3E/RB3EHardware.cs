using System;
using System.Net;
using System.Net.Sockets;
using PlasticBand.Haptics;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Integration.StageKit;
using YARG.Settings;

namespace YARG.Integration.RB3E
{
    public class RB3EHardware : MonoSingleton<RB3EHardware>
    {
        private enum CommandID : byte
        {
            FogOn = 0x01,
            FogOff = 0x02,

            StrobeSlow = 0x03,
            StrobeMedium = 0x04,
            StrobeFast = 0x05,
            StrobeFastest = 0x06,
            StrobeOff = 0x07,

            BlueLeds = 0x20,
            GreenLeds = 0x40,
            YellowLeds = 0x60,
            RedLeds = 0x80,

            DisableAll = 0xFF
        }

        public IPAddress IPAddress = IPAddress.Parse("255.255.255.255"); // "this" network's broadcast address
        private const int PORT = 21070;                                  // That is what RB3E uses
        private UdpClient _sendClient;

        private void OnApplicationQuit()
        {
            KillRB3E();
        }

        public void HandleEnabledChanged(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_sendClient != null) return;

                YargLogger.LogInfo("Starting RB3E lighting hardware...");

                MasterLightingController.OnFogState += OnFogStateEvent;
                MasterLightingController.OnStrobeEvent += OnStrobeEvent;
                StageKitInterpreter.OnLedEvent += HandleLedEvent;
                //Bonus Effects are ignored, since the stage kit doesn't seem to do anything with them.

                _sendClient = new UdpClient();
            }
            else
            {
                KillRB3E();
            }
        }

        private void KillRB3E()
        {
            if (_sendClient == null) return;

            YargLogger.LogInfo("Killing RB3E lighting hardware...");

            MasterLightingController.OnFogState -= OnFogStateEvent;
            MasterLightingController.OnStrobeEvent -= OnStrobeEvent;
            StageKitInterpreter.OnLedEvent -= HandleLedEvent;

            SendPacket((byte) CommandID.DisableAll, 0x00);

            _sendClient.Dispose();
        }

        private void HandleLedEvent(StageKitLedColor color, byte led)
        {
            if ((color & StageKitLedColor.Blue) != 0)
            {
                SendPacket((byte) CommandID.BlueLeds, led);
            }

            if ((color & StageKitLedColor.Green) != 0)
            {
                SendPacket((byte) CommandID.GreenLeds, led);
            }

            if ((color & StageKitLedColor.Red) != 0)
            {
                SendPacket((byte) CommandID.RedLeds, led);
            }

            if ((color & StageKitLedColor.Yellow) != 0)
            {
                SendPacket((byte) CommandID.YellowLeds, led);
            }
        }

        private void OnFogStateEvent(MasterLightingController.FogState fogState)
        {
            if (fogState == MasterLightingController.FogState.On)
            {
                SendPacket((byte) CommandID.FogOn, 0x00);
            }
            else
            {
                SendPacket((byte) CommandID.FogOff, 0x00);
            }
        }

        private void OnStrobeEvent(StageKitStrobeSpeed value)
        {
            switch (value)
            {
                case StageKitStrobeSpeed.Off:
                    SendPacket((byte) CommandID.StrobeOff, 0x00);
                    break;
                case StageKitStrobeSpeed.Slow:
                    SendPacket((byte) CommandID.StrobeSlow, 0x00);
                    break;
                case StageKitStrobeSpeed.Medium:
                    SendPacket((byte) CommandID.StrobeMedium, 0x00);
                    break;
                case StageKitStrobeSpeed.Fast:
                    SendPacket((byte) CommandID.StrobeFast, 0x00);
                    break;
                case StageKitStrobeSpeed.Fastest:
                    SendPacket((byte) CommandID.StrobeFastest, 0x00);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        private void SendPacket(byte commandID, byte parameter)
        {
            byte[] packetData = new byte[]
            {
                0x52, 0x42, 0x33, 0x45, // Magic
                0x00,                   // Version
                0x06,                   // Packet type (RB3E_EVENT_STAGEKIT)
                0x02,                   // Packet size
                0x80,                   // Platform (RB3E_PLATFORM_YARG)
                parameter,              // Left stagekit channel, parameter ID
                commandID               // Right stagekit channel, command ID
            };

            _sendClient.Send(packetData, packetData.Length, IPAddress.ToString(), PORT);
        }
    }
}
