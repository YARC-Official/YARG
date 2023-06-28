using System;

namespace YARG.Data
{
    [Flags]
    public enum FretFlag
    {
        NONE = 0,
        GREEN = 1,
        RED = 2,
        YELLOW = 4,
        BLUE = 8,
        ORANGE = 16,
        OPEN = 32
    }

    public static class FretFlagExtensions
    {
        public static bool IsFlagSingleNote(this FretFlag flag)
        {
            // Check for only 1 bit enabled
            return flag != 0 && (flag & (flag - 1)) == 0;
        }
    }
}