using System;
using System.Collections.Generic;
using Haukcode.sACN;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YARG.Integration.Sacn
{
    public class SacnHardware : MonoSingleton<SacnHardware>
    {
        // DMX spec says 44 updates per second is the max
        private const float TARGET_FPS = 44f;
        private const float TIME_BETWEEN_CALLS = 1f / TARGET_FPS;

        // Each universe supports up to 512 channels
        private const int UNIVERSE_SIZE = 512;

        private const string ACN_SOURCE_NAME = "YARG";

        // A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
        private readonly Guid _acnSourceId = new Guid("{4B454550-504C-4159-494E-475941524721}");

        private SACNClient _sendClient;

        private readonly byte[] _dataPacket = new byte[UNIVERSE_SIZE];

        Queue<byte> _keysQueue = new Queue<byte>();
        Queue<byte> _guitarQueue = new Queue<byte>();
        Queue<byte> _bassQueue = new Queue<byte>();
        Queue<byte> _drumsQueue = new Queue<byte>();

        private bool _toastShown;

        public void HandleEnabledChanged(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_sendClient != null) return;

                var IPAddress = SACNCommon.GetFirstBindAddress().IPAddress;

                if (IPAddress == null)
                {
                    if (!_toastShown)
                    {
                        ToastManager.ToastWarning("No network found! sACN ouput disabled!");
                        _toastShown = true;
                    }

                    YargLogger.LogInfo("Failed to start sACN Hardware Controller (system has no IP address)");
                    return;
                }

                YargLogger.LogInfo("Starting sACN Hardware Controller...");

                SacnInterpreter.OnChannelSet += HandleChannelEvent;


                _sendClient = new SACNClient(senderId: _acnSourceId, senderName: ACN_SOURCE_NAME,
                    localAddress: IPAddress);

                InvokeRepeating(nameof(Sender), 0, TIME_BETWEEN_CALLS);

            }
            else
            {
                KillSacn();
            }
        }

        private void HandleChannelEvent(int channel, byte value)
        {
            //only the instrument channels need to be queued as they are the only ones who end at note off.
            if (channel == SettingsManager.Settings.DMXBassChannel.Value)
            {
                _bassQueue.Enqueue(value);
            }
            else if (channel == SettingsManager.Settings.DMXDrumsChannel.Value)
            {
                _drumsQueue.Enqueue(value);
            }
            else if (channel == SettingsManager.Settings.DMXGuitarChannel.Value)
            {
                _guitarQueue.Enqueue(value);
            }
            else if (channel == SettingsManager.Settings.DMXKeysChannel.Value)
            {
                _keysQueue.Enqueue(value);
            }
            else
            {
                _dataPacket[channel - 1] = value;
            }
        }

        private void KillSacn()
        {
            if (_sendClient == null) return;

            YargLogger.LogInfo("Killing sACN Controller...");

            CancelInvoke(nameof(Sender));

            // Clear the command queue
            _bassQueue.Clear();
            _drumsQueue.Clear();
            _guitarQueue.Clear();
            _keysQueue.Clear();

            // A good controller will also turn everything off after not receiving a packet after 2.5 seconds.
            // But this doesn't hurt to do.
            for (int i = 0; i < _dataPacket.Length; i++)
            {
                //turn everything off directly
                _dataPacket[i] = 0;
            }

            //force send final packet.
            _sendClient.SendMulticast((ushort) SettingsManager.Settings.DMXUniverseChannel.Value, _dataPacket);

            _sendClient.Dispose();
            _sendClient = null;
        }

        private void OnApplicationQuit()
        {
            KillSacn();
        }

        private void Sender()
        {
            if (_bassQueue.Count > 0)
            {
                _dataPacket[SettingsManager.Settings.DMXBassChannel.Value - 1] = _bassQueue.Dequeue();
            }

            if (_drumsQueue.Count > 0)
            {
                _dataPacket[SettingsManager.Settings.DMXDrumsChannel.Value - 1] = _drumsQueue.Dequeue();
            }

            if (_guitarQueue.Count > 0)
            {
                _dataPacket[SettingsManager.Settings.DMXGuitarChannel.Value - 1] = _guitarQueue.Dequeue();
            }

            if (_keysQueue.Count > 0)
            {
                _dataPacket[SettingsManager.Settings.DMXKeysChannel.Value - 1] = _keysQueue.Dequeue();
            }

            //Sacn spec says multicast is the correct default way to go but singlecast can be used if needed.
            _sendClient.SendMulticast((ushort) SettingsManager.Settings.DMXUniverseChannel.Value, _dataPacket);

            //These channels are only on for 1 frame so they need to be turned off after sending.
            _dataPacket[SettingsManager.Settings.DMXKeyframeChannel.Value - 1] = 0;
            _dataPacket[SettingsManager.Settings.DMXBonusEffectChannel.Value - 1] = 0;
            _dataPacket[SettingsManager.Settings.DMXBeatlineChannel.Value - 1] = 0;

        }
    }
}
