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

        private const int MAX_INSTRUMENT_QUEUE_SIZE = 3;

        // A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
        private readonly Guid _acnSourceId = new("{4B454550-504C-4159-494E-475941524721}");

        private SACNClient _sendClient;

        private readonly byte[]                 _dataPacket      = new byte[UNIVERSE_SIZE];
        private readonly Dictionary<int, float> _channelOffTimes = new();
        private readonly List<int>              _expiredChannels = new();

        private float _timer;

        private Queue<byte> _keysQueue   = new();
        private Queue<byte> _guitarQueue = new();
        private Queue<byte> _bassQueue   = new();
        private Queue<byte> _drumsQueue  = new();

        private float _drumsLastEventTime  = -1f;
        private float _guitarLastEventTime = -1f;
        private float _bassLastEventTime   = -1f;
        private float _keysLastEventTime   = -1f;

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

        private void EnqueueInstrument(Queue<byte> queue, ref float lastEventTime, byte value)
        {
            float now = Time.time;
            if (lastEventTime < 0f || now - lastEventTime > 0.005f)
            {
                queue.Enqueue(value);
                lastEventTime = now;
            }
        }

        private void HandleChannelEvent(int channel, byte value)
        {
                // Only instrument channels need to be queued as they are the only ones who end at note off.
                if (channel == SettingsManager.Settings.DMXBassChannel.Value)
                    EnqueueInstrument(_bassQueue, ref _bassLastEventTime, value);
                else if (channel == SettingsManager.Settings.DMXDrumsChannel.Value)
                    EnqueueInstrument(_drumsQueue, ref _drumsLastEventTime, value);
                else if (channel == SettingsManager.Settings.DMXGuitarChannel.Value)
                    EnqueueInstrument(_guitarQueue, ref _guitarLastEventTime, value);
                else if (channel == SettingsManager.Settings.DMXKeysChannel.Value)
                    EnqueueInstrument(_keysQueue, ref _keysLastEventTime, value);
                else
                {
                    _dataPacket[channel - 1] = value;
                    if (value <= 0)
                    {
                        return;
                    }

                    int keyframe = SettingsManager.Settings.DMXKeyframeChannel.Value;
                    int bonusEffect = SettingsManager.Settings.DMXBonusEffectChannel.Value;
                    int beatline = SettingsManager.Settings.DMXBeatlineChannel.Value;

                    if (channel != keyframe && channel != bonusEffect && channel != beatline)
                    {
                        return;
                    }

                    if (PulseDuration > 0f) _channelOffTimes[channel] = Time.time + PulseDuration;
                }
        }

        private void KillSacn()
        {
            if (_sendClient == null) return;

            YargLogger.LogInfo("Killing sACN Controller...");

            SacnInterpreter.OnChannelSet -= HandleChannelEvent;

            // Clear the command queue
            _bassQueue.Clear();
            _drumsQueue.Clear();
            _guitarQueue.Clear();
            _keysQueue.Clear();
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

                // Turn off channels whose pulse has expired
                if (pulseDuration <= 0f)
                {
                    _channelOffTimes.Clear();
                }
                else
                {
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
                }

                if (_bassQueue.Count > 0)
                {
                    int ch = SettingsManager.Settings.DMXBassChannel.Value;
                    while (_bassQueue.Count > MAX_INSTRUMENT_QUEUE_SIZE) _bassQueue.Dequeue();
                    _dataPacket[ch - 1] = _bassQueue.Dequeue();
                    if (pulseDuration > 0f) _channelOffTimes[ch] = Time.time + pulseDuration;
                }

                if (_drumsQueue.Count > 0)
                {
                    int ch = SettingsManager.Settings.DMXDrumsChannel.Value;
                    while (_drumsQueue.Count > MAX_INSTRUMENT_QUEUE_SIZE) _drumsQueue.Dequeue();
                    _dataPacket[ch - 1] = _drumsQueue.Dequeue();
                    if (pulseDuration > 0f) _channelOffTimes[ch] = Time.time + pulseDuration;
                }

                if (_guitarQueue.Count > 0)
                {
                    int ch = SettingsManager.Settings.DMXGuitarChannel.Value;
                    while (_guitarQueue.Count > MAX_INSTRUMENT_QUEUE_SIZE) _guitarQueue.Dequeue();
                    _dataPacket[ch - 1] = _guitarQueue.Dequeue();
                    if (pulseDuration > 0f) _channelOffTimes[ch] = Time.time + pulseDuration;
                }

                if (_keysQueue.Count > 0)
                {
                    int ch = SettingsManager.Settings.DMXKeysChannel.Value;
                    while (_keysQueue.Count > MAX_INSTRUMENT_QUEUE_SIZE) _keysQueue.Dequeue();
                    _dataPacket[ch - 1] = _keysQueue.Dequeue();
                    if (PulseDuration > 0f) _channelOffTimes[ch] = Time.time + PulseDuration;
                }

                // Sacn spec says multicast is the correct default way to go but singlecast can be used if needed.
                _sendClient.SendMulticast((ushort) SettingsManager.Settings.DMXUniverseChannel.Value, _dataPacket);

                if (PulseDuration <= 0f)
                {
                    _dataPacket[SettingsManager.Settings.DMXKeyframeChannel.Value - 1] = 0;
                    _dataPacket[SettingsManager.Settings.DMXBonusEffectChannel.Value - 1] = 0;
                    _dataPacket[SettingsManager.Settings.DMXBeatlineChannel.Value - 1] = 0;
                }
        }
    }
}