using ManagedBass;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    public sealed class BassOutputChannel : OutputChannel
    {
        public readonly BassFlags Flags;

#nullable enable
        internal static BassOutputChannel? Create(int channelId)
#nullable disable
        {
            BassFlags? channel = channelId switch
            {
                // Front speakers, left + right
                1 or 2 => BassFlags.SpeakerPair1,
                // Rear speakers
                3 or 4 => BassFlags.SpeakerPair2,
                // Center and LFE speaker
                5 or 6 => BassFlags.SpeakerPair3,
                // Rear center speakers for 7.1
                7 or 8 => BassFlags.SpeakerPair4,
                9 or 10 => BassFlags.SpeakerPair5,
                11 or 12 => BassFlags.SpeakerPair6,
                13 or 14 => BassFlags.SpeakerPair7,
                15 or 16 => BassFlags.SpeakerPair8,
                17 or 18 => BassFlags.SpeakerPair9,
                19 or 20 => BassFlags.SpeakerPair10,
                21 or 22 => BassFlags.SpeakerPair11,
                23 or 24 => BassFlags.SpeakerPair12,
                25 or 26 => BassFlags.SpeakerPair13,
                27 or 28 => BassFlags.SpeakerPair14,
                29 or 30 => BassFlags.SpeakerPair15,
                // Unknown pair
                _ => null,
            };

            if (channel == null) {
                return null;
            }

            return new BassOutputChannel((BassFlags)channel, channelId);
        }

        private BassOutputChannel(BassFlags flags, int channelId)
            : base(channelId)
        {
            Flags = flags;
        }
    }
}
