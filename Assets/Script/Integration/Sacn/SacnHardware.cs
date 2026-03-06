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
        private float TIME_BETWEEN_CALLS => 1f / SettingsManager.Settings.DMXTargetFPS.Value;
        private float PulseDuration => SettingsManager.Settings.DMXPulseDuration.Value / 1000f;

        // Each universe supports up to 512 channels
        private const int UNIVERSE_SIZE = 512;

        private const string ACN_SOURCE_NAME = "YARG";

        // A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
        private readonly Guid _acnSourceId = new("{4B454550-504C-4159-494E-475941524721}");

        private SACNClient _sendClient;

        private readonly byte[]                 _dataPacket      = new byte[UNIVERSE_SIZE];
        private readonly Dictionary<int, float> _channelOffTimes = new();
        private readonly List<int>              _expiredChannels = new();

        private float _timer;

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

                _timer = 0f;
            }
            else
            {
                KillSacn();
            }
        }

        private void Update()
        {
            if (_sendClient == null) return;

            _timer += Time.deltaTime;
            if (_timer >= TIME_BETWEEN_CALLS)
            {
                _timer -= TIME_BETWEEN_CALLS;
                Sender();
            }
        }

        private void HandleChannelEvent(int channel, byte value)
        {
            int bass = SettingsManager.Settings.DMXBassChannel.Value;
            int drums = SettingsManager.Settings.DMXDrumsChannel.Value;
            int guitar = SettingsManager.Settings.DMXGuitarChannel.Value;
            int keys = SettingsManager.Settings.DMXKeysChannel.Value;

            if (channel == bass || channel == drums || channel == guitar || channel == keys)
            {
                if (value <= 0) return;
                _dataPacket[channel - 1] = value;
                if (PulseDuration > 0f)
                    _channelOffTimes[channel] = Time.time + PulseDuration;
                else
                    _channelOffTimes[channel] = Time.time + TIME_BETWEEN_CALLS;
            }
            else
            {
                _dataPacket[channel - 1] = value;
                if (value <= 0) return;

                int keyframe = SettingsManager.Settings.DMXKeyframeChannel.Value;
                int bonusEffect = SettingsManager.Settings.DMXBonusEffectChannel.Value;
                int beatline = SettingsManager.Settings.DMXBeatlineChannel.Value;

                if (channel != keyframe && channel != bonusEffect && channel != beatline) return;

                if (PulseDuration > 0f) _channelOffTimes[channel] = Time.time + PulseDuration;
            }
        }

        private void KillSacn()
        {
            if (_sendClient == null) return;

            YargLogger.LogInfo("Killing sACN Controller...");

            SacnInterpreter.OnChannelSet -= HandleChannelEvent;

            // Clear the command queue
            _channelOffTimes.Clear();

            // A good controller will also turn everything off after not receiving a packet after 2.5 seconds.
            // But this doesn't hurt to do.
            for (int i = 0; i < _dataPacket.Length; i++)
            {
                _dataPacket[i] = 0;
            }

            // Force send final packet.
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
            float pulseDuration = PulseDuration;

            _expiredChannels.Clear();
            foreach (var kvp in _channelOffTimes)
            {
                if (Time.time >= kvp.Value)
                {
                    _dataPacket[kvp.Key - 1] = 0;
                    _expiredChannels.Add(kvp.Key);
                }
            }
            foreach (var ch in _expiredChannels) _channelOffTimes.Remove(ch);

            _sendClient.SendMulticast((ushort) SettingsManager.Settings.DMXUniverseChannel.Value, _dataPacket);

            if (pulseDuration <= 0f)
            {
                _dataPacket[SettingsManager.Settings.DMXKeyframeChannel.Value - 1] = 0;
                _dataPacket[SettingsManager.Settings.DMXBonusEffectChannel.Value - 1] = 0;
                _dataPacket[SettingsManager.Settings.DMXBeatlineChannel.Value - 1] = 0;
            }
        }
    }
}