namespace YARG.Data
{
    public struct YargVersion
    {
        public int version;
        public int major;
        public int minor;
        public bool beta;

        public YargVersion(int version, int major, int minor)
        {
            this.version = version;
            this.major = major;
            this.minor = minor;
            beta = false;
        }

        public YargVersion(int version, int major, int minor, bool beta)
        {
            this.version = version;
            this.major = major;
            this.minor = minor;
            this.beta = beta;
        }

        public override string ToString()
        {
            if (beta)
            {
                return $"v{version}.{major}.{minor}b";
            }
            else
            {
                return $"v{version}.{major}.{minor}";
            }
        }

        public static YargVersion Parse(string version)
        {
            try
            {
                string[] split = version[1..].Split('.');

                bool beta = false;
                if (split[2].EndsWith("b"))
                {
                    split[2] = split[2][..^1];
                    beta = true;
                }

                int versionNum = int.Parse(split[0]);
                int majorNum = int.Parse(split[1]);
                int minorNum = int.Parse(split[2]);

                return new YargVersion(versionNum, majorNum, minorNum, beta);
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
            return a.version == b.version && a.major == b.major &&
                a.minor == b.minor && a.beta == b.beta;
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

            if (a.version > b.version)
            {
                return true;
            }

            if (a.version == b.version)
            {
                if (a.major > b.major)
                {
                    return true;
                }

                if (a.major == b.major)
                {
                    if (a.minor > b.minor)
                    {
                        return true;
                    }

                    if (a.minor == b.minor)
                    {
                        if (!a.beta && b.beta)
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