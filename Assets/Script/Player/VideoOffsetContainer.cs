using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Game;
using YARG.Core.Logging;

namespace YARG.Player
{
    /// <summary>
    /// Stores each profile's personal video offset (see
    /// <see cref="YARG.Gameplay.Player.BasePlayer.VideoOffsetSeconds"/>).
    /// <br/><br/>
    /// This lives outside of <see cref="YargProfile"/> (and therefore YARG.Core) since it's a
    /// purely cosmetic, client-side rendering preference with no bearing on scoring, engine
    /// behavior, or replay determinism. It follows the same profile-ID-keyed sidecar file
    /// pattern already used for bindings (<see cref="YARG.Input.Bindings.BindingsContainer"/>),
    /// just without that system's extra versioning/backup machinery, since there's nothing here
    /// to version beyond a single number per profile.
    /// </summary>
    public static class VideoOffsetContainer
    {
        private static string OffsetsPath => Path.Combine(PlayerContainer.ProfilesDirectory, "video-offsets.json");

        private static readonly Dictionary<Guid, long> _offsetsMilliseconds = new();

        /// <summary>
        /// This profile's personal video offset, in seconds. Positive values make notes appear
        /// on this profile's highway earlier than the shared song timeline would otherwise show;
        /// see <see cref="YARG.Gameplay.Player.BasePlayer.VisualTime"/>.
        /// </summary>
        public static double GetOffsetSeconds(YargProfile profile)
        {
            return GetOffsetMilliseconds(profile) / 1000.0;
        }

        public static long GetOffsetMilliseconds(YargProfile profile)
        {
            return _offsetsMilliseconds.TryGetValue(profile.Id, out var milliseconds) ? milliseconds : 0;
        }

        public static void SetOffsetMilliseconds(YargProfile profile, long milliseconds)
        {
            if (milliseconds == 0)
            {
                // Don't bother persisting a no-op entry
                _offsetsMilliseconds.Remove(profile.Id);
                return;
            }

            _offsetsMilliseconds[profile.Id] = milliseconds;
        }

        /// <summary>
        /// Loads all stored video offsets. Safe to call even if no offsets have ever been saved.
        /// </summary>
        public static void LoadOffsets()
        {
            _offsetsMilliseconds.Clear();

            string path = OffsetsPath;
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var loaded = JsonConvert.DeserializeObject<Dictionary<Guid, long>>(File.ReadAllText(path));
                if (loaded is null)
                {
                    return;
                }

                foreach (var (id, milliseconds) in loaded)
                {
                    // Silently drop entries for profiles that no longer exist, same as bindings
                    if (PlayerContainer.GetProfileById(id) is not null)
                    {
                        _offsetsMilliseconds[id] = milliseconds;
                    }
                }
            }
            catch (Exception e)
            {
                YargLogger.LogFormatWarning("Failed to load video offsets: {0}", e.Message);
            }
        }

        public static void SaveOffsets()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_offsetsMilliseconds, Formatting.Indented);
                File.WriteAllText(OffsetsPath, json);
            }
            catch (Exception e)
            {
                YargLogger.LogFormatWarning("Failed to save video offsets: {0}", e.Message);
            }
        }
    }
}
