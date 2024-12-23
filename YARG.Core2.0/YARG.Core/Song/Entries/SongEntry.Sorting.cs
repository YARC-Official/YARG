using System;
using System.Collections.Generic;

namespace YARG.Core.Song
{
    public enum SongAttribute
    {
        Name,
        Artist,
        Album,
        Artist_Album,
        Genre,
        Year,
        Charter,
        Playlist,
        Source,
        SongLength,
        DateAdded,
    };

    public partial class SongEntry : IComparable<SongEntry>
    {
        public int CompareTo(SongEntry other)
        {
            int strCmp;
            if ((strCmp = Name.CompareTo(other.Name)) == 0 &&
                (strCmp = Artist.CompareTo(other.Artist)) == 0 &&
                (strCmp = Album.CompareTo(other.Album)) == 0 &&
                (strCmp = Charter.CompareTo(other.Charter)) == 0)
            {
                strCmp = Location.CompareTo(other.Location);
            }
            return strCmp;
        }

        public abstract DateTime GetAddDate();

        public bool IsPreferedOver(SongEntry other)
        {
            if (SubType != other.SubType)
            {
                // CON > ExCON > Sng > Ini
                return SubType > other.SubType;
            }
            // Otherwise, whatever would appear first
            return CompareTo(other) < 0;
        }

    }

    public sealed class EntryComparer : IComparer<SongEntry>
    {
        private readonly SongAttribute attribute;

        public EntryComparer(SongAttribute attribute) { this.attribute = attribute; }

        public int Compare(SongEntry lhs, SongEntry rhs) { return IsLowerOrdered(lhs, rhs) ? -1 : 1; }

        private bool IsLowerOrdered(SongEntry lhs, SongEntry rhs)
        {
            switch (attribute)
            {
                case SongAttribute.Album:
                    if (lhs.AlbumTrack != rhs.AlbumTrack)
                        return lhs.AlbumTrack < rhs.AlbumTrack;
                    break;
                case SongAttribute.Year:
                    if (lhs.YearAsNumber != rhs.YearAsNumber)
                        return lhs.YearAsNumber < rhs.YearAsNumber;
                    break;
                case SongAttribute.Playlist:
                    if (lhs is RBCONEntry rblhs && rhs is RBCONEntry rbrhs)
                    {
                        int lhsBand = rblhs.RBBandDiff;
                        int rhsBand = rbrhs.RBBandDiff;
                        if (lhsBand != -1 && rhsBand != -1)
                            return lhsBand < rhsBand;
                    }

                    if (lhs.PlaylistTrack != rhs.PlaylistTrack)
                        return lhs.PlaylistTrack < rhs.PlaylistTrack;

                    if (lhs.BandDifficulty != rhs.BandDifficulty)
                        return lhs.BandDifficulty < rhs.BandDifficulty;
                    break;
                case SongAttribute.SongLength:
                    if (lhs.SongLengthMilliseconds != rhs.SongLengthMilliseconds)
                        return lhs.SongLengthMilliseconds < rhs.SongLengthMilliseconds;
                    break;
            }

            return lhs.CompareTo(rhs) < 0;
        }
    }

    public sealed class InstrumentComparer : IComparer<SongEntry>
    {
        private static readonly EntryComparer baseComparer = new(SongAttribute.Name);
        public readonly Instrument instrument;
        public InstrumentComparer(Instrument instrument)
        {
            this.instrument = instrument;
        }

        public int Compare(SongEntry lhs, SongEntry rhs)
        {
            if (lhs == rhs)
                return 0;

            var lhsValues = lhs[instrument];
            var rhsValues = rhs[instrument];

            // This function only gets called if both entries have the instrument
            // That check is not necessary
            if (lhsValues.Intensity != rhsValues.Intensity)
            {
                if (lhsValues.Intensity != -1 && (rhsValues.Intensity == -1 || lhsValues.Intensity < rhsValues.Intensity))
                {
                    return -1;
                }
                return 1;
            }
            return baseComparer.Compare(lhs, rhs);
        }
    }
}
