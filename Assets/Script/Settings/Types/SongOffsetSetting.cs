using System;
using YARG.Song;

namespace YARG.Settings.Types
{
    /// <summary>
    /// An <see cref="IntSetting"/> backed by the current song's entry in <see cref="SongOffsetContainer"/>
    /// rather than the global settings file. Used to edit a single song's specific offset (in
    /// milliseconds) from the pause menu.
    /// </summary>
    public class SongOffsetSetting : IntSetting
    {
        private readonly string _songHashKey;

        public SongOffsetSetting(string songHashKey, Action<int> onChange, int min = -5000, int max = 5000)
            : base((int) SongOffsetContainer.GetOffsetMilliseconds(songHashKey), min, max, onChange)
        {
            _songHashKey = songHashKey;
        }

        protected override void SetValue(int value)
        {
            base.SetValue(value);
            SongOffsetContainer.SetOffsetMilliseconds(_songHashKey, _value);
        }
    }
}
