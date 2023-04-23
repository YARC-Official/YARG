// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public abstract class ChartObject : SongObject
    {
        [System.NonSerialized]
        public MoonChart moonChart;

        public ChartObject(uint position) : base(position) { }
    }
}
