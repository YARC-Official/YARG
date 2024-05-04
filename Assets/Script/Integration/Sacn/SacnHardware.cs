using System;
using Haukcode.sACN;
using UnityEngine;
using YARG.Core.Logging;

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

        public static event Action OnPacketSent;

        public void HandleEnabledChanged(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_sendClient != null) return;

                YargLogger.LogInfo("Starting sACN Hardware Controller...");
                SacnInterpreter.OnChannelSet += HandleChannelEvent;

                _sendClient = new SACNClient(senderId: _acnSourceId, senderName: ACN_SOURCE_NAME,
                    localAddress: SACNCommon.GetFirstBindAddress().IPAddress);

                InvokeRepeating(nameof(Sender), 0, TIME_BETWEEN_CALLS);
            }
            else
            {
                KillSacn();
            }
        }

        private void HandleChannelEvent(int channel, byte value)
        {
            _dataPacket[channel - 1] = value;
        }

        private void KillSacn()
        {
            if (_sendClient == null) return;

            YargLogger.LogInfo("Killing sACN Controller...");

            // A good controller will also turn everything off after not receiving a packet after 2.5 seconds.
            // But this doesn't hurt to do.
            for (int i = 0; i < _dataPacket.Length; i++)
            {
                _dataPacket[i] = 0; //turn everything off
            }

            CancelInvoke(nameof(Sender));

            //force send final packet.
            Sender();

            _sendClient.Dispose();
            _sendClient = null;
        }

        private void OnApplicationQuit()
        {
            KillSacn();
        }

        private void Sender()
        {
            // Hardcoded to universe 1, as this is for non-professional use, I doubt anyone is running multiple universes.
            // Didn't want to confuse the user with settings for something they don't need. However, it's a simple change
            // if needed. Same goes for sending multicast vs singlecast. Sacn spec says multicast is the correct default
            // way to go but singlecast can be used if needed.
            _sendClient.SendMulticast(1, _dataPacket);

            //this is mainly for the sacn interpreter to know when a packet is sent so it can turn off notes that are no
            //longer being played.
            OnPacketSent?.Invoke();
        }
    }
}

/*
    "If you ever drop your keys into a river of molten lava, let'em go...because man, they're gone.'

    - Jack Handey.
*/
