namespace YARG
{
    public readonly struct YargVersion
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Revision;
        public readonly bool Beta;

        public readonly int VersionBits;

        public YargVersion(int major, int minor, int revision, bool beta = false)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            Beta = beta;

            VersionBits = (byte) Major << 24;
            VersionBits |= (byte) Minor << 16;
            VersionBits |= (byte) Revision << 8;
            VersionBits |= (byte) (Beta ? 1 : 0);
        }

        public override string ToString()
        {
            if (Beta)
            {
                return $"v{Major}.{Minor}.{Revision}b";
            }
            else
            {
                return $"v{Major}.{Minor}.{Revision}";
            }
        }

        public static YargVersion Parse(string major)
        {
            try
            {
                string[] split = major[1..].Split('.');

                bool beta = false;
                if (split[2].EndsWith("b"))
                {
                    split[2] = split[2][..^1];
                    beta = true;
                }

                int majorNum = int.Parse(split[0]);
                int minorNum = int.Parse(split[1]);
                int revisionNum = int.Parse(split[2]);

                return new YargVersion(majorNum, minorNum, revisionNum, beta);
            }
            catch
            {
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
                a.Revision == b.Revision && a.Beta == b.Beta;
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

            if (a.Major == b.Major)
            {
                if (a.Minor > b.Minor)
                {
                    return true;
                }

                if (a.Minor == b.Minor)
                {
                    if (a.Revision > b.Revision)
                    {
                        return true;
                    }

                    if (a.Revision == b.Revision)
                    {
                        if (!a.Beta && b.Beta)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool operator <(YargVersion a, YargVersion b)
        {
            return !(a > b || a == b);
        }
    }
}