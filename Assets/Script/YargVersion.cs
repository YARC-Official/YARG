using System;
using UnityEngine;

namespace YARG
{
    public readonly struct YargVersion
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Revision;
        public readonly string PrereleaseText;

        public readonly bool IsPrerelease => !string.IsNullOrEmpty(PrereleaseText);

        public readonly int VersionBits;

        public YargVersion(int major, int minor, int revision, string prerelease = null)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            PrereleaseText = prerelease;

            VersionBits = (byte) Major << 24;
            VersionBits |= (byte) Minor << 16;
            VersionBits |= (byte) Revision << 8;
            VersionBits |= (byte) (IsPrerelease ? 1 : 0);
        }

        public override string ToString()
        {
            if (IsPrerelease)
            {
                return $"v{Major}.{Minor}.{Revision}-{PrereleaseText}";
            }
            else
            {
                return $"v{Major}.{Minor}.{Revision}";
            }
        }

        public static YargVersion Parse(string text)
        {
            try
            {
                // Remove starting 'v'
                if (text.StartsWith('v'))
                    text = text[1..];

                // Split out each of the numbers
                var parts = text.Split('.');
                string major = parts[0];
                string minor = parts[1];
                string revision = parts[2];

                // Check for prerelease info
                string prerelease = null;
                if (revision.Contains('-'))
                {
                    var revisionSplit = revision.Split('-', 2);
                    revision = revisionSplit[0];
                    prerelease = revisionSplit[1];
                }

                // Parse the version numbers
                int majorNum = int.Parse(major);
                int minorNum = int.Parse(minor);
                int revisionNum = int.Parse(revision);

                return new YargVersion(majorNum, minorNum, revisionNum, prerelease);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to parse version string!");
                Debug.LogException(ex);
                return new YargVersion(0, 0, 0);
            }
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(YargVersion a, YargVersion b)
        {
            return a.Major == b.Major && a.Minor == b.Minor &&
                a.Revision == b.Revision && a.PrereleaseText == b.PrereleaseText;
        }

        public static bool operator !=(YargVersion a, YargVersion b)
        {
            return !(a == b);
        }

        public static bool operator >(YargVersion a, YargVersion b)
        {
            if (a == b)
            {
                return false;
            }

            if (a.Major > b.Major)
            {
                return true;
            }

            if (a.Major != b.Major) return false;

            if (a.Minor > b.Minor)
            {
                return true;
            }

            if (a.Minor != b.Minor) return false;

            if (a.Revision > b.Revision)
            {
                return true;
            }

            if (a.Revision != b.Revision) return false;

            if (!a.IsPrerelease && b.IsPrerelease)
            {
                return true;
            }

            return false;
        }

        public static bool operator <(YargVersion a, YargVersion b)
        {
            return !(a > b || a == b);
        }
    }
}