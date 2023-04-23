// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public abstract class SyncTrack : SongObject
    {
        public SyncTrack(uint _position) : base(_position) { }
    }
}
