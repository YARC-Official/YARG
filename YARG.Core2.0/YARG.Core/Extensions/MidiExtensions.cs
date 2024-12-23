using Melanchall.DryWetMidi.Core;

namespace YARG.Core.Extensions
{
    public static class MidiExtensions
    {
        public static void Merge(this MidiFile targetFile, MidiFile sourceFile)
        {
            // Index 1 to skip the sync track
            for (int sourceIndex = 1; sourceIndex < sourceFile.Chunks.Count; sourceIndex++)
            {
                if (sourceFile.Chunks[sourceIndex] is not TrackChunk sourceTrack)
                    continue;

                // Add immediately if the track has no name,
                // no reasonable way to compare if so
                string sourceName = sourceTrack.GetTrackName();
                if (string.IsNullOrEmpty(sourceName))
                {
                    targetFile.Chunks.Add(sourceTrack);
                    continue;
                }

                // Replace any existing tracks first
                bool isExisting = false;
                for (int targetIndex = 1; targetIndex < targetFile.Chunks.Count; targetIndex++)
                {
                    if (targetFile.Chunks[targetIndex] is not TrackChunk targetTrack)
                        continue;

                    if (sourceName == targetTrack.GetTrackName())
                    {
                        targetFile.Chunks[targetIndex] = sourceTrack;
                        isExisting = true;
                        break;
                    }
                }

                // Otherwise, add it
                if (!isExisting)
                    targetFile.Chunks.Add(sourceTrack);
            }
        }

        public static string GetTrackName(this TrackChunk track)
        {
            // The first event is not always the track name,
            // so we need to search through everything at tick 0
            for (int i = 0; i < track.Events.Count; i++)
            {
                var midiEvent = track.Events[i];

                // Search until the first event that's not at position 0,
                // indicated by the first non-zero delta-time
                if (midiEvent.DeltaTime != 0)
                    break;

                if (midiEvent.EventType == MidiEventType.SequenceTrackName &&
                    midiEvent is SequenceTrackNameEvent trackName)
                    return trackName.Text;
            }

            return "";
        }
    }
}