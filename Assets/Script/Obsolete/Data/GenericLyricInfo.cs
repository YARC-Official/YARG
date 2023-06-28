using System.Collections.Generic;

namespace YARG.Data
{
    public class GenericLyricInfo : AbstractInfo
    {
        public List<(float time, string word)> lyric;

        public GenericLyricInfo()
        {
            lyric = new();
        }

        public GenericLyricInfo(float time, float length, List<(float, string)> lyric)
        {
            this.time = time;
            this.length = length;
            this.lyric = lyric;
        }
    }
}