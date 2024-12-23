// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MoonscraperChartEditor.Song
{
    internal class Metadata
    {
        private string m_name, m_artist, m_charter, m_genre, m_album;

        public string name
        {
            get => m_name;
            [MemberNotNull(nameof(m_name))]
            set => m_name = MakeValidMetadataString(value);
        }

        public string artist
        {
            get => m_artist;
            [MemberNotNull(nameof(m_artist))]
            set => m_artist = MakeValidMetadataString(value);
        }

        public string charter
        {
            get => m_charter;
            [MemberNotNull(nameof(m_charter))]
            set => m_charter = MakeValidMetadataString(value);
        }

        public string genre
        {
            get => m_genre;
            [MemberNotNull(nameof(m_genre))]
            set => m_genre = MakeValidMetadataString(value);
        }

        public string album
        {
            get => m_album;
            [MemberNotNull(nameof(m_album))]
            set => m_album = MakeValidMetadataString(value);
        }

        public string year;

        public int difficulty;
        public float previewStart, previewEnd;

        public Metadata()
        {
            name = artist = charter = album = year = string.Empty;
            difficulty = 0;
            previewStart = previewEnd = 0;
            genre = "rock";
        }

        public Metadata(Metadata metaData)
        {
            name = metaData.name;
            artist = metaData.artist;
            charter = metaData.charter;
            album = metaData.album;
            year = metaData.year;
            difficulty = metaData.difficulty;
            previewStart = metaData.previewStart;
            previewEnd = metaData.previewEnd;
            genre = metaData.genre;
        }

        private string MakeValidMetadataString(string v)
        {
            return v.Replace("\"", string.Empty);
        }
    }
}
